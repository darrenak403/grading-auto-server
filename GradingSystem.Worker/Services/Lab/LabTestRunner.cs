using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using GradingSystem.Domain.Entities;

namespace GradingSystem.Worker.Services.Lab;

public class LabTestRunner(IHttpClientFactory httpClientFactory, ILogger<LabTestRunner> logger)
{
    private static readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web);
    private static readonly Regex _variablePattern = new(@"\{\{(?<name>[^{}]+)\}\}", RegexOptions.Compiled);
    private sealed record SendResult(
        int StatusCode,
        string Body,
        string? InputJson,
        string? ErrorMessage,
        bool StudentApiUnavailable);

    private sealed record TestCaseRunOutcome(LabTestCaseResult Result, string? StopRemainingReason);

    public async Task<List<LabTestCaseResult>> RunAsync(
        string baseUrl, Guid jobId, IEnumerable<LabTestCase> testCases, CancellationToken ct)
    {
        var results = new List<LabTestCaseResult>();
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        var normalizedBaseUrl = baseUrl.TrimEnd('/');
        string? skipRemainingReason = null;

        foreach (var tc in testCases.OrderBy(t => t.Order).ThenBy(t => t.CreatedAt))
        {
            if (skipRemainingReason is not null)
            {
                var skippedUrl = BuildUrl(normalizedBaseUrl, tc.UrlTemplate, variables);
                var skippedResult = Fail(tc, jobId, skippedUrl, skipRemainingReason);
                results.Add(skippedResult);
                logger.LogInformation(
                    "Job {JobId} tc {TcId} ({Method} {Url}): skipped after previous API failure",
                    jobId, tc.Id, tc.HttpMethod, tc.UrlTemplate);
                continue;
            }

            var outcome = await RunSingleAsync(tc, normalizedBaseUrl, jobId, client, variables, ct);
            var result = outcome.Result;
            results.Add(result);

            if (outcome.StopRemainingReason is not null)
                skipRemainingReason = BuildSkipRemainingReason(outcome.StopRemainingReason);

            logger.LogInformation(
                "Job {JobId} tc {TcId} ({Method} {Url}): passed={Passed} status={Status}",
                jobId, tc.Id, tc.HttpMethod, tc.UrlTemplate, result.Passed, result.ActualStatusCode);
        }

        return results;
    }

    private async Task<TestCaseRunOutcome> RunSingleAsync(
        LabTestCase tc,
        string normalizedBaseUrl,
        Guid jobId,
        HttpClient client,
        Dictionary<string, string> variables,
        CancellationToken ct)
    {
        var resolvedUrlTemplate = ResolveTemplate(tc.UrlTemplate, variables);
        var resolvedInputJson = ResolveTemplate(tc.InputJson, variables);
        var resolvedHeadersJson = ResolveTemplate(tc.HeadersJson, variables);
        var url = normalizedBaseUrl + resolvedUrlTemplate;
        var method = new HttpMethod(tc.HttpMethod.ToUpperInvariant());
        var requiresAuthorization = HasAuthorizationHeader(resolvedHeadersJson);

        if (requiresAuthorization)
        {
            using var anonymousRequest = BuildRequest(method, url, resolvedInputJson, resolvedHeadersJson, includeAuthorization: false);

            try
            {
                var anonymousResponse = await client.SendAsync(anonymousRequest, ct);
                var anonymousStatus = (int)anonymousResponse.StatusCode;

                if (anonymousStatus is not 401 and not 403)
                {
                    var anonymousBody = await anonymousResponse.Content.ReadAsStringAsync(ct);
                    return new TestCaseRunOutcome(new LabTestCaseResult
                    {
                        LabGradingJobId = jobId,
                        LabTestCaseId = tc.Id,
                        Passed = false,
                        AwardedScore = 0,
                        ActualStatusCode = anonymousStatus,
                        ActualResponse = TrimResponse(anonymousBody),
                        ErrorMessage = $"Expected protected endpoint to reject anonymous request with 401 or 403, got {anonymousStatus}.",
                    }, null);
                }
            }
            catch (Exception ex) when (IsStudentApiUnavailable(ex, ct))
            {
                var message = $"Anonymous auth check HTTP error: {ex.Message}";
                return new TestCaseRunOutcome(Fail(tc, jobId, url, message), message);
            }
            catch (Exception ex)
            {
                return new TestCaseRunOutcome(
                    Fail(tc, jobId, url, $"Anonymous auth check HTTP error: {ex.Message}"),
                    null);
            }
        }

        var sendResult = await SendWithEnrollmentRetryAsync(
            client,
            method,
            url,
            resolvedInputJson,
            resolvedHeadersJson,
            ct);

        if (sendResult.ErrorMessage is not null)
        {
            return new TestCaseRunOutcome(
                Fail(tc, jobId, url, sendResult.ErrorMessage),
                sendResult.StudentApiUnavailable ? sendResult.ErrorMessage : null);
        }

        resolvedInputJson = sendResult.InputJson;
        var actualBody = sendResult.Body;
        var actualStatus = sendResult.StatusCode;
        bool statusPassed = actualStatus == tc.ExpectedStatusCode;

        bool bodyPassed = tc.MatchMode == LabTestCaseMatchMode.StatusOnly
            ? true
            : CheckBody(tc, actualBody);

        bool passed = statusPassed && bodyPassed;
        string? errorMessage = passed ? null : BuildErrorMessage(tc, actualStatus, statusPassed, bodyPassed);

        if (passed)
            TryCaptureVariable(tc.SaveTokenFrom, actualBody, variables);

        return new TestCaseRunOutcome(new LabTestCaseResult
        {
            LabGradingJobId = jobId,
            LabTestCaseId   = tc.Id,
            Passed          = passed,
            AwardedScore    = passed ? tc.Score : 0,
            ActualStatusCode = actualStatus,
            ActualResponse  = TrimResponse(actualBody),
            ErrorMessage    = errorMessage,
        }, null);
    }

    private static async Task<SendResult> SendWithEnrollmentRetryAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        string? inputJson,
        string? headersJson,
        CancellationToken ct)
    {
        const int maxDuplicateRetries = 50;

        for (var attempt = 0; attempt <= maxDuplicateRetries; attempt++)
        {
            using var request = BuildRequest(method, url, inputJson, headersJson, includeAuthorization: true);

            try
            {
                using var response = await client.SendAsync(request, ct);
                var body = await response.Content.ReadAsStringAsync(ct);
                var statusCode = (int)response.StatusCode;

                if (!ShouldRetryDuplicateEnrollment(method, url, statusCode, body, inputJson)
                    || !TryIncrementEnrollmentCourseId(inputJson, out var nextInputJson))
                {
                    return new SendResult(statusCode, body, inputJson, null, StudentApiUnavailable: false);
                }

                inputJson = nextInputJson;
            }
            catch (Exception ex) when (IsStudentApiUnavailable(ex, ct))
            {
                return new SendResult(0, string.Empty, inputJson, $"HTTP error: {ex.Message}", StudentApiUnavailable: true);
            }
            catch (Exception ex)
            {
                return new SendResult(0, string.Empty, inputJson, $"HTTP error: {ex.Message}", StudentApiUnavailable: false);
            }
        }

        return new SendResult(
            0,
            string.Empty,
            inputJson,
            "Could not find a non-duplicate enrollment body after 50 retries.",
            StudentApiUnavailable: false);
    }

    private static bool ShouldRetryDuplicateEnrollment(
        HttpMethod method,
        string url,
        int statusCode,
        string actualBody,
        string? inputJson)
    {
        return method == HttpMethod.Post
            && url.Contains("/enrollments", StringComparison.OrdinalIgnoreCase)
            && statusCode == 400
            && !string.IsNullOrWhiteSpace(inputJson)
            && actualBody.Contains("already enrolled", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryIncrementEnrollmentCourseId(string? inputJson, out string? nextInputJson)
    {
        nextInputJson = inputJson;
        if (string.IsNullOrWhiteSpace(inputJson)) return false;

        try
        {
            var node = JsonNode.Parse(inputJson) as JsonObject;
            if (node is null) return false;

            var key = node.ContainsKey("courseId") ? "courseId" :
                node.ContainsKey("CourseId") ? "CourseId" : null;
            if (key is null) return false;

            var current = node[key]?.GetValue<int>() ?? 0;
            if (current <= 0) return false;

            node[key] = current + 1;
            nextInputJson = node.ToJsonString(_jsonOpts);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static HttpRequestMessage BuildRequest(
        HttpMethod method,
        string url,
        string? inputJson,
        string? headersJson,
        bool includeAuthorization)
    {
        var request = new HttpRequestMessage(method, url);

        if (inputJson != null)
        {
            if (method == HttpMethod.Get || method == HttpMethod.Delete)
                request.RequestUri = new Uri(url + (url.Contains('?') ? "&" : "?") + JsonToQueryString(inputJson));
            else
                request.Content = new StringContent(inputJson, Encoding.UTF8, "application/json");
        }
        else if (method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch)
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        ApplyHeaders(request, headersJson, includeAuthorization);
        return request;
    }

    private static bool CheckBody(LabTestCase tc, string actualBody)
    {
        if (string.IsNullOrWhiteSpace(tc.ExpectJson)) return true;

        try
        {
            using var actualDoc   = JsonDocument.Parse(actualBody);
            using var expectedDoc = JsonDocument.Parse(tc.ExpectJson);

            return tc.MatchMode == LabTestCaseMatchMode.Exact
                ? JsonDeepEqual(actualDoc.RootElement, expectedDoc.RootElement)
                : JsonSubsetOf(actualDoc.RootElement, expectedDoc.RootElement);
        }
        catch
        {
            return false;
        }
    }

    // All keys/values in expected must be present (recursively) in actual; extra keys in actual are OK.
    private static bool JsonSubsetOf(JsonElement actual, JsonElement expected)
    {
        if (expected.ValueKind == JsonValueKind.Object)
        {
            if (actual.ValueKind != JsonValueKind.Object) return false;
            foreach (var prop in expected.EnumerateObject())
            {
                if (!actual.TryGetProperty(prop.Name, out var actualProp)) return false;
                if (!JsonSubsetOf(actualProp, prop.Value)) return false;
            }
            return true;
        }
        // Primitives and arrays: exact equality by raw text
        return actual.ToString() == expected.ToString();
    }

    private static bool JsonDeepEqual(JsonElement a, JsonElement b) =>
        a.ToString() == b.ToString();

    private static string? BuildErrorMessage(LabTestCase tc, int actual, bool statusOk, bool bodyOk)
    {
        if (!statusOk) return $"Expected status {tc.ExpectedStatusCode}, got {actual}.";
        if (!bodyOk)   return $"Body mismatch ({tc.MatchMode}).";
        return null;
    }

    private static string? ResolveTemplate(string? template, IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template) || variables.Count == 0) return template;

        return _variablePattern.Replace(template, match =>
        {
            var name = match.Groups["name"].Value.Trim();
            return variables.TryGetValue(name, out var value) ? value : match.Value;
        });
    }

    private static string BuildUrl(
        string normalizedBaseUrl,
        string urlTemplate,
        IReadOnlyDictionary<string, string> variables) =>
        normalizedBaseUrl + ResolveTemplate(urlTemplate, variables);

    private static string BuildSkipRemainingReason(string previousError) =>
        $"Skipped because a previous API request could not reach the student API: {previousError}";

    private static bool IsStudentApiUnavailable(Exception ex, CancellationToken ct) =>
        ex is HttpRequestException
        || ex is TimeoutException
        || (ex is TaskCanceledException && !ct.IsCancellationRequested);

    private static bool HasAuthorizationHeader(string? headersJson)
    {
        if (string.IsNullOrWhiteSpace(headersJson)) return false;

        try
        {
            using var doc = JsonDocument.Parse(headersJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;

            return doc.RootElement.EnumerateObject()
                .Any(prop => IsAuthorizationHeader(prop.Name));
        }
        catch
        {
            return false;
        }
    }

    private static void ApplyHeaders(HttpRequestMessage request, string? headersJson, bool includeAuthorization)
    {
        if (string.IsNullOrWhiteSpace(headersJson)) return;

        try
        {
            using var doc = JsonDocument.Parse(headersJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!includeAuthorization && IsAuthorizationHeader(prop.Name)) continue;

                var value = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString()
                    : prop.Value.GetRawText();

                if (string.IsNullOrWhiteSpace(value)) continue;

                if (request.Headers.TryAddWithoutValidation(prop.Name, value)) continue;

                request.Content ??= new StringContent(string.Empty);
                request.Content.Headers.TryAddWithoutValidation(prop.Name, value);
            }
        }
        catch
        {
            // Ignore malformed optional headers and keep executing the test case.
        }
    }

    private static void TryCaptureVariable(
        string? saveTokenFrom,
        string actualBody,
        IDictionary<string, string> variables)
    {
        if (string.IsNullOrWhiteSpace(saveTokenFrom) || string.IsNullOrWhiteSpace(actualBody)) return;

        try
        {
            using var doc = JsonDocument.Parse(actualBody);
            foreach (var jsonPath in SplitCapturePaths(saveTokenFrom))
            {
                if (!TryResolveJsonPath(doc.RootElement, jsonPath, out var valueElement)) continue;

                var variableName = InferVariableName(jsonPath);
                var variableValue = valueElement.ValueKind == JsonValueKind.String
                    ? valueElement.GetString()
                    : valueElement.GetRawText();

                if (string.IsNullOrWhiteSpace(variableName) || string.IsNullOrWhiteSpace(variableValue)) continue;
                variables[variableName] = variableValue;
                SaveVariableAliases(variableName, variableValue, variables);
            }
        }
        catch
        {
            // Ignore extraction failures and continue grading the remaining test cases.
        }
    }

    private static IEnumerable<string> SplitCapturePaths(string saveTokenFrom) =>
        saveTokenFrom
            .Split([';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(path => !string.IsNullOrWhiteSpace(path));

    private static void SaveVariableAliases(
        string variableName,
        string variableValue,
        IDictionary<string, string> variables)
    {
        if (variableName.Equals("accessToken", StringComparison.OrdinalIgnoreCase))
            variables["token"] = variableValue;

        if (variableName.Equals("token", StringComparison.OrdinalIgnoreCase))
            variables["accessToken"] = variableValue;
    }

    private static string InferVariableName(string jsonPath)
    {
        var path = jsonPath.Trim();
        if (path.StartsWith("$.")) path = path[2..];
        else if (path.StartsWith("$")) path = path[1..];

        var name = path.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
        var bracketIndex = name.IndexOf('[');
        if (bracketIndex >= 0)
            name = name[..bracketIndex];

        return name.Trim();
    }

    private static bool TryResolveJsonPath(JsonElement root, string jsonPath, out JsonElement value)
    {
        value = root;
        if (string.IsNullOrWhiteSpace(jsonPath)) return false;

        var path = jsonPath.Trim();
        if (path == "$") return true;
        if (path.StartsWith("$.")) path = path[2..];
        else if (path.StartsWith("$")) path = path[1..];

        if (string.IsNullOrWhiteSpace(path)) return true;

        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TryResolveSegment(value, segment, out value))
                return false;
        }

        return true;
    }

    private static bool TryResolveSegment(JsonElement current, string segment, out JsonElement value)
    {
        value = current;
        if (string.IsNullOrWhiteSpace(segment)) return false;

        var remainder = segment;
        var bracketIndex = remainder.IndexOf('[');
        var propertyName = bracketIndex >= 0 ? remainder[..bracketIndex] : remainder;

        if (!string.IsNullOrEmpty(propertyName))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(propertyName, out value))
                return false;
        }

        remainder = bracketIndex >= 0 ? remainder[bracketIndex..] : string.Empty;
        while (!string.IsNullOrEmpty(remainder))
        {
            if (remainder[0] != '[') return false;
            var end = remainder.IndexOf(']');
            if (end <= 1) return false;

            var indexText = remainder[1..end];
            if (!int.TryParse(indexText, out var index) || value.ValueKind != JsonValueKind.Array)
                return false;

            var arrayValues = value.EnumerateArray().ToList();
            if (index < 0 || index >= arrayValues.Count) return false;
            value = arrayValues[index];
            remainder = remainder[(end + 1)..];
        }

        return true;
    }

    private static string JsonToQueryString(string inputJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(inputJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return string.Empty;
            return string.Join("&", doc.RootElement.EnumerateObject().Select(p =>
                Uri.EscapeDataString(p.Name) + "=" + Uri.EscapeDataString(p.Value.ToString())));
        }
        catch { return string.Empty; }
    }

    private static string TrimResponse(string response) =>
        response.Length > 1000 ? response[..1000] : response;

    private static bool IsAuthorizationHeader(string name) =>
        name.Equals("Authorization", StringComparison.OrdinalIgnoreCase);

    private static LabTestCaseResult Fail(LabTestCase tc, Guid jobId, string url, string message) => new()
    {
        LabGradingJobId  = jobId,
        LabTestCaseId    = tc.Id,
        Passed           = false,
        AwardedScore     = 0,
        ActualStatusCode = 0,
        ActualResponse   = url,
        ErrorMessage     = message,
    };
}
