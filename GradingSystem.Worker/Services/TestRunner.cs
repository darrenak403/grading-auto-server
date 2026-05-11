using System.Diagnostics;
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

public class TestRunner(ILogger<TestRunner> logger, IOptions<WorkerOptions> workerOpts)
{
    private static readonly JsonSerializerOptions _jsonOpts =
        new(JsonSerializerDefaults.Web); // PropertyNameCaseInsensitive = true, no AOT issue

    private readonly NewmanLaunch? _newman = ResolveNewman(workerOpts.Value, logger);
    private readonly string _bindHost = workerOpts.Value.BindHost;

    public async Task RunAsync(GradingJob job, StudentContext ctx, IUnitOfWork uow, CancellationToken ct)
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

            if (app.GivenUrlInvalid)
            {
                // Student used wrong GivenApiBaseUrl → zero score for all test cases
                details = testCases.Select(tc => new TestCaseResult
                {
                    TestCaseId = tc.Id,
                    Pass = false,
                    AwardedScore = 0,
                    HttpMethod = tc.HttpMethod,
                    Url = tc.UrlTemplate,
                    ActualStatus = 0,
                    FailReason = app.GivenUrlInvalidReason,
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

    // ── Q1: API question — newman for HTTP test cases, swagger for schema-only cases ──

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

        // Prefer direct HTTP runner for all HTTP cases (including expected body checks).
        // Keep swagger-only checks separate.
        var newmanCases = new List<TestCase>();
        var directCases = new List<TestCase>();

        foreach (var tc in testCases)
        {
            var expect = DeserializeExpect(tc.ExpectJson);
            bool hasBody = expect.Body.HasValue && expect.Body.Value.ValueKind != JsonValueKind.Undefined
                           && expect.Body.Value.ValueKind != JsonValueKind.Null;
            bool isHttpCase = expect.Status != null || tc.InputJson != null || hasBody;

            if (isHttpCase)
                directCases.Add(tc);
            else
                directCases.Add(tc); // swagger-only
        }

        var results = new List<TestCaseResult>(testCases.Count);

        // Run newman cases
        if (newmanCases.Count > 0)
        {
            var newmanResults = await RunNewmanCasesAsync(newmanCases, port, _bindHost, ct);
            results.AddRange(newmanResults);
        }

        // Run direct / swagger cases
        foreach (var tc in directCases)
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

    // ── Newman runner ──

    private async Task<List<TestCaseResult>> RunNewmanCasesAsync(
        List<TestCase> testCases, int port, string bindHost, CancellationToken ct)
    {
        var collectionPath = Path.Combine(Path.GetTempPath(), $"newman-col-{Guid.NewGuid():N}.json");
        var reportPath     = Path.Combine(Path.GetTempPath(), $"newman-rep-{Guid.NewGuid():N}.json");

        try
        {
            if (_newman is null)
            {
                const string hint =
                    "Newman CLI not found. Install: npm install -g newman, ensure Node is on PATH, or set Worker:NewmanExecutable to the full path of newman.cmd.";
                return testCases.Select(tc =>
                        FailResult(tc, $"http://{bindHost}:{port}{tc.UrlTemplate}", hint))
                    .ToList();
            }

            var collection = BuildPostmanCollection(testCases, port, bindHost);
            await File.WriteAllTextAsync(collectionPath, collection, ct);

            var tail =
                $"run \"{collectionPath}\" --reporters json --reporter-json-export \"{reportPath}\" --timeout-request 10000";
            var args = _newman.Value.UseNpx ? $"--yes newman {tail}" : tail;
            var (exitCode, stdout, stderr) = await RunProcessAsync(_newman.Value.ExecutablePath, args, ct);

            logger.LogInformation("newman exit {Code}: {Stderr}", exitCode, stderr?.Length > 200 ? stderr[..200] : stderr);

            if (!File.Exists(reportPath))
                return testCases.Select(tc => FailResult(tc, $"http://{bindHost}:{port}{tc.UrlTemplate}", "newman did not produce report")).ToList();

            var reportJson = await File.ReadAllTextAsync(reportPath, ct);
            return ParseNewmanReport(testCases, reportJson, port, bindHost);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "newman failed");
            return testCases.Select(tc => FailResult(tc, $"http://{bindHost}:{port}{tc.UrlTemplate}", $"newman error: {ex.Message}")).ToList();
        }
        finally
        {
            TryDelete(collectionPath);
            TryDelete(reportPath);
        }
    }

    private static string BuildPostmanCollection(List<TestCase> testCases, int port, string bindHost)
    {
        var items = new JsonArray();

        foreach (var tc in testCases)
        {
            var expect = DeserializeExpect(tc.ExpectJson);
            var url = $"http://{bindHost}:{port}{tc.UrlTemplate}";

            var httpMethod = tc.HttpMethod.ToUpperInvariant();
            var isGetOrDelete = httpMethod == "GET" || httpMethod == "DELETE";
            var requestObj = new JsonObject
            {
                ["method"] = httpMethod,
                ["header"] = isGetOrDelete
                    ? new JsonArray { new JsonObject { ["key"] = "Accept", ["value"] = "application/json" } }
                    : new JsonArray
                    {
                        new JsonObject { ["key"] = "Content-Type", ["value"] = "application/json" },
                        new JsonObject { ["key"] = "Accept",       ["value"] = "application/json" }
                    },
                ["url"] = new JsonObject { ["raw"] = url }
            };

            if (tc.InputJson != null)
            {
                if (isGetOrDelete)
                {
                    var qs = JsonToQueryString(tc.InputJson);
                    ((JsonObject)requestObj["url"]!)["raw"] = url + "?" + qs;
                }
                else
                {
                    requestObj["body"] = new JsonObject
                    {
                        ["mode"] = "raw",
                        ["raw"] = tc.InputJson
                    };
                }
            }
            else
            {
                if (httpMethod == "POST" || httpMethod == "PUT" || httpMethod == "PATCH")
                {
                    requestObj["body"] = new JsonObject
                    {
                        ["mode"] = "raw",
                        ["raw"] = "{}"
                    };
                }
            }

            var tests = new StringBuilder();
            if (expect.Status.HasValue)
                tests.AppendLine($"pm.test('status {expect.Status}', function() {{ var code = pm.response ? pm.response.code : undefined; pm.expect(code).to.eql({expect.Status}); }});");

            if (expect.Body.HasValue && expect.Body.Value.ValueKind != JsonValueKind.Undefined
                && expect.Body.Value.ValueKind != JsonValueKind.Null)
            {
                var expectedBodyJson = expect.Body.Value.GetRawText();
                tests.AppendLine($"pm.test('body match', function() {{");
                tests.AppendLine($"  if (!pm.response) {{ pm.expect.fail('No response received'); return; }}");
                tests.AppendLine($"  var text; try {{ text = pm.response.text(); }} catch(e) {{ text = null; }}");
                tests.AppendLine($"  if (text == null) {{ pm.expect.fail('Empty or no response body'); return; }}");
                tests.AppendLine($"  var res; try {{ res = JSON.parse(text); }} catch(e) {{ pm.expect.fail('Not JSON: ' + String(text).substring(0, 100)); return; }}");
                tests.AppendLine($"  var expected = {expectedBodyJson};");
                tests.AppendLine($"  pm.expect(res).to.deep.equal(expected);");
                tests.AppendLine($"}});");
            }

            var item = new JsonObject
            {
                ["name"] = tc.Name,
                ["request"] = requestObj,
                ["event"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["listen"] = "test",
                        ["script"] = new JsonObject
                        {
                            ["exec"] = new JsonArray { tests.ToString() },
                            ["type"] = "text/javascript"
                        }
                    }
                }
            };

            items.Add(item);
        }

        var collection = new JsonObject
        {
            ["info"] = new JsonObject
            {
                ["name"] = "GradingCollection",
                ["schema"] = "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
            },
            ["item"] = items
        };

        return collection.ToJsonString();
    }

    private static List<TestCaseResult> ParseNewmanReport(List<TestCase> testCases, string reportJson, int port, string bindHost)
    {
        var results = new List<TestCaseResult>(testCases.Count);

        try
        {
            using var doc = JsonDocument.Parse(reportJson);
            var root = doc.RootElement;

            // newman report: run.executions[]
            JsonElement executions = default;
            bool found = false;
            if (root.TryGetProperty("run", out var run) && run.TryGetProperty("executions", out executions))
                found = true;

            if (!found)
            {
                return testCases.Select(tc =>
                    FailResult(tc, $"http://{bindHost}:{port}{tc.UrlTemplate}", "newman report missing executions")).ToList();
            }

            var executionList = executions.EnumerateArray().ToList();

            for (int i = 0; i < testCases.Count; i++)
            {
                var tc = testCases[i];
                var url = $"http://{bindHost}:{port}{tc.UrlTemplate}";

                if (i >= executionList.Count)
                {
                    results.Add(FailResult(tc, url, "newman execution missing for this test case"));
                    continue;
                }

                var exec = executionList[i];
                int actualStatus = 0;
                string? actualBody = null;
                string? failReason = null;

                // Newman 6.x runs test scripts even on request failure; detect it early
                if (exec.TryGetProperty("requestError", out var reqErr) && reqErr.ValueKind != JsonValueKind.Null)
                {
                    var errMsg = reqErr.ValueKind == JsonValueKind.Object
                        ? FormatRequestError(reqErr)
                        : reqErr.ToString();
                    results.Add(FailResult(tc, url, $"Request error: {errMsg}"));
                    continue;
                }

                if (exec.TryGetProperty("response", out var resp))
                {
                    if (resp.TryGetProperty("code", out var code))
                        actualStatus = code.GetInt32();
                    if (resp.TryGetProperty("body", out var bodyEl))
                        actualBody = bodyEl.GetString();
                }

                // Collect test failures
                var failures = new List<string>();
                if (exec.TryGetProperty("assertions", out var assertions))
                {
                    foreach (var assertion in assertions.EnumerateArray())
                    {
                        if (assertion.TryGetProperty("error", out var err))
                        {
                            var msg = err.TryGetProperty("message", out var m) ? m.GetString() : "assertion failed";
                            failures.Add(msg ?? "assertion failed");
                        }
                    }
                }

                if (failures.Count > 0)
                    failReason = string.Join("; ", failures);

                bool pass = failReason == null;
                results.Add(new TestCaseResult
                {
                    TestCaseId = tc.Id,
                    Pass = pass,
                    AwardedScore = pass ? tc.Score : 0,
                    HttpMethod = tc.HttpMethod,
                    Url = url,
                    ActualStatus = actualStatus,
                    ActualBody = actualBody?.Length > 500 ? actualBody[..500] : actualBody,
                    FailReason = failReason,
                });
            }
        }
        catch (Exception ex)
        {
            return testCases.Select(tc =>
                FailResult(tc, $"http://{bindHost}:{port}{tc.UrlTemplate}", $"parse newman report error: {ex.Message}")).ToList();
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

        string? failReason = EvaluateHttp(expect, actualStatus, body, isJson, isHtml);
        bool pass = failReason == null;

        return new TestCaseResult
        {
            TestCaseId = tc.Id,
            Pass = pass,
            AwardedScore = pass ? tc.Score : 0,
            HttpMethod = tc.HttpMethod,
            Url = url,
            ActualStatus = actualStatus,
            ActualBody = body.Length > 500 ? body[..500] : body,
            FailReason = failReason,
        };
    }

    private static string? EvaluateHttp(ExpectJson expect, int actualStatus, string body, bool isJson, bool isHtml)
    {
        if (expect.Status != null && actualStatus != expect.Status)
            return $"Expected status {expect.Status}, got {actualStatus}";

        if (expect.Body.HasValue
            && expect.Body.Value.ValueKind != JsonValueKind.Undefined
            && expect.Body.Value.ValueKind != JsonValueKind.Null)
        {
            if (!isJson)
                return "Expected JSON body but response Content-Type is not application/json";

            JsonNode? actual;
            JsonNode? expected;
            try
            {
                actual = JsonNode.Parse(body);
                expected = JsonNode.Parse(expect.Body.Value.GetRawText());
            }
            catch { return "Response is not valid JSON"; }

            if (!JsonNode.DeepEquals(actual, expected))
                return "Response body does not match expected body";
        }

        if (isJson)
        {
            JsonElement root;
            try { root = JsonDocument.Parse(body).RootElement; }
            catch { return "Response is not valid JSON"; }

            if (expect.IsArray != null)
            {
                bool actualIsArray = root.ValueKind == JsonValueKind.Array;
                if (actualIsArray != expect.IsArray)
                    return $"Expected isArray={expect.IsArray}, got {(actualIsArray ? "array" : "object")}";
            }

            if (expect.Fields != null)
            {
                var target = root.ValueKind == JsonValueKind.Array ? root[0] : root;
                var missing = expect.Fields.Where(f => !target.TryGetProperty(f, out _)).ToList();
                if (missing.Count > 0)
                    return $"Missing fields: {string.Join(", ", missing)}";
            }
        }

        if (isHtml)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(body);

            if (expect.Value != null && !body.Contains(expect.Value, StringComparison.OrdinalIgnoreCase))
                return $"Value '{expect.Value}' not found in response";

            // id-based element check (new, replaces/supplements selector)
            if (expect.ElementId != null)
            {
                var node = doc.GetElementbyId(expect.ElementId);
                if (node == null)
                    return $"Element with id='{expect.ElementId}' not found";

                if (expect.ElementText != null
                    && !node.InnerText.Contains(expect.ElementText, StringComparison.OrdinalIgnoreCase))
                    return $"Element id='{expect.ElementId}' does not contain text '{expect.ElementText}'";
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
                        return $"Selector '{expect.Selector}' matched {nodes?.Count ?? 0}, expected >= {expect.SelectorMinCount}";
                }
                else if (nodes == null || nodes.Count == 0)
                {
                    return $"Selector '{expect.Selector}' not found";
                }

                if (expect.SelectorText != null)
                {
                    var node = doc.DocumentNode.SelectSingleNode(xpath);
                    if (node?.InnerText.Contains(expect.SelectorText, StringComparison.OrdinalIgnoreCase) != true)
                        return $"SelectorText '{expect.SelectorText}' not found in element";
                }
            }
        }

        return null;
    }

