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
        string? termId = null,
        CancellationToken ct = default)
    {
        EnsureSupabaseConfigured();

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

        var resolvedLabCode = NormalizeSupabaseKey(string.IsNullOrWhiteSpace(labIdOverride) ? assignment.Title : labIdOverride);

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

        var studentCode = NormalizeSupabaseKey(submission.StudentCode);
        var className = NormalizeSupabaseKey(string.IsNullOrWhiteSpace(classNameOverride)
            ? await GetClassNameAsync(submission.StudentCode, ct)
            : classNameOverride);

        if (className == "UNKNOWN")
        {
            throw new InvalidOperationException($"Cannot resolve class_name for student '{studentCode}'. Pass className in the sync request body or configure Supabase roster data.");
        }

        var syncResult = await SyncResolvedGradeAsync(
            studentCode,
            className,
            resolvedLabCode,
            score,
            details,
            sourceUrl: null,
            termId,
            ct);

        _logger.LogInformation(
            "Successfully synced submission {SubmissionId} ({StudentCode}) to Supabase class_lab_submissions as {ItemType}",
            submissionId,
            studentCode,
            syncResult.ItemType);
    }

    public async Task<SupabaseDropdownOptionsDto> GetDropdownOptionsAsync(string? termId = null, string? className = null, CancellationToken ct = default)
    {
        EnsureSupabaseConfigured();

        var normalizedTermId = NormalizeNullableSupabaseKey(termId);
        var normalizedClassName = string.IsNullOrWhiteSpace(className)
            ? null
            : NormalizeSupabaseKey(className);

        var terms = await GetTermOptionsAsync(ct);
        var classes = await GetClassOptionsAsync(normalizedTermId, ct);
        var labs = await GetLabOptionsAsync(normalizedTermId, normalizedClassName, ct);

        return new SupabaseDropdownOptionsDto(terms, classes, labs);
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
                await SyncSubmissionAsync(sub.Id, request?.LabId, request?.ClassName, request?.TermId, ct);
                syncedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync submission {SubmissionId} to Supabase", sub.Id);
            }
        }

        return syncedCount;
    }

    public async Task<SyncSupabaseGradeResponse> SyncGradeAsync(SyncSupabaseGradeRequest request, CancellationToken ct = default)
    {
        EnsureSupabaseConfigured();

        ValidateGradeRequest(request);

        var studentCode = NormalizeSupabaseKey(request.StudentCode);
        var className = NormalizeSupabaseKey(request.ClassName);
        var labCode = NormalizeSupabaseKey(request.LabCode);

        return await SyncResolvedGradeAsync(
            studentCode,
            className,
            labCode,
            request.Score,
            request.Details,
            request.SourceUrl,
            request.TermId,
            ct);
    }

    public async Task<SyncSupabaseGradesResponse> SyncGradesAsync(SyncSupabaseGradesRequest request, CancellationToken ct = default)
    {
        EnsureSupabaseConfigured();

        if (string.IsNullOrWhiteSpace(request.ClassName))
        {
            throw new BadRequestException("className is required.");
        }
        if (string.IsNullOrWhiteSpace(request.LabCode))
        {
            throw new BadRequestException("labCode is required.");
        }
        if (request.Submissions is null || request.Submissions.Count == 0)
        {
            throw new BadRequestException("submissions must contain at least one item.");
        }

        var className = NormalizeSupabaseKey(request.ClassName);
        var labCode = NormalizeSupabaseKey(request.LabCode);
        var termId = NormalizeNullableSupabaseKey(request.TermId);
        var synced = new List<SyncSupabaseGradeItemResult>();
        var failed = new List<SyncSupabaseGradeItemFailure>();

        foreach (var item in request.Submissions)
        {
            var studentCodeForError = string.IsNullOrWhiteSpace(item.StudentCode)
                ? "(missing)"
                : NormalizeSupabaseKey(item.StudentCode);

            try
            {
                ValidateGradeItemRequest(item);

                var studentCode = NormalizeSupabaseKey(item.StudentCode);
                var result = await SyncResolvedGradeAsync(
                    studentCode,
                    className,
                    labCode,
                    item.Score,
                    item.Details,
                    item.SourceUrl,
                    termId,
                    ct);

                synced.Add(new SyncSupabaseGradeItemResult(
                    studentCode,
                    result.ClassStudentId,
                    result.ClassLabId,
                    result.ItemType,
                    result.FulfillsRequestId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync Supabase grade for student {StudentCode}", studentCodeForError);
                failed.Add(new SyncSupabaseGradeItemFailure(studentCodeForError, ex.Message));
            }
        }

        return new SyncSupabaseGradesResponse(
            request.Submissions.Count,
            synced.Count,
            failed.Count,
            synced,
            failed);
    }

    private async Task<string> GetClassNameAsync(string studentId, CancellationToken ct)
    {
        try
        {
            var normalizedStudentId = NormalizeSupabaseKey(studentId);

            // 1. Tra cứu schema ERD mới.
            var classStudentRequest = new HttpRequestMessage(
                HttpMethod.Get,
                $"rest/v1/class_students?select=classes!inner(name),students!inner(student_code)&students.student_code=eq.{EscapeFilterValue(normalizedStudentId)}&limit=1");
            AddSupabaseAuthHeaders(classStudentRequest);

            var classStudentResponse = await _client.SendAsync(classStudentRequest, ct);
            if (classStudentResponse.IsSuccessStatusCode)
            {
                var json = await classStudentResponse.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    var first = doc.RootElement[0];
                    if (first.TryGetProperty("classes", out var classesProp) &&
                        classesProp.ValueKind == JsonValueKind.Object &&
                        classesProp.TryGetProperty("name", out var nameProp) &&
                        nameProp.ValueKind == JsonValueKind.String)
                    {
                        var val = nameProp.GetString();
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }
                }
            }

            // 2. Tra cứu bảng cũ để tương thích dữ liệu legacy.
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

            // 3. Tra cứu bảng students cũ.
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

    private static void ValidateGradeRequest(SyncSupabaseGradeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.StudentCode))
        {
            throw new BadRequestException("studentCode is required.");
        }
        if (string.IsNullOrWhiteSpace(request.ClassName))
        {
            throw new BadRequestException("className is required.");
        }
        if (string.IsNullOrWhiteSpace(request.LabCode))
        {
            throw new BadRequestException("labCode is required.");
        }
        if (request.Details.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new BadRequestException("details is required.");
        }
    }

    private static void ValidateGradeItemRequest(SyncSupabaseGradeItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.StudentCode))
        {
            throw new BadRequestException("studentCode is required.");
        }
        if (request.Details.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new BadRequestException("details is required.");
        }
    }

    private async Task<SyncSupabaseGradeResponse> SyncResolvedGradeAsync(
        string studentCode,
        string className,
        string labCode,
        decimal score,
        object details,
        string? sourceUrl,
        string? termId,
        CancellationToken ct)
    {
        var normalizedTermId = NormalizeNullableSupabaseKey(termId);
        var classStudentId = await GetRequiredClassStudentIdAsync(studentCode, className, normalizedTermId, ct);
        var classLab = await GetRequiredClassLabAsync(labCode, className, normalizedTermId, ct);
        var approvedRequestId = await GetApprovedResubmissionRequestIdAsync(classStudentId, classLab.Id, ct);
        var itemType = DetermineItemType(approvedRequestId, classLab.Deadline);

        await CreateClassLabSubmissionAsync(
            classStudentId,
            classLab.Id,
            itemType,
            score,
            details,
            sourceUrl,
            approvedRequestId,
            ct);

        if (!string.IsNullOrEmpty(approvedRequestId))
        {
            await CompleteResubmissionRequestAsync(approvedRequestId, ct);
        }

        return new SyncSupabaseGradeResponse(classStudentId, classLab.Id, itemType, approvedRequestId);
    }

    private async Task CompleteResubmissionRequestAsync(string requestId, CancellationToken ct)
    {
        try
        {
            var patchUrl = $"rest/v1/resubmission_requests_v2?id=eq.{EscapeFilterValue(requestId)}&status=eq.approved";
            var now = DateTime.UtcNow;
            var patchPayload = new
            {
                status = "completed",
                completed_at = now,
                updated_at = now
            };

            var patchRequest = new HttpRequestMessage(HttpMethod.Patch, patchUrl)
            {
                Content = JsonContent.Create(patchPayload)
            };
            AddSupabaseAuthHeaders(patchRequest);

            var patchResponse = await _client.SendAsync(patchRequest, ct);
            if (!patchResponse.IsSuccessStatusCode)
            {
                var errContent = await patchResponse.Content.ReadAsStringAsync(ct);
                _logger.LogError("Failed to update resubmission request {RequestId} to completed: {StatusCode} - {Error}", requestId, patchResponse.StatusCode, errContent);
            }
            else
            {
                _logger.LogInformation("Successfully marked resubmission request v2 {RequestId} as completed", requestId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while updating resubmission request {RequestId} to completed", requestId);
        }
    }

    private async Task<IReadOnlyList<SupabaseTermOptionDto>> GetTermOptionsAsync(CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "rest/v1/terms?select=id,name&order=name.asc");
        AddSupabaseAuthHeaders(request);

        var response = await _client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            throw new Exception($"Failed to query Supabase terms: {response.StatusCode} - {errorContent}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return doc.RootElement.EnumerateArray()
            .Select(ParseTermOption)
            .Where(option => option is not null)
            .Select(option => option!)
            .GroupBy(option => option.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(option => option.Code ?? option.Name ?? option.Id)
            .ToList();
    }

    private async Task<IReadOnlyList<SupabaseClassOptionDto>> GetClassOptionsAsync(string? termId, CancellationToken ct)
    {
        var url = "rest/v1/classes"
            + "?select=name,terms!inner(id,name)";

        if (!string.IsNullOrWhiteSpace(termId))
        {
            url += $"&terms.id=eq.{EscapeFilterValue(termId)}";
        }

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddSupabaseAuthHeaders(request);

        var response = await _client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            throw new Exception($"Failed to query Supabase classes: {response.StatusCode} - {errorContent}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return doc.RootElement.EnumerateArray()
            .Select(ParseClassOption)
            .Where(option => option is not null)
            .Select(option => option!)
            .GroupBy(option => $"{option.TermId}|{option.Name}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(option => option.TermCode ?? option.TermName ?? option.TermId)
            .ThenBy(option => option.Name)
            .ToList();
    }

    private async Task<IReadOnlyList<SupabaseLabOptionDto>> GetLabOptionsAsync(string? termId, string? className, CancellationToken ct)
    {
        var url = "rest/v1/class_labs"
            + "?select=deadline,labs!inner(code),classes!inner(name,terms!inner(id,name))";

        if (!string.IsNullOrWhiteSpace(termId))
        {
            url += $"&classes.terms.id=eq.{EscapeFilterValue(termId)}";
        }
        if (!string.IsNullOrWhiteSpace(className))
        {
            url += $"&classes.name=eq.{EscapeFilterValue(className)}";
        }

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddSupabaseAuthHeaders(request);

        var response = await _client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            throw new Exception($"Failed to query Supabase class labs: {response.StatusCode} - {errorContent}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return doc.RootElement.EnumerateArray()
            .Select(ParseLabOption)
            .Where(option => option is not null)
            .Select(option => option!)
            .GroupBy(option => $"{option.TermId}|{option.ClassName}|{option.Code}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(option => option.TermCode ?? option.TermName ?? option.TermId)
            .ThenBy(option => option.ClassName)
            .ThenBy(option => option.Code)
            .ToList();
    }

    private async Task<string> GetRequiredClassStudentIdAsync(string studentCode, string className, string? termId, CancellationToken ct)
    {
        var url = "rest/v1/class_students"
            + "?select=id,students!inner(student_code),"
            + (string.IsNullOrWhiteSpace(termId)
                ? "classes!inner(name)"
                : "classes!inner(name,terms!inner(id))")
            + $"&students.student_code=eq.{EscapeFilterValue(studentCode)}"
            + $"&classes.name=eq.{EscapeFilterValue(className)}"
            + "&limit=1";

        if (!string.IsNullOrWhiteSpace(termId))
        {
            url += $"&classes.terms.id=eq.{EscapeFilterValue(termId)}";
        }

        var element = await GetFirstArrayElementAsync(url, ct);
        if (element is null || !element.Value.TryGetProperty("id", out var idProp) || idProp.ValueKind != JsonValueKind.String)
        {
            throw new NotFoundException($"Supabase class_student not found for student '{studentCode}' in class '{className}'{FormatTermError(termId)}.");
        }

        return idProp.GetString()!;
    }

    private async Task<ClassLabRef> GetRequiredClassLabAsync(string labCode, string className, string? termId, CancellationToken ct)
    {
        var url = "rest/v1/class_labs"
            + "?select=id,deadline,labs!inner(code),"
            + (string.IsNullOrWhiteSpace(termId)
                ? "classes!inner(name)"
                : "classes!inner(name,terms!inner(id))")
            + $"&labs.code=eq.{EscapeFilterValue(labCode)}"
            + $"&classes.name=eq.{EscapeFilterValue(className)}"
            + "&limit=1";

        if (!string.IsNullOrWhiteSpace(termId))
        {
            url += $"&classes.terms.id=eq.{EscapeFilterValue(termId)}";
        }

        var element = await GetFirstArrayElementAsync(url, ct);
        if (element is null || !element.Value.TryGetProperty("id", out var idProp) || idProp.ValueKind != JsonValueKind.String)
        {
            throw new NotFoundException($"Supabase class_lab not found for lab '{labCode}' in class '{className}'{FormatTermError(termId)}.");
        }

        DateTimeOffset? deadline = null;
        if (element.Value.TryGetProperty("deadline", out var deadlineProp) &&
            deadlineProp.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(deadlineProp.GetString(), out var parsedDeadline))
        {
            deadline = parsedDeadline;
        }

        return new ClassLabRef(idProp.GetString()!, deadline);
    }

    private async Task<string?> GetApprovedResubmissionRequestIdAsync(string classStudentId, string classLabId, CancellationToken ct)
    {
        var url = "rest/v1/resubmission_requests_v2"
            + "?select=id"
            + $"&class_student_id=eq.{EscapeFilterValue(classStudentId)}"
            + $"&class_lab_id=eq.{EscapeFilterValue(classLabId)}"
            + "&status=eq.approved"
            + "&created_submission_id=is.null"
            + "&order=updated_at.desc"
            + "&limit=1";

        var element = await GetFirstArrayElementAsync(url, ct);
        if (element is not null && element.Value.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
        {
            return idProp.GetString();
        }

        return null;
    }

    private async Task CreateClassLabSubmissionAsync(
        string classStudentId,
        string classLabId,
        string itemType,
        decimal score,
        object details,
        string? sourceUrl,
        string? fulfillsRequestId,
        CancellationToken ct)
    {
        var payload = new
        {
            p_class_student_id = classStudentId,
            p_class_lab_id = classLabId,
            p_item_type = itemType,
            p_source_url = sourceUrl,
            p_score = score,
            p_details = details,
            p_fulfills_request_id = fulfillsRequestId
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "rest/v1/rpc/create_class_lab_submission")
        {
            Content = JsonContent.Create(payload)
        };
        AddSupabaseAuthHeaders(request);

        var response = await _client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            throw new BadRequestException($"Failed to create Supabase class_lab_submission: {response.StatusCode} - {errorContent}");
        }
    }

    private async Task<JsonElement?> GetFirstArrayElementAsync(string url, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddSupabaseAuthHeaders(request);

        var response = await _client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            throw new BadRequestException($"Supabase query failed: {response.StatusCode} - {errorContent}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
        {
            return null;
        }

        return doc.RootElement[0].Clone();
    }

    private static string DetermineItemType(string? approvedRequestId, DateTimeOffset? deadline)
    {
        if (!string.IsNullOrEmpty(approvedRequestId))
        {
            return "resubmit";
        }

        return deadline.HasValue && DateTimeOffset.UtcNow > deadline.Value.ToUniversalTime()
            ? "late"
            : "original";
    }

    private void AddSupabaseAuthHeaders(HttpRequestMessage request)
    {
        request.Headers.Add("apikey", _options.ServiceRoleKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ServiceRoleKey);
    }

    private void EnsureSupabaseConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.Url) ||
            string.IsNullOrWhiteSpace(_options.ServiceRoleKey) ||
            _options.Url.Contains("your-project-id") ||
            _options.ServiceRoleKey.Contains("your-service-role-key"))
        {
            throw new InvalidOperationException("Supabase is not configured or still using default placeholder values. Please set SUPABASE_URL and SUPABASE_SERVICE_ROLE_KEY correctly and restart the application.");
        }
    }

    private static SupabaseTermOptionDto? ParseTermOption(JsonElement item)
    {
        if (!item.TryGetProperty("id", out var idProp) ||
            idProp.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(idProp.GetString()))
        {
            return null;
        }

        var code = GetOptionalString(item, "code");
        var name = GetOptionalString(item, "name");
        return new SupabaseTermOptionDto(idProp.GetString()!, code, name);
    }

    private static SupabaseClassOptionDto? ParseClassOption(JsonElement item)
    {
        var className = GetOptionalString(item, "name");
        if (string.IsNullOrWhiteSpace(className))
        {
            return null;
        }

        var term = ParseNestedTerm(item);
        return new SupabaseClassOptionDto(className, term?.Id, term?.Code, term?.Name);
    }

    private static SupabaseLabOptionDto? ParseLabOption(JsonElement item)
    {
        if (!item.TryGetProperty("labs", out var labProp) ||
            labProp.ValueKind != JsonValueKind.Object ||
            !labProp.TryGetProperty("code", out var codeProp) ||
            codeProp.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(codeProp.GetString()))
        {
            return null;
        }

        string? className = null;
        if (item.TryGetProperty("classes", out var classProp) &&
            classProp.ValueKind == JsonValueKind.Object &&
            classProp.TryGetProperty("name", out var classNameProp) &&
            classNameProp.ValueKind == JsonValueKind.String)
        {
            className = classNameProp.GetString();
        }

        var term = item.TryGetProperty("classes", out var nestedClassProp) &&
            nestedClassProp.ValueKind == JsonValueKind.Object
            ? ParseNestedTerm(nestedClassProp)
            : null;

        DateTimeOffset? deadline = null;
        if (item.TryGetProperty("deadline", out var deadlineProp) &&
            deadlineProp.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(deadlineProp.GetString(), out var parsedDeadline))
        {
            deadline = parsedDeadline;
        }

        return new SupabaseLabOptionDto(codeProp.GetString()!, null, className, term?.Id, term?.Code, term?.Name, deadline);
    }

    private static string NormalizeSupabaseKey(string? value) => (value ?? "").Trim().ToUpperInvariant();

    private static string? NormalizeNullableSupabaseKey(string? value) => string.IsNullOrWhiteSpace(value)
        ? null
        : value.Trim();

    private static string EscapeFilterValue(string value) => Uri.EscapeDataString(value);

    private static string FormatTermError(string? termId) => string.IsNullOrWhiteSpace(termId)
        ? ""
        : $" in term '{termId}'";

    private static SupabaseTermOptionDto? ParseNestedTerm(JsonElement item)
    {
        if (!item.TryGetProperty("terms", out var termProp) || termProp.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return ParseTermOption(termProp);
    }

    private static string? GetOptionalString(JsonElement item, string propertyName)
    {
        return item.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }

    private record ClassLabRef(string Id, DateTimeOffset? Deadline);

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
