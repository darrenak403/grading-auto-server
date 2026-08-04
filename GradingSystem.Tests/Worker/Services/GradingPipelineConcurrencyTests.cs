using System.Collections.Concurrent;
using GradingSystem.Application.Interfaces;
using GradingSystem.Domain.Entities;
using GradingSystem.Tests.Fakes;
using GradingSystem.Worker.Options;
using GradingSystem.Worker.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GradingSystem.Tests.Worker.Services;

/// <summary>
/// Phase 1 (pe-grading-scale-reliability): proves the per-assignment SemaphoreSlim lock
/// no longer serializes two submissions of the same assignment.
/// </summary>
public class GradingPipelineConcurrencyTests
{
    private class DelayingArtifactRunner(TimeSpan delay) : ArtifactRunner(
        Options.Create(new WorkerOptions()),
        new ConfigurationBuilder().Build(),
        NullLogger<ArtifactRunner>.Instance)
    {
        public ConcurrentBag<(DateTime Start, DateTime End)> Windows { get; } = new();

        public override async Task RunAsync(
            GradingJob job, IReadOnlyList<Question> questions, StudentContext ctx, CancellationToken ct)
        {
            var start = DateTime.UtcNow;
            await Task.Delay(delay, ct);
            Windows.Add((start, DateTime.UtcNow));
        }
    }

    private class NoOpTestRunner() : TestRunner(
        NullLogger<TestRunner>.Instance,
        Options.Create(new WorkerOptions()),
        Options.Create(new PlaywrightOptions()))
    {
        public override Task RunAsync(GradingJob job, StudentContext ctx, IUnitOfWork uow, CancellationToken ct)
            => Task.CompletedTask;
    }

    [Fact]
    public async Task ProcessAsync_TwoConcurrentSubmissionsForSameAssignment_OverlapInTime()
    {
        var assignment = new Assignment { Id = Guid.NewGuid(), Title = "Shared mã đề" };
        var uow = new FakeUnitOfWork();
        await uow.Assignments.AddAsync(assignment);

        var (job1, sub1) = MakeJobAndSubmission(assignment.Id);
        var (job2, sub2) = MakeJobAndSubmission(assignment.Id);
        await uow.Submissions.AddAsync(sub1);
        await uow.Submissions.AddAsync(sub2);
        await uow.GradingJobs.AddAsync(job1);
        await uow.GradingJobs.AddAsync(job2);

        var services = new ServiceCollection();
        services.AddSingleton<IUnitOfWork>(uow);
        var provider = services.BuildServiceProvider();

        var artifactRunner = new DelayingArtifactRunner(TimeSpan.FromMilliseconds(250));
        var testRunner = new NoOpTestRunner();
        var pipeline = new GradingPipeline(
            provider.GetRequiredService<IServiceScopeFactory>(),
            artifactRunner,
            testRunner,
            Options.Create(new WorkerOptions()),
            NullLogger<GradingPipeline>.Instance);

        await Task.WhenAll(
            pipeline.ProcessAsync(job1.Id, CancellationToken.None),
            pipeline.ProcessAsync(job2.Id, CancellationToken.None));

        var windows = artifactRunner.Windows.ToList();
        Assert.Equal(2, windows.Count);

        var (s1, e1) = windows[0];
        var (s2, e2) = windows[1];
        var overlap = s1 < e2 && s2 < e1;

        Assert.True(overlap,
            "Expected two same-assignment submissions to run with overlapping time windows " +
            "once the per-assignment SemaphoreSlim lock is removed.");
    }

    private static (GradingJob Job, Submission Submission) MakeJobAndSubmission(Guid assignmentId)
    {
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignmentId,
            StudentCode = "SV001",
            Status = SubmissionStatus.Pending,
        };
        var job = new GradingJob
        {
            Id = Guid.NewGuid(),
            SubmissionId = submission.Id,
            Status = JobStatus.Pending,
        };
        return (job, submission);
    }
}
