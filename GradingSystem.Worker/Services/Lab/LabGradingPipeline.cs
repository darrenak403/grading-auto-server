using GradingSystem.Application.Interfaces;
using GradingSystem.Domain.Entities;

namespace GradingSystem.Worker.Services.Lab;

public class LabGradingPipeline(
    IServiceScopeFactory scopeFactory,
    DockerComposeRunner docker,
    LabTestRunner testRunner,
    SourceAnalyzer sourceAnalyzer,
    ILogger<LabGradingPipeline> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

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

        try
        {
            // Phase 1: Extract archive — SOURCE checks need this, always runs first
            var workDir = docker.Extract(submission.FilePath, jobId);

            // Phase 2: SOURCE checks — file-system only, no Docker needed
            foreach (var tc in sourceTests)
            {
                var r = sourceAnalyzer.Check(tc, workDir, jobId);
                allResults.Add(r);
                logger.LogInformation("Job {JobId} SOURCE tc {TcId}: passed={Passed} — {Detail}",
                    jobId, tc.Id, r.Passed, r.ActualResponse);
            }

            // Phase 3: Docker + HTTP checks
            var apiPort = await docker.StartContainersAsync(workDir, jobId, ct);
            var httpResults = await testRunner.RunAsync($"http://localhost:{apiPort}", jobId, httpTests, ct);
            allResults.AddRange(httpResults);

            job.Status        = LabGradingJobStatus.Done;
            submission.Status = LabSubmissionStatus.Done;
            logger.LogInformation("Lab job {JobId} done — {Passed}/{Total} passed",
                jobId, allResults.Count(r => r.Passed), allResults.Count);
        }
        catch (DockerComposeTimeoutException ex)
        {
            logger.LogWarning(ex, "Lab job {JobId} timed out starting Docker", jobId);
            allResults.AddRange(FailRemaining(httpTests, allResults, jobId, $"Docker timed out: {ex.Message}"));
            uow.ClearChanges();
            job        = (await uow.LabGradingJobs.GetByIdAsync(jobId))!;
            submission = (await uow.LabSubmissions.GetByIdAsync(job.LabSubmissionId))!;
            job.Status        = LabGradingJobStatus.Failed;
            job.ErrorMessage  = ex.Message;
            submission.Status = LabSubmissionStatus.BuildFailed;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lab job {JobId} failed", jobId);
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

            try { await docker.StopAsync(jobId); }
            catch (Exception ex) { logger.LogError(ex, "Docker cleanup failed for job {JobId}", jobId); }

            try { await uow.SaveChangesAsync(CancellationToken.None); }
            catch (Exception ex) { logger.LogError(ex, "Final save failed for job {JobId}", jobId); }
        }
    }

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
