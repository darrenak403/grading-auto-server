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
/// Phase 3 (pe-grading-scale-reliability): a submission that never finishes must be
/// interrupted at WorkerOptions.SubmissionTimeoutSeconds, cleaned up, and must not affect
/// other concurrently-running submissions.
/// </summary>
public class GradingPipelineTimeoutTests
{
    private class HangingArtifactRunner(WorkerOptions opts) : ArtifactRunner(
        Options.Create(opts),
        new ConfigurationBuilder().Build(),
        NullLogger<ArtifactRunner>.Instance)
    {
        public ConcurrentBag<string> CleanedUpSandboxPaths { get; } = new();

        public override async Task RunAsync(
            GradingJob job, IReadOnlyList<Question> questions, StudentContext ctx, CancellationToken ct)
        {
            // Simulates a hung student process: never completes on its own,
            // only stops if the caller's token is cancelled.
            await Task.Delay(TimeSpan.FromMinutes(10), ct);
        }

        public override Task CleanupAsync(StudentContext ctx)
        {
            CleanedUpSandboxPaths.Add(ctx.SandboxPath);
            return Task.CompletedTask;
        }
    }

    private class DelayingArtifactRunner(TimeSpan delay) : ArtifactRunner(
        Options.Create(new WorkerOptions()),
        new ConfigurationBuilder().Build(),
        NullLogger<ArtifactRunner>.Instance)
    {
        public ConcurrentBag<Guid> CompletedJobIds { get; } = new();

        public override async Task RunAsync(
            GradingJob job, IReadOnlyList<Question> questions, StudentContext ctx, CancellationToken ct)
        {
            await Task.Delay(delay, ct);
            CompletedJobIds.Add(job.Id);
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
    public async Task ProcessAsync_SubmissionThatNeverFinishes_IsInterruptedWithinConfiguredTimeout()
    {
        var opts = new WorkerOptions { SubmissionTimeoutSeconds = 1 };
        var artifactRunner = new HangingArtifactRunner(opts);
        var testRunner = new NoOpTestRunner();
        var (uow, job) = await SeedJobAsync();

        var pipeline = new GradingPipeline(
            BuildScopeFactory(uow),
            artifactRunner,
            testRunner,
            Options.Create(opts),
            NullLogger<GradingPipeline>.Instance);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await pipeline.ProcessAsync(job.Id, CancellationToken.None);
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5),
            $"Expected the hung submission to be interrupted near the 1s timeout, took {sw.Elapsed}.");

        Assert.Contains(artifactRunner.CleanedUpSandboxPaths, p => p.Contains(job.Id.ToString()));

        var savedJob = await uow.GradingJobs.GetByIdAsync(job.Id);
        Assert.Equal(JobStatus.Failed, savedJob!.Status);
    }

    [Fact]
    public async Task ProcessAsync_OneSubmissionTimesOut_OtherConcurrentSubmissionIsUnaffected()
    {
        var opts = new WorkerOptions { SubmissionTimeoutSeconds = 1 };
        var hangingRunner = new HangingArtifactRunner(opts);
        var healthyRunner = new DelayingArtifactRunner(TimeSpan.FromMilliseconds(100));
        var testRunner = new NoOpTestRunner();

        var (uow, hangingJob) = await SeedJobAsync();
        var (_, healthyJob) = await SeedJobAsync(uow);

        var scopeFactory = BuildScopeFactory(uow);

        var hangingPipeline = new GradingPipeline(
            scopeFactory, hangingRunner, testRunner, Options.Create(opts), NullLogger<GradingPipeline>.Instance);
        var healthyPipeline = new GradingPipeline(
            scopeFactory, healthyRunner, testRunner, Options.Create(opts), NullLogger<GradingPipeline>.Instance);

        await Task.WhenAll(
            hangingPipeline.ProcessAsync(hangingJob.Id, CancellationToken.None),
            healthyPipeline.ProcessAsync(healthyJob.Id, CancellationToken.None));

        Assert.Contains(healthyJob.Id, healthyRunner.CompletedJobIds);

        var savedHealthyJob = await uow.GradingJobs.GetByIdAsync(healthyJob.Id);
        Assert.Equal(JobStatus.Done, savedHealthyJob!.Status);

        var savedHangingJob = await uow.GradingJobs.GetByIdAsync(hangingJob.Id);
        Assert.Equal(JobStatus.Failed, savedHangingJob!.Status);
    }

