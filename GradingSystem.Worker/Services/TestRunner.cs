using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GradingSystem.Application.Common;
using GradingSystem.Application.Interfaces;
using GradingSystem.Domain.Entities;
using GradingSystem.Worker.Options;
using HtmlAgilityPack;
using Microsoft.Playwright;
using Microsoft.Extensions.Options;

namespace GradingSystem.Worker.Services;

public class TestRunner(
    ILogger<TestRunner> logger,
    IOptions<WorkerOptions> workerOpts,
    IOptions<PlaywrightOptions> playwrightOpts)
{
    private static readonly JsonSerializerOptions _jsonOpts =
        new(JsonSerializerDefaults.Web); // PropertyNameCaseInsensitive = true, no AOT issue

    private readonly string _bindHost = workerOpts.Value.BindHost;
    private readonly PlaywrightOptions _playwright = playwrightOpts.Value;

    public virtual async Task RunAsync(GradingJob job, StudentContext ctx, IUnitOfWork uow, CancellationToken ct)
    {
        var questions = (await uow.Questions.FindAsync(q => q.AssignmentId == job.Submission.AssignmentId))
                        .OrderBy(q => q.CreatedAt).ToList();

        var handler = new HttpClientHandler
        {
            CookieContainer = new System.Net.CookieContainer(),
            AllowAutoRedirect = false,
        };
        using var client = new HttpClient(handler);

        foreach (var question in questions)
        {
            if (!ctx.QuestionApps.TryGetValue(question.Id, out var app))
            {
                logger.LogWarning("No running app for question {QId} — skipping", question.Id);
                continue;
            }

            var testCases = (await uow.TestCases.FindAsync(tc => tc.QuestionId == question.Id))
                            .OrderBy(tc => tc.Order).ThenBy(tc => tc.CreatedAt).ToList();

            List<TestCaseResult> details;

            if (app.GivenUrlInvalid || app.NotSubmitted)
            {
                // Student used wrong GivenApiBaseUrl, or this question's folder was missing from
                // the artifact entirely → zero score for all test cases
                var failReason = app.GivenUrlInvalid ? app.GivenUrlInvalidReason : app.NotSubmittedReason;
                details = testCases.Select(tc => new TestCaseResult
                {
                    TestCaseId = tc.Id,
                    Pass = false,
                    AwardedScore = 0,
                    HttpMethod = tc.HttpMethod,
                    Url = tc.UrlTemplate,
                    ActualStatus = 0,
                    FailReason = failReason,
                }).ToList();
            }
            else if (question.Type == QuestionType.Api)
                details = await RunApiCasesAsync(testCases, app.Port, client, ct);
            else
                details = await RunPlaywrightCasesAsync(testCases, app.Port, ct);

            decimal totalScore = details.Sum(r => r.AwardedScore);

            await uow.QuestionResults.AddAsync(new QuestionResult
            {
                SubmissionId = job.SubmissionId,
                GradingJobId = job.Id,
                QuestionId   = question.Id,
                Score        = totalScore,
                MaxScore     = question.MaxScore,
                Detail       = JsonSerializer.Serialize(details),
            });

            logger.LogInformation("Question {QuestionId}: {Score}/{Max}", question.Id, totalScore, question.MaxScore);
        }

        await uow.SaveChangesAsync(ct);
    }

    // ── Q1: API question — direct HTTP runner for HTTP test cases, swagger for schema-only cases ──

    private async Task<List<TestCaseResult>> RunApiCasesAsync(
        List<TestCase> testCases, int port, HttpClient client, CancellationToken ct)
    {
        var swaggerUrl = $"http://{_bindHost}:{port}/swagger/v1/swagger.json";
        JsonDocument? swaggerDoc = null;
        string? fetchError = null;

        try
        {
            var resp = await client.GetAsync(swaggerUrl, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (resp.IsSuccessStatusCode)
                swaggerDoc = JsonDocument.Parse(body);
            else
                fetchError = $"swagger.json returned HTTP {(int)resp.StatusCode}";
        }
        catch (Exception ex)
        {
            fetchError = $"Failed to fetch swagger.json: {ex.Message}";
        }

        logger.LogInformation("Swagger fetch {Url}: {Result}", swaggerUrl,
            swaggerDoc != null ? "OK" : fetchError);

        var results = new List<TestCaseResult>(testCases.Count);

        foreach (var tc in testCases)
        {
            var expect = DeserializeExpect(tc.ExpectJson);
            bool isHttpCase = expect.Status != null || tc.InputJson != null;

            if (isHttpCase)
                results.Add(await RunHttpTestCaseAsync(tc, port, client, ct));
            else if (swaggerDoc != null)
                results.Add(EvaluateSwaggerCase(tc, swaggerDoc, swaggerUrl));
            else
                results.Add(FailResult(tc, swaggerUrl, fetchError!));
        }

        return results;
    }

    // ── Swagger schema-only cases ──

    private static TestCaseResult EvaluateSwaggerCase(TestCase tc, JsonDocument swagger, string swaggerUrl)
    {
        var expect = DeserializeExpect(tc.ExpectJson);
        var root = swagger.RootElement;

        if (!root.TryGetProperty("paths", out var paths))
            return FailResult(tc, swaggerUrl, "swagger.json has no 'paths'");

        if (!paths.TryGetProperty(tc.UrlTemplate, out var pathItem))
            return FailResult(tc, swaggerUrl, $"Path '{tc.UrlTemplate}' not found in swagger");

        var methodKey = tc.HttpMethod.ToLowerInvariant();
        if (!pathItem.TryGetProperty(methodKey, out var operation))
            return FailResult(tc, swaggerUrl, $"Method '{tc.HttpMethod}' not found for path '{tc.UrlTemplate}'");

        if (expect.Fields is { Count: > 0 })
        {
            var schemaError = CheckResponseSchema(root, operation, expect.Fields);
            if (schemaError != null)
                return FailResult(tc, swaggerUrl, schemaError);
        }

        return new TestCaseResult
        {
            TestCaseId = tc.Id,
            Pass = true,
            AwardedScore = tc.Score,
            HttpMethod = tc.HttpMethod,
            Url = $"{swaggerUrl} — {tc.HttpMethod} {tc.UrlTemplate}",
            ActualStatus = 200,
        };
    }

    private static string? CheckResponseSchema(JsonElement root, JsonElement operation, List<string> fields)
    {
        if (!operation.TryGetProperty("responses", out var responses)) return null;
        if (!responses.TryGetProperty("200", out var resp200)) return null;
        if (!resp200.TryGetProperty("content", out var content)) return null;

        JsonElement schema = default;
        bool found = false;

        foreach (var mediaType in content.EnumerateObject())
        {
            if (mediaType.Value.TryGetProperty("schema", out schema))
            {
                found = true;
                break;
            }
        }

        if (!found) return null;

        schema = ResolveRef(root, schema);

        if (schema.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "array"
            && schema.TryGetProperty("items", out var items))
        {
            schema = ResolveRef(root, items);
        }

        if (!schema.TryGetProperty("properties", out var props)) return null;

        var missing = fields.Where(f => !props.TryGetProperty(f, out _)).ToList();
        return missing.Count > 0
            ? $"Response schema missing properties: {string.Join(", ", missing)}"
            : null;
    }

    private static JsonElement ResolveRef(JsonElement root, JsonElement schema)
    {
        if (!schema.TryGetProperty("$ref", out var refEl)) return schema;

        var parts = (refEl.GetString() ?? "").TrimStart('#', '/').Split('/');
        var current = root;

        foreach (var part in parts)
        {
            if (!current.TryGetProperty(part, out current)) return schema;
        }

        return current;
    }

    private async Task<TestCaseResult> RunHttpTestCaseAsync(
        TestCase tc, int port, HttpClient client, CancellationToken ct)
    {
        var url = $"http://{_bindHost}:{port}{tc.UrlTemplate}";
        var method = new HttpMethod(tc.HttpMethod.ToUpper());
        var request = new HttpRequestMessage(method, url);

        if (tc.InputJson != null)
        {
            if (method == HttpMethod.Get || method == HttpMethod.Delete)
                request.RequestUri = new Uri(url + "?" + JsonToQueryString(tc.InputJson));
            else
                request.Content = new StringContent(tc.InputJson, Encoding.UTF8, "application/json");
        }
        else if (method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch)
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        HttpResponseMessage? response = null;
        string body = string.Empty;

        try
        {
            response = await client.SendAsync(request, ct);
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            return FailResult(tc, url, $"Exception: {ex.Message}");
        }

        var actualStatus = (int)response.StatusCode;
        var contentType = response.Content.Headers.ContentType?.MediaType;
        var isJson = contentType == "application/json";
        var isHtml = contentType?.Contains("text/html") == true;

        var expect = DeserializeExpect(tc.ExpectJson);

        var (failReason, scoreRatio) = EvaluateHttp(expect, actualStatus, body, isJson, isHtml);
        bool pass = failReason == null;
        var awardedScore = scoreRatio >= 1m ? tc.Score : Math.Round(tc.Score * scoreRatio, 2);

        return new TestCaseResult
        {
            TestCaseId = tc.Id,
            Pass = pass,
            AwardedScore = awardedScore,
            HttpMethod = tc.HttpMethod,
            Url = url,
            ActualStatus = actualStatus,
            ActualBody = body.Length > 500 ? body[..500] : body,
            FailReason = failReason,
        };
    }

    // Status/shape checks (status code, content-type, IsArray, Fields, HTML assertions) remain
    // all-or-nothing gates — a mismatch there means the wrong endpoint/shape was hit entirely.
    // A JSON Body mismatch, however, is graded proportionally: correct status + a partially-off
    // body should still earn partial credit rather than losing the whole test case's points.
    private static (string? FailReason, decimal ScoreRatio) EvaluateHttp(
        ExpectJson expect, int actualStatus, string body, bool isJson, bool isHtml)
    {
        if (expect.Status != null && actualStatus != expect.Status)
            return ($"Expected status {expect.Status}, got {actualStatus}", 0m);

        bool expectsBody = expect.Body.HasValue
            && expect.Body.Value.ValueKind != JsonValueKind.Undefined
            && expect.Body.Value.ValueKind != JsonValueKind.Null;

        if (expectsBody && !isJson)
            return ("Expected JSON body but response Content-Type is not application/json", 0m);

        decimal scoreRatio = 1m;
        string? note = null;

        if (isJson)
        {
            JsonElement root;
            try { root = JsonDocument.Parse(body).RootElement; }
            catch { return ("Response is not valid JSON", 0m); }

            if (expectsBody)
            {
                var (matched, total) = JsonSubsetScore(root, expect.Body!.Value);
                scoreRatio = total == 0 ? 1m : (decimal)matched / total;
                if (scoreRatio < 1m)
                    note = $"Response body partially matches expected body ({matched}/{total} fields correct)";
            }

            if (expect.IsArray != null)
            {
                bool actualIsArray = root.ValueKind == JsonValueKind.Array;
                if (actualIsArray != expect.IsArray)
                    return ($"Expected isArray={expect.IsArray}, got {(actualIsArray ? "array" : "object")}", 0m);
            }

            if (expect.Fields != null)
            {
                var target = root.ValueKind == JsonValueKind.Array ? root[0] : root;
                var missing = expect.Fields.Where(f => !target.TryGetProperty(f, out _)).ToList();
                if (missing.Count > 0)
                    return ($"Missing fields: {string.Join(", ", missing)}", 0m);
            }
        }

        if (isHtml)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(body);

            if (expect.Value != null && !body.Contains(expect.Value, StringComparison.OrdinalIgnoreCase))
                return ($"Value '{expect.Value}' not found in response", 0m);

            // id-based element check (new, replaces/supplements selector)
            if (expect.ElementId != null)
            {
                var node = doc.GetElementbyId(expect.ElementId);
                if (node == null)
                    return ($"Element with id='{expect.ElementId}' not found", 0m);

                if (expect.ElementText != null
                    && !node.InnerText.Contains(expect.ElementText, StringComparison.OrdinalIgnoreCase))
                    return ($"Element id='{expect.ElementId}' does not contain text '{expect.ElementText}'", 0m);
            }

            if (expect.Selector != null)
            {
                var xpath = expect.Selector.StartsWith('/')
                    ? expect.Selector
                    : "//" + expect.Selector.Trim().Replace(" ", "//");
                var nodes = doc.DocumentNode.SelectNodes(xpath);

                if (expect.SelectorMinCount != null)
                {
                    if (nodes == null || nodes.Count < expect.SelectorMinCount)
                        return ($"Selector '{expect.Selector}' matched {nodes?.Count ?? 0}, expected >= {expect.SelectorMinCount}", 0m);
                }
                else if (nodes == null || nodes.Count == 0)
                {
                    return ($"Selector '{expect.Selector}' not found", 0m);
                }

                if (expect.SelectorText != null)
                {
                    var node = doc.DocumentNode.SelectSingleNode(xpath);
                    if (node?.InnerText.Contains(expect.SelectorText, StringComparison.OrdinalIgnoreCase) != true)
                        return ($"SelectorText '{expect.SelectorText}' not found in element", 0m);
                }
            }
        }

        return (note, scoreRatio);
    }

    // ── Q2: Playwright runner ──

    private string PlaywrightTestHost =>
        string.IsNullOrWhiteSpace(_playwright.BrowserCdpEndpoint)
            ? _bindHost
            : _playwright.ArtifactHostFromBrowser;

    private async Task<List<TestCaseResult>> RunPlaywrightCasesAsync(
        List<TestCase> testCases, int port, CancellationToken ct)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await ConnectOrLaunchBrowserAsync(playwright);
        await using var browserContext = await browser.NewContextAsync();
        var apiContext = browserContext.APIRequest;

        var context = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<TestCaseResult>(testCases.Count);

        foreach (var tc in testCases)
            results.Add(await RunPlaywrightTestCaseAsync(tc, port, browserContext, apiContext, context, ct));

        return results;
    }

    private async Task<IBrowser> ConnectOrLaunchBrowserAsync(IPlaywright playwright)
    {
        var cdp = _playwright.BrowserCdpEndpoint?.Trim();
        if (!string.IsNullOrEmpty(cdp))
        {
            logger.LogInformation("Playwright: connecting over CDP to {Endpoint}", cdp);
            return await playwright.Chromium.ConnectOverCDPAsync(cdp);
        }

        return await playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    private async Task<TestCaseResult> RunPlaywrightTestCaseAsync(
        TestCase tc, int port,
        IBrowserContext browserContext, IAPIRequestContext apiContext,
        Dictionary<string, string> context, CancellationToken ct)
    {
        var urlPath = InterpolateVariables(tc.UrlTemplate, context);
        var url = $"http://{PlaywrightTestHost}:{port}{urlPath}";
        var inputJson = tc.InputJson != null ? InterpolateVariables(tc.InputJson, context) : null;

        var expect = DeserializeExpect(tc.ExpectJson);
        int actualStatus = 0;
        string body = string.Empty;
        IPage? page = null;

        try
        {
            var method = tc.HttpMethod.ToUpperInvariant();

            if (method == "GET")
            {
                var fullUrl = url;
                if (inputJson != null)
                {
                    var qs = JsonToQueryString(inputJson);
                    if (!string.IsNullOrEmpty(qs))
                        fullUrl = url.Contains('?') ? url + "&" + qs : url + "?" + qs;
                }
                page = await browserContext.NewPageAsync();
                var response = await page.GotoAsync(fullUrl,
                    new() { WaitUntil = WaitUntilState.DOMContentLoaded });
                actualStatus = response?.Status ?? 0;
                body = await page.ContentAsync();
            }
            else
            {
                var options = new APIRequestContextOptions
                {
                    Headers = new Dictionary<string, string>
                        { ["Content-Type"] = "application/json" },
                    DataString = inputJson ?? "{}",
                };
                IAPIResponse response = method switch
                {
                    "POST"   => await apiContext.PostAsync(url, options),
                    "PUT"    => await apiContext.PutAsync(url, options),
                    "PATCH"  => await apiContext.PatchAsync(url, options),
                    "DELETE" => await apiContext.DeleteAsync(url, options),
                    _        => await apiContext.GetAsync(url, options),
                };
                actualStatus = response.Status;
                body = await response.TextAsync();

                var contentType = response.Headers.GetValueOrDefault("content-type", "");
                if (contentType.Contains("text/html"))
                {
                    page = await browserContext.NewPageAsync();
                    await page.SetContentAsync(body);
                }
            }
        }
        catch (Exception ex)
        {
            return FailResult(tc, url, $"Playwright exception: {ex.Message}");
        }

        string? failReason = null;
        decimal scoreRatio = 1m;

        if (expect.Status != null && actualStatus != expect.Status)
        {
            failReason = $"Expected status {expect.Status}, got {actualStatus}";
            scoreRatio = 0m;
        }

        if (failReason == null && page != null)
        {
            failReason = await EvaluatePlaywrightAsync(expect, page);
            if (failReason != null) scoreRatio = 0m;
        }

        if (failReason == null && page == null && !string.IsNullOrEmpty(body))
            (failReason, scoreRatio) = EvaluateJsonBody(expect, body);

        if (!string.IsNullOrEmpty(body))
            ExtractVariables(body, expect.Extract, context);

        string? screenshotBase64 = null;
        if (page != null)
        {
            try
            {
                var png = await page.ScreenshotAsync(new() { FullPage = true });
                screenshotBase64 = Convert.ToBase64String(png);
            }
            catch { /* screenshot is best-effort */ }

            await page.CloseAsync();
        }

        bool pass = failReason == null;
        var awardedScore = scoreRatio >= 1m ? tc.Score : Math.Round(tc.Score * scoreRatio, 2);
        return new TestCaseResult
        {
            TestCaseId      = tc.Id,
            Pass            = pass,
            AwardedScore    = awardedScore,
            HttpMethod      = tc.HttpMethod,
            Url             = url,
            ActualStatus    = actualStatus,
            ActualBody      = body.Length > 500 ? body[..500] : body,
            FailReason      = failReason,
            ScreenshotBase64 = screenshotBase64,
        };
    }

    private static async Task<string?> EvaluatePlaywrightAsync(ExpectJson expect, IPage page)
    {
        if (expect.Value != null)
        {
            var content = await page.ContentAsync();
            if (!content.Contains(expect.Value, StringComparison.OrdinalIgnoreCase))
                return $"Value '{expect.Value}' not found in response";
        }

        if (expect.ElementId != null)
        {
            var loc = page.Locator($"#{expect.ElementId}");
            if (await loc.CountAsync() == 0)
                return $"Element with id='{expect.ElementId}' not found";
            if (expect.ElementText != null)
            {
                var text = await loc.InnerTextAsync();
                if (!text.Contains(expect.ElementText, StringComparison.OrdinalIgnoreCase))
                    return $"Element id='{expect.ElementId}' does not contain '{expect.ElementText}'";
            }
        }

        if (expect.Selector != null)
        {
            var loc = page.Locator(expect.Selector);
            var count = await loc.CountAsync();
            if (expect.SelectorMinCount != null)
            {
                if (count < expect.SelectorMinCount)
                    return $"Selector '{expect.Selector}' matched {count}, expected >= {expect.SelectorMinCount}";
            }
            else if (count == 0)
                return $"Selector '{expect.Selector}' not found";

            if (expect.SelectorText != null)
            {
                var text = await loc.First.InnerTextAsync();
                if (!text.Contains(expect.SelectorText, StringComparison.OrdinalIgnoreCase))
                    return $"SelectorText '{expect.SelectorText}' not found in element";
            }
        }

        return null;
    }

    private static (string? FailReason, decimal ScoreRatio) EvaluateJsonBody(ExpectJson expect, string body)
    {
        JsonElement root;
        try { root = JsonDocument.Parse(body).RootElement; }
        catch { return ("Response is not valid JSON", 0m); }

        bool expectsBody = expect.Body.HasValue
            && expect.Body.Value.ValueKind != JsonValueKind.Undefined
            && expect.Body.Value.ValueKind != JsonValueKind.Null;

        decimal scoreRatio = 1m;
        string? note = null;

        if (expectsBody)
        {
            var (matched, total) = JsonSubsetScore(root, expect.Body!.Value);
            scoreRatio = total == 0 ? 1m : (decimal)matched / total;
            if (scoreRatio < 1m)
                note = $"Response body partially matches expected body ({matched}/{total} fields correct)";
        }

        if (expect.IsArray != null)
        {
            bool actualIsArray = root.ValueKind == JsonValueKind.Array;
            if (actualIsArray != expect.IsArray)
                return ($"Expected isArray={expect.IsArray}, got {(actualIsArray ? "array" : "object")}", 0m);
        }

        if (expect.Fields != null)
        {
            var target = root.ValueKind == JsonValueKind.Array ? root[0] : root;
            var missing = expect.Fields.Where(f => !target.TryGetProperty(f, out _)).ToList();
            if (missing.Count > 0)
                return ($"Missing fields: {string.Join(", ", missing)}", 0m);
        }

        return (note, scoreRatio);
    }

    // Counts how many of the leaf values in `expected` are present with a matching value
    // (recursively) somewhere in `actual`; `total` is the number of leaves in `expected`. Used to
    // award partial credit for a body that's mostly-but-not-exactly right, instead of an
    // all-or-nothing match: a correct HTTP status with a slightly-off body should cost points only
    // for the mismatched parts, not the whole test case. Extra keys in `actual` are tolerated.
    // Arrays are matched positionally (same index compared recursively); a missing/extra-length
    // array counts every expected element in that array as unmatched rather than failing outright.
    private static (int Matched, int Total) JsonSubsetScore(JsonElement actual, JsonElement expected)
    {
        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                if (actual.ValueKind != JsonValueKind.Object) return (0, CountLeaves(expected));
                int matched = 0, total = 0;
                foreach (var prop in expected.EnumerateObject())
                {
                    if (actual.TryGetProperty(prop.Name, out var actualProp))
                    {
                        var (m, t) = JsonSubsetScore(actualProp, prop.Value);
                        matched += m;
                        total += t;
                    }
                    else
                    {
                        total += CountLeaves(prop.Value);
                    }
                }
                return (matched, total);

            case JsonValueKind.Array:
                if (actual.ValueKind != JsonValueKind.Array) return (0, CountLeaves(expected));
                var expectedItems = expected.EnumerateArray().ToList();
                var actualItems = actual.EnumerateArray().ToList();
                int arrMatched = 0, arrTotal = 0;
                for (int i = 0; i < expectedItems.Count; i++)
                {
                    if (i < actualItems.Count)
                    {
                        var (m, t) = JsonSubsetScore(actualItems[i], expectedItems[i]);
                        arrMatched += m;
                        arrTotal += t;
                    }
                    else
                    {
                        arrTotal += CountLeaves(expectedItems[i]);
                    }
                }
                return (arrMatched, arrTotal);

            default:
                return (actual.ToString() == expected.ToString() ? 1 : 0, 1);
        }
    }

    private static int CountLeaves(JsonElement expected) => expected.ValueKind switch
    {
        JsonValueKind.Object => expected.EnumerateObject().Sum(p => CountLeaves(p.Value)),
        JsonValueKind.Array  => expected.EnumerateArray().Sum(CountLeaves),
        _ => 1,
    };

    private static string InterpolateVariables(string template, Dictionary<string, string> ctx)
    {
        foreach (var (key, value) in ctx)
            template = template.Replace($"{{{{{key}}}}}", value);
        return template;
    }

    private static void ExtractVariables(string body, Dictionary<string, string>? extract,
        Dictionary<string, string> ctx)
    {
        if (extract == null || string.IsNullOrWhiteSpace(body)) return;
        try
        {
            var doc = JsonDocument.Parse(body).RootElement;
            foreach (var (varName, path) in extract)
            {
                var node = doc;
                foreach (var part in path.TrimStart('$', '.').Split('.'))
                    if (!node.TryGetProperty(part, out node)) goto next;
                ctx[varName] = node.ToString();
                next:;
            }
        }
        catch { }
    }

    // ── Helpers ──

    private static TestCaseResult FailResult(TestCase tc, string url, string reason) => new()
    {
        TestCaseId = tc.Id,
        Pass = false,
        AwardedScore = 0,
        HttpMethod = tc.HttpMethod,
        Url = url,
        ActualStatus = 0,
        FailReason = reason,
    };

    private static ExpectJson DeserializeExpect(string expectJson) =>
        JsonSerializer.Deserialize<ExpectJson>(expectJson, _jsonOpts)!;

    private static string JsonToQueryString(string inputJson)
    {
        var node = JsonNode.Parse(inputJson)?.AsObject();
        if (node == null) return string.Empty;
        return string.Join("&", node.Select(kv =>
            Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value?.ToString() ?? "")));
    }

}
