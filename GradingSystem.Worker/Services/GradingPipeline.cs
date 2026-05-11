using System.Collections.Concurrent;
using GradingSystem.Application.Interfaces;
using GradingSystem.Application.Services;
using GradingSystem.Domain.Entities;

namespace GradingSystem.Worker.Services;

public class GradingPipeline(
    IServiceScopeFactory scopeFactory,
    ArtifactRunner artifactRunner,
    TestRunner testRunner,
    ILogger<GradingPipeline> logger)
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public async Task ProcessAsync(Guid gradingJobId, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var job = await uow.GradingJobs.GetByIdAsync(gradingJobId);
        if (job is null)
        {
            logger.LogWarning("GradingJob {Id} not found", gradingJobId);
            return;
        }

        if (job.Status != JobStatus.Pending)
        {
            logger.LogInformation("GradingJob {Id} is already {Status} — skipping", gradingJobId, job.Status);
            return;
        }

        var submission = await uow.Submissions.GetByIdAsync(job.SubmissionId);
        if (submission is null)
        {
            logger.LogError("Submission {Id} not found for job {JobId}", job.SubmissionId, job.Id);
            return;
        }

        var assignment = await uow.Assignments.GetByIdAsync(submission.AssignmentId);
        if (assignment is null)
        {
            logger.LogError("Assignment {Id} not found", submission.AssignmentId);
            return;
        }

        submission.Assignment = assignment;
        job.Submission = submission;

        var questions = (await uow.Questions.FindAsync(q => q.AssignmentId == assignment.Id))
                        .OrderBy(q => q.CreatedAt).ToList();

        // Per-assignment lock prevents concurrent SQL Server setup for the same mã đề
        var semaphore = _locks.GetOrAdd(assignment.Id, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);

        StudentContext? ctx = null;
        try
        {
            job.Status    = JobStatus.Running;
            job.StartedAt = DateTime.UtcNow;
            uow.GradingJobs.Update(job);
            await uow.SaveChangesAsync(ct);

            logger.LogInformation("Processing job {JobId} for submission {SubId} (round: {Round})",
                job.Id, submission.Id, job.GradingRound);

            ctx = await artifactRunner.RunAsync(job, questions, ct);
            await testRunner.RunAsync(job, ctx, uow, ct);

            job.Status        = JobStatus.Done;
            submission.Status = SubmissionStatus.Done;

            logger.LogInformation("Job {JobId} completed successfully", job.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job {JobId} failed", job.Id);
            job.Status        = JobStatus.Failed;
            job.ErrorMessage  = ex.Message;
            submission.Status = SubmissionStatus.Error;

            // Insert 0-score results for any question without a result for this job
            var existingResults = await uow.QuestionResults.FindAsync(r => r.GradingJobId == job.Id);
            var gradedIds = existingResults.Select(r => r.QuestionId).ToHashSet();
            var setupNote = BulkUploadService.MakeNote($"Lỗi setup: {ex.Message}");
            foreach (var q in questions.Where(q => !gradedIds.Contains(q.Id)))
            {
                await uow.QuestionResults.AddAsync(new QuestionResult
                {
                    SubmissionId = submission.Id,
                    GradingJobId = job.Id,
                    QuestionId   = q.Id,
                    Score        = 0,
                    MaxScore     = q.MaxScore,
                    Detail       = setupNote,
                });
            }
        }
        finally
        {
            if (ctx is not null)
            {
                try { await artifactRunner.CleanupAsync(ctx); }
                catch (Exception ex) { logger.LogWarning(ex, "Cleanup failed for job {JobId}", job.Id); }
            }

            // Delete artifact zip immediately after grading to free storage
            DeleteArtifact(submission);

            job.FinishedAt = DateTime.UtcNow;
            uow.GradingJobs.Update(job);
            uow.Submissions.Update(submission);
            await uow.SaveChangesAsync(ct);

            semaphore.Release();
        }
    }

    private void DeleteArtifact(Submission submission)
    {
        if (string.IsNullOrEmpty(submission.ArtifactZipPath)) return;
        try
        {
            var dir = Path.GetDirectoryName(submission.ArtifactZipPath);
            if (dir != null && Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
            submission.ArtifactZipPath = string.Empty;
            logger.LogInformation("Deleted artifact for submission {Id}", submission.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete artifact for submission {Id}", submission.Id);
        }
    }
}
