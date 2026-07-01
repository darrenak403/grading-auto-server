using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GradingSystem.Application.DTOs;
using GradingSystem.Application.Exceptions;
using GradingSystem.Application.Interfaces;
using GradingSystem.Application.Options;
using GradingSystem.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GradingSystem.Application.Services;

public class SupabaseSyncService : ISupabaseSyncService
{
    private readonly HttpClient _client;
    private readonly IUnitOfWork _uow;
    private readonly SupabaseOptions _options;
    private readonly ILogger<SupabaseSyncService> _logger;

    public SupabaseSyncService(
        HttpClient client,
        IUnitOfWork uow,
        IOptions<SupabaseOptions> options,
        ILogger<SupabaseSyncService> logger)
    {
        _client = client;
        _uow = uow;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SyncSubmissionAsync(
        Guid submissionId,
        string? labIdOverride = null,
        string? classNameOverride = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Url) || 
            string.IsNullOrWhiteSpace(_options.ServiceRoleKey) ||
            _options.Url.Contains("your-project-id") || 
            _options.ServiceRoleKey.Contains("your-service-role-key"))
        {
            throw new InvalidOperationException("Supabase is not configured or still using default placeholder values. Please set SUPABASE_URL and SUPABASE_SERVICE_ROLE_KEY correctly and restart the application.");
        }

        var submission = await _uow.LabSubmissions.GetByIdAsync(submissionId);
        if (submission is null)
        {
            throw new NotFoundException($"LabSubmission '{submissionId}' not found.");
        }

        var assignment = await _uow.LabAssignments.GetByIdAsync(submission.LabAssignmentId);
        if (assignment is null)
        {
            throw new NotFoundException($"LabAssignment '{submission.LabAssignmentId}' not found.");
        }

        var resolvedLabId = string.IsNullOrWhiteSpace(labIdOverride) ? assignment.Title : labIdOverride.Trim();

        // Check if there is an approved resubmission request on Supabase
        string? approvedRequestId = null;
        try
        {
            var checkUrl = $"rest/v1/resubmission_requests?student_id=eq.{submission.StudentCode}&lab_id=eq.{Uri.EscapeDataString(resolvedLabId)}&status=eq.approved&order=updated_at.desc&limit=1";
            var checkRequest = new HttpRequestMessage(HttpMethod.Get, checkUrl);
            checkRequest.Headers.Add("apikey", _options.ServiceRoleKey);
            checkRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ServiceRoleKey);