    // ── Q2: Playwright runner ──

    private async Task<List<TestCaseResult>> RunPlaywrightCasesAsync(
        List<TestCase> testCases, int port, CancellationToken ct)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var browserContext = await browser.NewContextAsync();
        var apiContext = browserContext.APIRequest;

        var context = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<TestCaseResult>(testCases.Count);

        foreach (var tc in testCases)
            results.Add(await RunPlaywrightTestCaseAsync(tc, port, browserContext, apiContext, context, ct));

        return results;
    }

    private async Task<TestCaseResult> RunPlaywrightTestCaseAsync(
        TestCase tc, int port,
        IBrowserContext browserContext, IAPIRequestContext apiContext,
        Dictionary<string, string> context, CancellationToken ct)
    {
        var urlPath = InterpolateVariables(tc.UrlTemplate, context);
        var url = $"http://{_bindHost}:{port}{urlPath}";
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

        if (expect.Status != null && actualStatus != expect.Status)
            failReason = $"Expected status {expect.Status}, got {actualStatus}";

        if (failReason == null && page != null)
            failReason = await EvaluatePlaywrightAsync(expect, page);

        if (failReason == null && page == null && !string.IsNullOrEmpty(body))
            failReason = EvaluateJsonBody(expect, body);

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
        return new TestCaseResult
        {
            TestCaseId      = tc.Id,
            Pass            = pass,
            AwardedScore    = pass ? tc.Score : 0,
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

    private static string? EvaluateJsonBody(ExpectJson expect, string body)
    {
        JsonElement root;
        try { root = JsonDocument.Parse(body).RootElement; }
        catch { return "Response is not valid JSON"; }

        if (expect.IsArray != null)
        {
            bool actualIsArray = root.ValueKind == JsonValueKind.Array;
            if (actualIsArray != expect.IsArray)
                return $"Expected isArray={expect.IsArray}, got {(actualIsArray ? "array" : "object")}";
        }

        if (expect.Fields != null)
        {
            var target = root.ValueKind == JsonValueKind.Array ? root[0] : root;
            var missing = expect.Fields.Where(f => !target.TryGetProperty(f, out _)).ToList();
            if (missing.Count > 0)
                return $"Missing fields: {string.Join(", ", missing)}";
        }

        return null;
    }

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

    private static string FormatRequestError(JsonElement requestError)
    {
        var parts = new List<string>();
        if (requestError.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
            parts.Add(message.GetString()!);
        if (requestError.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String)
            parts.Add($"code={code.GetString()}");
        if (requestError.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
            parts.Add($"name={name.GetString()}");

        if (parts.Count > 0)
            return string.Join(", ", parts);

        var raw = requestError.GetRawText();
        return string.IsNullOrWhiteSpace(raw) || raw == "{}"
            ? "Unknown request error"
            : raw;
    }

    private readonly struct NewmanLaunch(string executablePath, bool useNpx)
    {
        public string ExecutablePath { get; } = executablePath;
        public bool UseNpx { get; } = useNpx;
    }

    private static NewmanLaunch? ResolveNewman(WorkerOptions opts, ILogger logger)
    {
        var configured = opts.NewmanExecutable?.Trim();
        if (!string.IsNullOrEmpty(configured))
        {
            if (File.Exists(configured))
                return new NewmanLaunch(configured, false);
            logger.LogWarning(
                "Worker:NewmanExecutable '{Path}' not found — searching PATH / npx",
                configured);
        }

        var fromPath = FindExecutableOnPath("newman");
        if (fromPath is not null)
            return new NewmanLaunch(fromPath, false);

        if (OperatingSystem.IsWindows())
        {
            var appDataNpm = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "npm",
                "newman.cmd");
            if (File.Exists(appDataNpm))
                return new NewmanLaunch(appDataNpm, false);
        }

        var npx = FindExecutableOnPath("npx");
        if (npx is not null)
            return new NewmanLaunch(npx, true);

        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var npxPf = Path.Combine(programFiles, "nodejs", "npx.cmd");
            if (File.Exists(npxPf))
                return new NewmanLaunch(npxPf, true);

            var pf86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            if (!string.IsNullOrEmpty(pf86))
            {
                var npx86 = Path.Combine(pf86, "nodejs", "npx.cmd");
                if (File.Exists(npx86))
                    return new NewmanLaunch(npx86, true);
            }
        }

        logger.LogWarning(
            "Newman not resolved: not on PATH, not under %AppData%\\npm, and npx not found.");
        return null;
    }

    private static string? FindExecutableOnPath(string nameWithoutExtension)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
            return null;

        IEnumerable<string> names = OperatingSystem.IsWindows()
            ?
            [
                $"{nameWithoutExtension}.exe",
                $"{nameWithoutExtension}.cmd",
                $"{nameWithoutExtension}.bat",
                nameWithoutExtension,
            ]
            : [nameWithoutExtension, $"{nameWithoutExtension}.exe"];

        foreach (var segment in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var dir = segment.Trim().Trim('"');
            if (string.IsNullOrEmpty(dir))
                continue;

            foreach (var n in names)
            {
                var candidate = Path.Combine(dir, n);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    private static async Task<(int exitCode, string stdout, string stderr)> RunProcessAsync(
        string fileName, string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)!;
        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return (process.ExitCode, stdout, stderr);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }
}
