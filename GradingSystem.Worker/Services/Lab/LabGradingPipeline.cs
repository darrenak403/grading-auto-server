using GradingSystem.Application.Interfaces;
using GradingSystem.Domain.Entities;

namespace GradingSystem.Worker.Services.Lab;

public class LabGradingPipeline(
    IServiceScopeFactory scopeFactory,
    DockerComposeRunner docker,
    LabTestRunner testRunner,
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

        if (job.Status != LabGradingJobStatus.Pending)
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

        job.Status    = LabGradingJobStatus.Running;
        job.StartedAt = DateTime.UtcNow;
        submission.Status = LabSubmissionStatus.Grading;
        uow.LabGradingJobs.Update(job);
        uow.LabSubmissions.Update(submission);
        await uow.SaveChangesAsync(ct);

        logger.LogInformation("Processing lab job {JobId}, {Count} approved testcases", jobId, testCases.Count);

        int apiPort = 0;
        try
        {
            apiPort = await docker.StartAsync(submission.FilePath, jobId, ct);
            var results = await testRunner.RunAsync($"http://localhost:{apiPort}", jobId, testCases, ct);

            foreach (var r in results)
                await uow.LabTestCaseResults.AddAsync(r);

            job.Status        = LabGradingJobStatus.Done;
            submission.Status = LabSubmissionStatus.Done;
            logger.LogInformation("Lab job {JobId} done — {Passed}/{Total} passed",
                jobId, results.Count(r => r.Passed), results.Count);
        }
        catch (DockerComposeTimeoutException ex)
        {
            logger.LogWarning(ex, "Lab job {JobId} timed out", jobId);
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
            uow.ClearChanges();
            job        = (await uow.LabGradingJobs.GetByIdAsync(jobId))!;
            submission = (await uow.LabSubmissions.GetByIdAsync(job.LabSubmissionId))!;
            job.Status        = LabGradingJobStatus.Failed;
            job.ErrorMessage  = ex.Message;
            submission.Status = LabSubmissionStatus.Error;
        }
        finally
        {
            job.FinishedAt = DateTime.UtcNow;
            uow.LabGradingJobs.Update(job);
            uow.LabSubmissions.Update(submission);

            try { await docker.StopAsync(jobId); }
            catch (Exception ex) { logger.LogError(ex, "Docker cleanup failed for job {JobId}", jobId); }

            try { await uow.SaveChangesAsync(CancellationToken.None); }
            catch (Exception ex) { logger.LogError(ex, "Final save failed for job {JobId}", jobId); }
        }
    }
}
