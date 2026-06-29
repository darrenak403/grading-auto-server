using GradingSystem.Application.Interfaces;
using GradingSystem.Domain.Entities;
using GradingSystem.Worker.Options;
using Microsoft.Extensions.Options;

namespace GradingSystem.Worker.Services.Lab;

public class LabGradingPipeline(
    IServiceScopeFactory scopeFactory,
    DockerComposeRunner docker,
    LabTestRunner testRunner,
    SourceAnalyzer sourceAnalyzer,
    IOptions<WorkerOptions> opts,
    ILogger<LabGradingPipeline> logger)
{
    private readonly SemaphoreSlim _gate = new(Math.Max(1, opts.Value.LabMaxConcurrentJobs), Math.Max(1, opts.Value.LabMaxConcurrentJobs));

    public async Task ProcessAsync(Guid jobId, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await RunAsync(jobId, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task RunAsync(Guid jobId, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var job = await uow.LabGradingJobs.GetByIdAsync(jobId);
        if (job is null)
        {
            logger.LogWarning("LabGradingJob {Id} not found", jobId);
            return;
        }

        if (job.Status == LabGradingJobStatus.Done || job.Status == LabGradingJobStatus.Failed)
        {
            logger.LogInformation("LabGradingJob {Id} is already {Status} — skipping", jobId, job.Status);
            return;
        }

        var submission = await uow.LabSubmissions.GetByIdAsync(job.LabSubmissionId);
        if (submission is null)
        {
            logger.LogError("LabSubmission {Id} not found for job {JobId}", job.LabSubmissionId, jobId);
            return;
        }

        var testCases = (await uow.LabTestCases.FindAsync(tc =>
            tc.LabAssignmentId == submission.LabAssignmentId &&
            tc.Status == LabTestCaseStatus.Approved))
            .ToList();

        var sourceTests = testCases
            .Where(tc => tc.HttpMethod.Equals("SOURCE", StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Order).ThenBy(t => t.CreatedAt)
            .ToList();
        var httpTests = testCases
            .Where(tc => !tc.HttpMethod.Equals("SOURCE", StringComparison.OrdinalIgnoreCase))
            .ToList();

        job.Status    = LabGradingJobStatus.Running;
        job.StartedAt = DateTime.UtcNow;
        submission.Status = LabSubmissionStatus.Grading;
        uow.LabGradingJobs.Update(job);
        uow.LabSubmissions.Update(submission);
        await uow.SaveChangesAsync(ct);

        logger.LogInformation(
            "Processing lab job {JobId} — {Total} approved test cases ({Source} source, {Http} http)",
            jobId, testCases.Count, sourceTests.Count, httpTests.Count);

        var allResults = new List<LabTestCaseResult>();
        var dockerStarted = false;
        string? workDir = null;

        try
        {
            // Phase 1: Extract archive — SOURCE checks need this, always runs first
            workDir = DockerComposeRunner.Extract(submission.FilePath, jobId);

            // Phase 2: SOURCE checks — file-system only, no Docker needed
            foreach (var tc in sourceTests)
            {
                var r = sourceAnalyzer.Check(tc, workDir, jobId);
                allResults.Add(r);
                logger.LogInformation("Job {JobId} SOURCE tc {TcId}: passed={Passed} — {Detail}",
                    jobId, tc.Id, r.Passed, r.ActualResponse);
            }

            // Phase 3: Docker + HTTP checks
            if (httpTests.Count > 0)
            {
                dockerStarted = true;
                var apiPort = await docker.StartContainersAsync(workDir, jobId, ct);
                var httpResults = await testRunner.RunAsync(docker.GetApiBaseUrl(apiPort), jobId, httpTests, ct);
                allResults.AddRange(httpResults);
            }
            else
            {
                logger.LogInformation("Job {JobId} has no HTTP test cases — skipping Docker startup", jobId);
            }

            job.Status        = LabGradingJobStatus.Done;
            submission.Status = LabSubmissionStatus.Done;
            logger.LogInformation("Lab job {JobId} done — {Passed}/{Total} passed",
                jobId, allResults.Count(r => r.Passed), allResults.Count);
        }
        catch (Exception ex) when (ex is DockerComposeException or DockerComposeTimeoutException)
        {
            logger.LogWarning(ex, "Lab job {JobId} Docker failed: {Message}", jobId, ex.Message);
            allResults.AddRange(FailRemaining(httpTests, allResults, jobId, ex.Message));
            uow.ClearChanges();
            job        = (await uow.LabGradingJobs.GetByIdAsync(jobId))!;
            submission = (await uow.LabSubmissions.GetByIdAsync(job.LabSubmissionId))!;
            job.Status        = LabGradingJobStatus.Failed;
            job.ErrorMessage  = ex.Message;
            submission.Status = LabSubmissionStatus.BuildFailed;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lab job {JobId} unexpected error", jobId);
            allResults.AddRange(FailRemaining(testCases, allResults, jobId, ex.Message));
            uow.ClearChanges();
            job        = (await uow.LabGradingJobs.GetByIdAsync(jobId))!;
            submission = (await uow.LabSubmissions.GetByIdAsync(job.LabSubmissionId))!;
            job.Status        = LabGradingJobStatus.Failed;
            job.ErrorMessage  = ex.Message;
            submission.Status = LabSubmissionStatus.Error;
        }
        finally
        {
            foreach (var r in allResults)
                await uow.LabTestCaseResults.AddAsync(r);

            job.FinishedAt = DateTime.UtcNow;
            uow.LabGradingJobs.Update(job);
            uow.LabSubmissions.Update(submission);

            if (dockerStarted)
            {
                try { await docker.StopAsync(jobId); }
                catch (Exception ex) { logger.LogError(ex, "Docker cleanup failed for job {JobId}", jobId); }
            }
            else if (workDir is not null)
            {
                TryDeleteWorkDir(workDir, jobId);
            }

            try { await uow.SaveChangesAsync(CancellationToken.None); }
            catch (Exception ex) { logger.LogError(ex, "Final save failed for job {JobId}", jobId); }

            try
            {
                await RefreshSubmissionAndAssignmentStatusAsync(uow, submission.LabAssignmentId, submission.Id, job.Id);
                await uow.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Status refresh failed for lab job {JobId}", jobId);
            }
        }
    }

    private void TryDeleteWorkDir(string workDir, Guid jobId)
    {
        try
        {
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete lab workdir for job {JobId}: {Path}", jobId, workDir);
        }
    }

    private static async Task RefreshSubmissionAndAssignmentStatusAsync(
        IUnitOfWork uow,
        Guid assignmentId,
        Guid submissionId,
        Guid completedJobId)
    {
        var submission = await uow.LabSubmissions.GetByIdAsync(submissionId);
        if (submission is not null)
        {
            var submissionJobs = (await uow.LabGradingJobs.FindAsync(j => j.LabSubmissionId == submissionId))
                .OrderByDescending(j => j.CreatedAt)
                .ThenByDescending(j => j.Id)
                .ToList();
            var latestJob = submissionJobs.FirstOrDefault();
            if (latestJob is not null && latestJob.Id != completedJobId)
            {
                submission.Status = ToSubmissionStatus(latestJob);
                submission.UpdatedAt = DateTime.UtcNow;
                uow.LabSubmissions.Update(submission);
            }
        }

        var assignment = await uow.LabAssignments.GetByIdAsync(assignmentId);
        if (assignment is null) return;

        var submissions = (await uow.LabSubmissions.FindAsync(s => s.LabAssignmentId == assignmentId)).ToList();
        if (submissions.Count == 0) return;

        var submissionIds = submissions.Select(s => s.Id).ToHashSet();
        var allJobs = (await uow.LabGradingJobs.FindAsync(j => submissionIds.Contains(j.LabSubmissionId))).ToList();
        if (allJobs.Count == 0) return;

        assignment.Status = allJobs.Any(j => j.Status is LabGradingJobStatus.Pending or LabGradingJobStatus.Running)
            ? LabAssignmentStatus.Grading
            : LabAssignmentStatus.Done;
        assignment.UpdatedAt = DateTime.UtcNow;
        uow.LabAssignments.Update(assignment);
    }

    private static LabSubmissionStatus ToSubmissionStatus(LabGradingJob job) =>
        job.Status switch
        {
            LabGradingJobStatus.Pending => LabSubmissionStatus.Pending,
            LabGradingJobStatus.Running => LabSubmissionStatus.Grading,
            LabGradingJobStatus.Done => LabSubmissionStatus.Done,
            _ => LabSubmissionStatus.Error
        };

    private static IEnumerable<LabTestCaseResult> FailRemaining(
        IEnumerable<LabTestCase> allTests,
        List<LabTestCaseResult> done,
        Guid jobId,
        string error)
    {
        var doneIds = done.Select(r => r.LabTestCaseId).ToHashSet();
        return allTests
            .Where(tc => !doneIds.Contains(tc.Id))
            .Select(tc => new LabTestCaseResult
            {
                LabGradingJobId  = jobId,
                LabTestCaseId    = tc.Id,
                Passed           = false,
                AwardedScore     = 0,
                ActualStatusCode = 0,
                ErrorMessage     = error,
            });
    }
}
