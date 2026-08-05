using GradingSystem.Domain.Entities;
using GradingSystem.Worker.Services.Lab;
using Microsoft.Extensions.Logging.Abstractions;

namespace GradingSystem.Tests.Worker.Services;

public class LabTestRunnerTests
{
    [Fact]
    public async Task RunAsync_WhenStudentApiTransportFails_SkipsRemainingHttpTests()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new TaskCanceledException("student API timed out"));
        var runner = CreateRunner(handler);
        var jobId = Guid.NewGuid();

        var results = await runner.RunAsync("http://student", jobId,
        [
            TestCase("POST", "/api/auth/login", order: 1),
            TestCase("GET", "/api/courses", order: 2),
        ], CancellationToken.None);

        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(2, results.Count);
        Assert.False(results[0].Passed);
        Assert.Contains("HTTP error", results[0].ErrorMessage);
        Assert.False(results[1].Passed);
        Assert.Contains("previous API request", results[1].ErrorMessage);
        Assert.Equal("http://student/api/courses", results[1].ActualResponse);
    }

    [Fact]
    public async Task RunAsync_WhenStudentApiReturnsFailingStatus_ContinuesRemainingHttpTests()
    {
        var handler = new StubHttpMessageHandler((_, requestNumber) =>
            Task.FromResult(new HttpResponseMessage(requestNumber == 1
                ? System.Net.HttpStatusCode.InternalServerError
                : System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}"),
            }));
        var runner = CreateRunner(handler);
        var jobId = Guid.NewGuid();

        var results = await runner.RunAsync("http://student", jobId,
        [
            TestCase("GET", "/api/broken", order: 1),
            TestCase("GET", "/api/healthy", order: 2),
        ], CancellationToken.None);

        Assert.Equal(2, handler.RequestCount);
        Assert.Equal(2, results.Count);
        Assert.False(results[0].Passed);
        Assert.True(results[1].Passed);
    }

    private static LabTestRunner CreateRunner(HttpMessageHandler handler) =>
        new(new StubHttpClientFactory(handler), NullLogger<LabTestRunner>.Instance);

    private static LabTestCase TestCase(string method, string url, int order) => new()
    {
        Id = Guid.NewGuid(),
        LabAssignmentId = Guid.NewGuid(),
        HttpMethod = method,
        UrlTemplate = url,
        ExpectedStatusCode = 200,
        MatchMode = LabTestCaseMatchMode.StatusOnly,
        Score = 1,
        Status = LabTestCaseStatus.Approved,
        Order = order,
    };

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, int, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
    {
        private int _requestCount;

        public int RequestCount => _requestCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var requestNumber = Interlocked.Increment(ref _requestCount);
            return sendAsync(request, requestNumber);
        }
    }
}
