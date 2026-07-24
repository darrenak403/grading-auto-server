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

    public async Task<SyncSupabaseGradeResponse?> SyncSubmissionAsync(
        Guid submissionId,
        string? labIdOverride = null,
        string? classNameOverride = null,
        string? termId = null,
        string? gradingSessionId = null,
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
            return null;
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
            gradingSessionId,
            ct);

        _logger.LogInformation(
            "Successfully synced submission {SubmissionId} ({StudentCode}) to Supabase session_submissions for grading session {GradingSessionId}",
            submissionId,
            studentCode,
            syncResult.GradingSessionId);

        return syncResult;
    }

    public async Task<SupabaseDropdownOptionsDto> GetDropdownOptionsAsync(string? termId = null, string? className = null, string? labCode = null, CancellationToken ct = default)
    {
        EnsureSupabaseConfigured();

        var normalizedTermId = NormalizeNullableSupabaseKey(termId);
        var normalizedClassName = string.IsNullOrWhiteSpace(className)
            ? null
            : NormalizeSupabaseKey(className);
        var normalizedLabCode = string.IsNullOrWhiteSpace(labCode)
            ? null
            : NormalizeSupabaseKey(labCode);

        var terms = await GetTermOptionsAsync(ct);
        var classes = await GetClassOptionsAsync(normalizedTermId, ct);
        var sessions = await GetGradingSessionOptionsAsync(normalizedTermId, normalizedClassName, normalizedLabCode, ct);
        var labs = sessions
            .GroupBy(option => $"{option.TermId}|{option.ClassName}|{option.LabCode}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(option => option.Status == "open").First())
            .Select(option => new SupabaseLabOptionDto(
                option.LabCode,
                option.LabTitle,
                option.ClassName,
                option.TermId,
                option.TermCode,
                option.TermName,
                option.Deadline))
            .ToList();

        return new SupabaseDropdownOptionsDto(terms, classes, labs, sessions);
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
        var syncedSessionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sub in submissions)
        {
            try
            {
                var result = await SyncSubmissionAsync(sub.Id, request?.LabId, request?.ClassName, request?.TermId, request?.GradingSessionId, ct);
                if (result is not null)
                {
                    syncedSessionIds.Add(result.GradingSessionId);
                    syncedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync submission {SubmissionId} to Supabase", sub.Id);
            }
        }

        foreach (var sessionId in syncedSessionIds)
        {
            try
            {
                var backfilledCount = await BackfillMissingSessionSubmissionsFromPreviousAsync(sessionId, ct);
                if (backfilledCount > 0)
                {
                    _logger.LogInformation(
                        "Backfilled {BackfilledCount} missing Supabase submissions for grading session {GradingSessionId}",
                        backfilledCount,
                        sessionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to backfill missing Supabase submissions for grading session {GradingSessionId}",
                    sessionId);
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
            request.GradingSessionId,
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
        var syncedSessionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                    request.GradingSessionId,
                    ct);

                synced.Add(new SyncSupabaseGradeItemResult(
                    studentCode,
                    result.ClassStudentId,
                    result.GradingSessionId));
                syncedSessionIds.Add(result.GradingSessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync Supabase grade for student {StudentCode}", studentCodeForError);
                failed.Add(new SyncSupabaseGradeItemFailure(studentCodeForError, ex.Message));
            }
        }

        var backfilledCount = 0;
        foreach (var sessionId in syncedSessionIds)
        {
            try
            {
                backfilledCount += await BackfillMissingSessionSubmissionsFromPreviousAsync(sessionId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to backfill missing Supabase submissions for grading session {GradingSessionId}",
                    sessionId);
                failed.Add(new SyncSupabaseGradeItemFailure("(backfill)", ex.Message));
            }
        }

        return new SyncSupabaseGradesResponse(
            request.Submissions.Count,
            synced.Count,
            failed.Count,
            backfilledCount,
            synced,
            failed);
    }

    private async Task<string> GetClassNameAsync(string studentId, CancellationToken ct)
    {
        try
        {
            var normalizedStudentId = NormalizeSupabaseKey(studentId);

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
        string? gradingSessionId,
        CancellationToken ct)
    {
        var normalizedTermId = NormalizeNullableSupabaseKey(termId);
        var classStudentId = await GetRequiredClassStudentIdAsync(studentCode, className, normalizedTermId, ct);
        var sessionId = await GetRequiredGradingSessionIdAsync(
            labCode,
            className,
            normalizedTermId,
            gradingSessionId,
            ct);

        await CreateSessionSubmissionAsync(
            classStudentId,
            sessionId,
            score,
            details,
            sourceUrl,
            ct);

        return new SyncSupabaseGradeResponse(classStudentId, sessionId);
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

    private async Task<IReadOnlyList<SupabaseGradingSessionOptionDto>> GetGradingSessionOptionsAsync(
        string? termId,
        string? className,
        string? labCode,
        CancellationToken ct)
    {
        var url = "rest/v1/grading_sessions"
            + "?select=id,name,status,deadline,labs!inner(code,title),classes!inner(name,terms!inner(id,name))";

        if (!string.IsNullOrWhiteSpace(termId))
        {
            url += $"&classes.terms.id=eq.{EscapeFilterValue(termId)}";
        }
        if (!string.IsNullOrWhiteSpace(className))
        {
            url += $"&classes.name=eq.{EscapeFilterValue(className)}";
        }
        if (!string.IsNullOrWhiteSpace(labCode))
        {
            url += $"&labs.code=eq.{EscapeFilterValue(labCode)}";
        }

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddSupabaseAuthHeaders(request);

        var response = await _client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            throw new Exception($"Failed to query Supabase grading sessions: {response.StatusCode} - {errorContent}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return doc.RootElement.EnumerateArray()
            .Select(ParseGradingSessionOption)
            .Where(option => option is not null)
            .Select(option => option!)
            .OrderBy(option => option.TermCode ?? option.TermName ?? option.TermId)
            .ThenBy(option => option.ClassName)
            .ThenBy(option => option.LabCode)
            .ThenByDescending(option => option.Status == "open")
            .ThenBy(option => option.Name)
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

    private async Task<string> GetRequiredGradingSessionIdAsync(
        string labCode,
        string className,
        string? termId,
        string? gradingSessionId,
        CancellationToken ct)
    {
        var url = "rest/v1/grading_sessions"
            + "?select=id,labs!inner(code),"
            + (string.IsNullOrWhiteSpace(termId)
                ? "classes!inner(name)"
                : "classes!inner(name,terms!inner(id))")
            + "&status=eq.open"
            + $"&labs.code=eq.{EscapeFilterValue(labCode)}"
            + $"&classes.name=eq.{EscapeFilterValue(className)}"
            + "&limit=1";

        if (!string.IsNullOrWhiteSpace(gradingSessionId))
        {
            url += $"&id=eq.{EscapeFilterValue(gradingSessionId.Trim())}";
        }
        if (!string.IsNullOrWhiteSpace(termId))
        {
            url += $"&classes.terms.id=eq.{EscapeFilterValue(termId)}";
        }

        var element = await GetFirstArrayElementAsync(url, ct);
        if (element is null || !element.Value.TryGetProperty("id", out var idProp) || idProp.ValueKind != JsonValueKind.String)
        {
            var sessionDescription = string.IsNullOrWhiteSpace(gradingSessionId)
                ? "an open grading session"
                : $"open grading session '{gradingSessionId.Trim()}'";
            throw new NotFoundException($"Supabase {sessionDescription} not found for lab '{labCode}' in class '{className}'{FormatTermError(termId)}.");
        }

        return idProp.GetString()!;
    }

    private async Task CreateSessionSubmissionAsync(
        string classStudentId,
        string gradingSessionId,
        decimal score,
        object details,
        string? sourceUrl,
        CancellationToken ct)
    {
        var payload = new
        {
            p_class_student_id = classStudentId,
            p_grading_session_id = gradingSessionId,
            p_source_url = sourceUrl,
            p_score = score,
            p_details = details
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "rest/v1/rpc/create_session_submission")
        {
            Content = JsonContent.Create(payload)
        };
        AddSupabaseAuthHeaders(request);

        var response = await _client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            throw new BadRequestException($"Failed to create Supabase session_submission: {response.StatusCode} - {errorContent}");
        }
    }

    private async Task<int> BackfillMissingSessionSubmissionsFromPreviousAsync(
        string gradingSessionId,
        CancellationToken ct)
    {
        var payload = new
        {
            p_grading_session_id = gradingSessionId
        };

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "rest/v1/rpc/backfill_missing_session_submissions_from_previous")
        {
            Content = JsonContent.Create(payload)
        };
        AddSupabaseAuthHeaders(request);

        var response = await _client.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new BadRequestException(
                $"Failed to backfill missing Supabase session_submissions: {response.StatusCode} - {responseBody}");
        }

        return ParseRpcInteger(responseBody);
    }

    private static int ParseRpcInteger(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return 0;
        }

        using var doc = JsonDocument.Parse(responseBody);
        return doc.RootElement.ValueKind == JsonValueKind.Number &&
               doc.RootElement.TryGetInt32(out var value)
            ? value
            : 0;
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

    private static SupabaseGradingSessionOptionDto? ParseGradingSessionOption(JsonElement item)
    {
        if (!item.TryGetProperty("id", out var idProp) ||
            idProp.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(idProp.GetString()) ||
            !item.TryGetProperty("name", out var nameProp) ||
            nameProp.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(nameProp.GetString()) ||
            !item.TryGetProperty("status", out var statusProp) ||
            statusProp.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(statusProp.GetString()) ||
            !item.TryGetProperty("labs", out var labProp) ||
            labProp.ValueKind != JsonValueKind.Object ||
            !labProp.TryGetProperty("code", out var codeProp) ||
            codeProp.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(codeProp.GetString()))
        {
            return null;
        }

        if (!item.TryGetProperty("classes", out var classProp) ||
            classProp.ValueKind != JsonValueKind.Object ||
            !classProp.TryGetProperty("name", out var classNameProp) ||
            classNameProp.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(classNameProp.GetString()))
        {
            return null;
        }

        var term = ParseNestedTerm(classProp);

        DateTimeOffset? deadline = null;
        if (item.TryGetProperty("deadline", out var deadlineProp) &&
            deadlineProp.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(deadlineProp.GetString(), out var parsedDeadline))
        {
            deadline = parsedDeadline;
        }

        return new SupabaseGradingSessionOptionDto(
            idProp.GetString()!,
            nameProp.GetString()!,
            statusProp.GetString()!,
            deadline,
            classNameProp.GetString()!,
            codeProp.GetString()!,
            GetOptionalString(labProp, "title"),
            term?.Id,
            term?.Code,
            term?.Name);
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
