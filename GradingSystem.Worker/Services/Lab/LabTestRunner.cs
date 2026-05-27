using System.Text;
using System.Text.Json;
using GradingSystem.Domain.Entities;

namespace GradingSystem.Worker.Services.Lab;

public class LabTestRunner(IHttpClientFactory httpClientFactory, ILogger<LabTestRunner> logger)
{
    private static readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<List<LabTestCaseResult>> RunAsync(
        string baseUrl, Guid jobId, IEnumerable<LabTestCase> testCases, CancellationToken ct)
    {
        var results = new List<LabTestCaseResult>();
        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        foreach (var tc in testCases.OrderBy(t => t.Order).ThenBy(t => t.CreatedAt))
        {
            var result = await RunSingleAsync(tc, baseUrl, jobId, client, ct);
            results.Add(result);
            logger.LogInformation(
                "Job {JobId} tc {TcId} ({Method} {Url}): passed={Passed} status={Status}",
                jobId, tc.Id, tc.HttpMethod, tc.UrlTemplate, result.Passed, result.ActualStatusCode);
        }

        return results;
    }

    private async Task<LabTestCaseResult> RunSingleAsync(
        LabTestCase tc, string baseUrl, Guid jobId, HttpClient client, CancellationToken ct)
    {
        var url = baseUrl.TrimEnd('/') + tc.UrlTemplate;
        var method = new HttpMethod(tc.HttpMethod.ToUpperInvariant());
        var request = new HttpRequestMessage(method, url);

        if (tc.InputJson != null)
        {
            if (method == HttpMethod.Get || method == HttpMethod.Delete)
                request.RequestUri = new Uri(url + (url.Contains('?') ? "&" : "?") + JsonToQueryString(tc.InputJson));
            else
                request.Content = new StringContent(tc.InputJson, Encoding.UTF8, "application/json");
        }
        else if (method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch)
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        HttpResponseMessage? response = null;
        string actualBody = string.Empty;

        try
        {
            response = await client.SendAsync(request, ct);
            actualBody = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            return Fail(tc, jobId, url, $"HTTP error: {ex.Message}");
        }

        var actualStatus = (int)response.StatusCode;
        bool statusPassed = actualStatus == tc.ExpectedStatusCode;

        bool bodyPassed = tc.MatchMode == LabTestCaseMatchMode.StatusOnly
            ? true
            : CheckBody(tc, actualBody);

        bool passed = statusPassed && bodyPassed;
        string? errorMessage = passed ? null : BuildErrorMessage(tc, actualStatus, statusPassed, bodyPassed);

        return new LabTestCaseResult
        {
            LabGradingJobId = jobId,
            LabTestCaseId   = tc.Id,
            Passed          = passed,
            AwardedScore    = passed ? tc.Score : 0,
            ActualStatusCode = actualStatus,
            ActualResponse  = actualBody.Length > 1000 ? actualBody[..1000] : actualBody,
            ErrorMessage    = errorMessage,
        };
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