            var checkResponse = await _client.SendAsync(checkRequest, ct);
            if (checkResponse.IsSuccessStatusCode)
            {
                var json = await checkResponse.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    var first = doc.RootElement[0];
                    if (first.TryGetProperty("id", out var idProp))
                    {
                        approvedRequestId = idProp.ToString();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while checking resubmission requests for student {StudentId} and lab {LabId}", submission.StudentCode, string.IsNullOrWhiteSpace(labIdOverride) ? assignment.Title : labIdOverride.Trim());
        }

        var jobs = await _uow.LabGradingJobs.FindAsync(j => j.LabSubmissionId == submissionId);
        var latestJob = jobs.OrderByDescending(j => j.CreatedAt).FirstOrDefault();
        if (latestJob is null)
        {
            _logger.LogWarning("No grading jobs found for submission {SubmissionId}. Cannot sync.", submissionId);
            return;
        }

        var results = (await _uow.LabTestCaseResults.FindAsync(r => r.LabGradingJobId == latestJob.Id)).ToList();
        var testCaseIds = results.Select(r => r.LabTestCaseId).ToHashSet();
        var testCases = (await _uow.LabTestCases.FindAsync(t => testCaseIds.Contains(t.Id)))
            .ToDictionary(t => t.Id);

        // Sắp xếp kết quả test case theo thứ tự Order của TestCase
        var sortedResults = results.OrderBy(r =>
        {
            if (testCases.TryGetValue(r.LabTestCaseId, out var tc))
            {
                return tc.Order;
            }
            return 99999; // Đẩy các test case không xác định xuống cuối
        }).ThenBy(r => r.CreatedAt).ToList();

        // Tính toán tổng điểm
        var score = sortedResults.Sum(r => r.ManualOverrideScore ?? r.AwardedScore);

        // Build details JSON
        var testDetails = sortedResults.Select(r =>
        {
            testCases.TryGetValue(r.LabTestCaseId, out var tc);
            var name = tc is not null
                ? (tc.HttpMethod.Equals("SOURCE", StringComparison.OrdinalIgnoreCase)
                    ? $"[SOURCE] {tc.Description ?? "No description"}"
                    : $"[{tc.HttpMethod}] {tc.UrlTemplate}{(string.IsNullOrWhiteSpace(tc.Description) ? "" : $" - {tc.Description}")}")
                : "Unknown test case";

            return new SupabaseTestDetailDto
            {
                Name = name,
                Passed = r.Passed,
                Score = r.ManualOverrideScore ?? r.AwardedScore,
                MaxScore = tc?.Score ?? 0,
                Error = r.ErrorMessage,
                ActualResponse = r.ActualResponse,
                ActualStatusCode = r.ActualStatusCode
            };
        }).ToList();

        var details = new SupabaseDetailsDto
        {
            Passed = sortedResults.Count(r => r.Passed),
            Failed = sortedResults.Count(r => !r.Passed),
            Total = sortedResults.Count,
            Tests = testDetails
        };

        // Tra cứu lớp học từ Supabase
        var className = string.IsNullOrWhiteSpace(classNameOverride)
            ? await GetClassNameAsync(submission.StudentCode, ct)
            : classNameOverride.Trim();

        // Gửi request POST UPSERT lên Supabase
        var payload = new List<SupabaseSubmissionPayloadDto>
        {
            new()
            {
                StudentId = submission.StudentCode,
                LabId = resolvedLabId,
                ClassName = className,
                Score = score,
                Status = submission.Status.ToString(),
                Details = details,
                UpdatedAt = DateTime.UtcNow
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "rest/v1/submissions")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("Prefer", "resolution=merge-duplicates");

        // Đảm bảo có ApiKey & Authorization header
        request.Headers.Add("apikey", _options.ServiceRoleKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ServiceRoleKey);

        var response = await _client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            throw new Exception($"Failed to sync submission to Supabase: {response.StatusCode} - {errorContent}");
        }

        _logger.LogInformation("Successfully synced submission {SubmissionId} ({StudentCode}) to Supabase", submissionId, submission.StudentCode);

        // Mark the approved resubmission request as completed if it exists
        if (!string.IsNullOrEmpty(approvedRequestId))
        {
            try
            {
                var patchUrl = $"rest/v1/resubmission_requests?id=eq.{approvedRequestId}&status=eq.approved";
                var patchPayload = new
                {
                    status = "completed",
                    completed_at = DateTime.UtcNow,
                    updated_at = DateTime.UtcNow
                };

                var patchRequest = new HttpRequestMessage(HttpMethod.Patch, patchUrl)
                {
                    Content = JsonContent.Create(patchPayload)
                };
                patchRequest.Headers.Add("apikey", _options.ServiceRoleKey);
                patchRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ServiceRoleKey);

                var patchResponse = await _client.SendAsync(patchRequest, ct);
                if (!patchResponse.IsSuccessStatusCode)
                {
                    var errContent = await patchResponse.Content.ReadAsStringAsync(ct);
                    _logger.LogError("Failed to update resubmission request {RequestId} to completed: {StatusCode} - {Error}", approvedRequestId, patchResponse.StatusCode, errContent);
                }
                else
                {
                    _logger.LogInformation("Successfully marked resubmission request {RequestId} as completed", approvedRequestId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while updating resubmission request {RequestId} to completed", approvedRequestId);
            }
        }
    }

    public async Task<int> SyncAssignmentAsync(Guid assignmentId, SyncSupabaseRequest? request = null, CancellationToken ct = default)
    {
        var assignment = await _uow.LabAssignments.GetByIdAsync(assignmentId);
        if (assignment is null)
        {
            throw new NotFoundException($"LabAssignment '{assignmentId}' not found.");
        }

        var submissions = (await _uow.LabSubmissions.FindAsync(s => s.LabAssignmentId == assignmentId)).ToList();
        if (submissions.Count == 0) return 0;

        var syncedCount = 0;
        foreach (var sub in submissions)
        {
            try
            {
                await SyncSubmissionAsync(sub.Id, request?.LabId, request?.ClassName, ct);
                syncedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync submission {SubmissionId} to Supabase", sub.Id);
            }
        }

        return syncedCount;
    }

    private async Task<string> GetClassNameAsync(string studentId, CancellationToken ct)
    {
        try
        {
            // 1. Tra cứu bảng allowed_emails
            var request = new HttpRequestMessage(HttpMethod.Get, $"rest/v1/allowed_emails?student_id=eq.{studentId}&select=class_name");
            request.Headers.Add("apikey", _options.ServiceRoleKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ServiceRoleKey);

            var response = await _client.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    var first = doc.RootElement[0];
                    if (first.TryGetProperty("class_name", out var prop) && prop.ValueKind == JsonValueKind.String)
                    {
                        var val = prop.GetString();
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }
                }
            }

            // 2. Tra cứu bảng students
            var requestStd = new HttpRequestMessage(HttpMethod.Get, $"rest/v1/students?student_id=eq.{studentId}&select=class_name");
            requestStd.Headers.Add("apikey", _options.ServiceRoleKey);
            requestStd.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ServiceRoleKey);

            var responseStd = await _client.SendAsync(requestStd, ct);
            if (responseStd.IsSuccessStatusCode)
            {
                var json = await responseStd.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    var first = doc.RootElement[0];
                    if (first.TryGetProperty("class_name", out var prop) && prop.ValueKind == JsonValueKind.String)
                    {
                        var val = prop.GetString();
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while querying class_name from Supabase for student {StudentId}", studentId);
        }

        return "UNKNOWN";
    }

    private class SupabaseSubmissionPayloadDto
    {
        [JsonPropertyName("student_id")]
        public string StudentId { get; set; } = "";

        [JsonPropertyName("lab_id")]
        public string LabId { get; set; } = "";

        [JsonPropertyName("class_name")]
        public string ClassName { get; set; } = "";

        [JsonPropertyName("score")]
        public decimal Score { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "";

        [JsonPropertyName("details")]
        public SupabaseDetailsDto? Details { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    private class SupabaseDetailsDto
    {
        [JsonPropertyName("passed")]
        public int Passed { get; set; }

        [JsonPropertyName("failed")]
        public int Failed { get; set; }

        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("tests")]
        public List<SupabaseTestDetailDto> Tests { get; set; } = [];
    }

    private class SupabaseTestDetailDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("passed")]
        public bool Passed { get; set; }

        [JsonPropertyName("score")]
        public decimal Score { get; set; }

        [JsonPropertyName("max_score")]
        public decimal MaxScore { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("actual_response")]
        public string? ActualResponse { get; set; }

        [JsonPropertyName("actual_status_code")]
        public int? ActualStatusCode { get; set; }
    }
}