    [Fact]
    public async Task ProcessAsync_SubmissionTimesOut_RecordsNoteDistinguishableFromOtherFailures()
    {
        var opts = new WorkerOptions { SubmissionTimeoutSeconds = 1 };
        var artifactRunner = new HangingArtifactRunner(opts);
        var testRunner = new NoOpTestRunner();
        var (uow, job, assignment) = await SeedJobWithQuestionAsync();

        var pipeline = new GradingPipeline(
            BuildScopeFactory(uow),
            artifactRunner,
            testRunner,
            Options.Create(opts),
            NullLogger<GradingPipeline>.Instance);

        await pipeline.ProcessAsync(job.Id, CancellationToken.None);

        var savedJob = await uow.GradingJobs.GetByIdAsync(job.Id);
        Assert.Equal(JobStatus.Failed, savedJob!.Status);

        var results = await uow.QuestionResults.FindAsync(r => r.GradingJobId == job.Id);
        var note = Assert.Single(results).Detail;
        Assert.Contains("timeout", note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessAsync_WorkerShutdownMidGrade_DoesNotRecordATimeoutNote()
    {
        var opts = new WorkerOptions { SubmissionTimeoutSeconds = 90 };
        var artifactRunner = new HangingArtifactRunner(opts);
        var testRunner = new NoOpTestRunner();
        var (uow, job, assignment) = await SeedJobWithQuestionAsync();

        var pipeline = new GradingPipeline(
            BuildScopeFactory(uow),
            artifactRunner,
            testRunner,
            Options.Create(opts),
            NullLogger<GradingPipeline>.Instance);

        using var shutdownCts = new CancellationTokenSource();
        var processTask = pipeline.ProcessAsync(job.Id, shutdownCts.Token);
        shutdownCts.Cancel();
        await processTask;

        var savedJob = await uow.GradingJobs.GetByIdAsync(job.Id);
        Assert.Equal(JobStatus.Failed, savedJob!.Status);

        var results = await uow.QuestionResults.FindAsync(r => r.GradingJobId == job.Id);
        var note = Assert.Single(results).Detail;
        Assert.DoesNotContain("timeout", note, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(FakeUnitOfWork Uow, GradingJob Job)> SeedJobAsync(FakeUnitOfWork? uow = null)
    {
        uow ??= new FakeUnitOfWork();

        var assignment = new Assignment { Id = Guid.NewGuid(), Title = "Mã đề" };
        await uow.Assignments.AddAsync(assignment);

        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignment.Id,
            StudentCode = "SV001",
            Status = SubmissionStatus.Pending,
        };
        var job = new GradingJob
        {
            Id = Guid.NewGuid(),
            SubmissionId = submission.Id,
            Status = JobStatus.Pending,
        };
        await uow.Submissions.AddAsync(submission);
        await uow.GradingJobs.AddAsync(job);

        return (uow, job);
    }

    private static async Task<(FakeUnitOfWork Uow, GradingJob Job, Assignment Assignment)> SeedJobWithQuestionAsync()
    {
        var (uow, job) = await SeedJobAsync();
        var assignment = (await uow.Assignments.FindAsync(a => true)).Single();

        var question = new Question
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignment.Id,
            Title = "Q1",
            Type = QuestionType.Api,
            MaxScore = 10,
            ArtifactFolderName = "Q1",
        };
        await uow.Questions.AddAsync(question);

        return (uow, job, assignment);
    }

    private static IServiceScopeFactory BuildScopeFactory(IUnitOfWork uow)
    {
        var services = new ServiceCollection();
        services.AddSingleton(uow);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }
}
