using System.Net;
using System.Text;
using System.Text.Json;
using GradingSystem.Application.DTOs;
using GradingSystem.Application.Interfaces;
using GradingSystem.Application.Options;
using GradingSystem.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace GradingSystem.Tests.Application.Services;

public class SupabaseSyncServiceTests
{
    [Fact]
    public async Task GetDropdownOptionsAsync_QueriesGradingSessionsAndReturnsSessionOptions()
    {
        var paths = new List<string>();
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.PathAndQuery;
            paths.Add(path);

            if (path.StartsWith("/rest/v1/terms", StringComparison.Ordinal))
            {
                return Task.FromResult(JsonResponse("[{\"id\":\"term-id\",\"name\":\"Summer 2026\"}]"));
            }
            if (path.StartsWith("/rest/v1/classes", StringComparison.Ordinal))
            {
                return Task.FromResult(JsonResponse("[{\"name\":\"SE1815\",\"terms\":{\"id\":\"term-id\",\"name\":\"Summer 2026\"}}]"));
            }
            if (path.StartsWith("/rest/v1/grading_sessions", StringComparison.Ordinal))
            {
                return Task.FromResult(JsonResponse("[{\"id\":\"session-id\",\"name\":\"LAB1 - Dot 1\",\"status\":\"open\",\"deadline\":null,\"labs\":{\"code\":\"LAB1\",\"title\":\"REST API\"},\"classes\":{\"name\":\"SE1815\",\"terms\":{\"id\":\"term-id\",\"name\":\"Summer 2026\"}}}]"));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
        var service = CreateService(handler);

        var result = await service.GetDropdownOptionsAsync("term-id", "SE1815", "LAB1");

        var session = Assert.Single(result.Sessions);
        Assert.Equal("session-id", session.Id);
        Assert.Equal("LAB1", session.LabCode);
        Assert.Equal("REST API", session.LabTitle);
        Assert.Equal("open", session.Status);
        Assert.Equal("LAB1", Assert.Single(result.Labs).Code);
        Assert.Contains(paths, path => path.StartsWith("/rest/v1/grading_sessions", StringComparison.Ordinal));
        Assert.DoesNotContain(paths, path => path.Contains("class_labs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SyncGradeAsync_UsesGradingSessionSchemaAndRpc()
    {
        var requests = new List<CapturedRequest>();
        var handler = new StubHttpMessageHandler(async request =>
        {
            requests.Add(await CapturedRequest.FromAsync(request));
            var path = request.RequestUri!.PathAndQuery;

            if (path.StartsWith("/rest/v1/class_students", StringComparison.Ordinal))
            {
                return JsonResponse("[{\"id\":\"class-student-id\"}]");
            }
            if (path.StartsWith("/rest/v1/grading_sessions", StringComparison.Ordinal))
            {
                return JsonResponse("[{\"id\":\"grading-session-id\"}]");
            }
            if (path == "/rest/v1/rpc/create_session_submission")
            {
                return JsonResponse("{}");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var service = CreateService(handler);
        using var detailsDocument = JsonDocument.Parse("{\"passed\":1}");

        var result = await service.SyncGradeAsync(new SyncSupabaseGradeRequest(
            "SE180001",
            "SE1815",
            "LAB1",
            8.5m,
            detailsDocument.RootElement.Clone(),
            "https://example.test/submission",
            "term-id",
            "grading-session-id"));

        Assert.Equal("class-student-id", result.ClassStudentId);
        Assert.Equal("grading-session-id", result.GradingSessionId);
        Assert.DoesNotContain(requests, request =>
            request.PathAndQuery.Contains("class_labs", StringComparison.Ordinal) ||
            request.PathAndQuery.Contains("resubmission_requests", StringComparison.Ordinal) ||
            request.PathAndQuery.Contains("create_class_lab_submission", StringComparison.Ordinal));

        var rpcRequest = Assert.Single(requests, request =>
            request.PathAndQuery == "/rest/v1/rpc/create_session_submission");
        using var payload = JsonDocument.Parse(rpcRequest.Body!);
        var root = payload.RootElement;
        Assert.Equal("class-student-id", root.GetProperty("p_class_student_id").GetString());
        Assert.Equal("grading-session-id", root.GetProperty("p_grading_session_id").GetString());
        Assert.False(root.TryGetProperty("p_class_lab_id", out _));
        Assert.False(root.TryGetProperty("p_item_type", out _));
        Assert.False(root.TryGetProperty("p_fulfills_request_id", out _));
    }

    [Fact]
    public async Task SyncGradesAsync_BackfillsMissingSubmissionsAfterBatchSync()
    {
        var requests = new List<CapturedRequest>();
        var classStudentCalls = 0;
        var handler = new StubHttpMessageHandler(async request =>
        {
            requests.Add(await CapturedRequest.FromAsync(request));
            var path = request.RequestUri!.PathAndQuery;

            if (path.StartsWith("/rest/v1/class_students", StringComparison.Ordinal))
            {
                classStudentCalls++;
                return JsonResponse($"[{{\"id\":\"class-student-id-{classStudentCalls}\"}}]");
            }
            if (path.StartsWith("/rest/v1/grading_sessions", StringComparison.Ordinal))
            {
                return JsonResponse("[{\"id\":\"grading-session-id\"}]");
            }
            if (path == "/rest/v1/rpc/create_session_submission")
            {
                return JsonResponse("{}");
            }
            if (path == "/rest/v1/rpc/backfill_missing_session_submissions_from_previous")
            {
                return JsonResponse("2");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var service = CreateService(handler);
        using var detailsDocument = JsonDocument.Parse("{\"passed\":1}");

        var result = await service.SyncGradesAsync(new SyncSupabaseGradesRequest(
            "SE1815",
            "LAB1",
            new List<SyncSupabaseGradeItemRequest>
            {
                new SyncSupabaseGradeItemRequest(
                    "SE180001",
                    8.5m,
                    detailsDocument.RootElement.Clone(),
                    "https://example.test/submission-1"),
                new SyncSupabaseGradeItemRequest(
                    "SE180002",
                    9.0m,
                    detailsDocument.RootElement.Clone(),
                    "https://example.test/submission-2")
            },
            "term-id",
            "grading-session-id"));

        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.SyncedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(2, result.BackfilledCount);
        Assert.Equal(2, requests.Count(request =>
            request.PathAndQuery == "/rest/v1/rpc/create_session_submission"));

        var backfillRequest = Assert.Single(requests, request =>
            request.PathAndQuery == "/rest/v1/rpc/backfill_missing_session_submissions_from_previous");
        using var payload = JsonDocument.Parse(backfillRequest.Body!);
        Assert.Equal(
            "grading-session-id",
            payload.RootElement.GetProperty("p_grading_session_id").GetString());
    }

    private static SupabaseSyncService CreateService(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://project.supabase.co/")
        };
        var options = Options.Create(new SupabaseOptions
        {
            Url = "https://project.supabase.co",
            ServiceRoleKey = "test-service-role-key"
        });

        return new SupabaseSyncService(
            client,
            new Mock<IUnitOfWork>().Object,
            options,
            new Mock<ILogger<SupabaseSyncService>>().Object);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed record CapturedRequest(string PathAndQuery, string? Body)
    {
        public static async Task<CapturedRequest> FromAsync(HttpRequestMessage request) => new(
            request.RequestUri!.PathAndQuery,
            request.Content is null ? null : await request.Content.ReadAsStringAsync());
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request);
    }
}
