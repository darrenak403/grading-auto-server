using GradingSystem.Application.Interfaces;
using GradingSystem.Application.Services;
using GradingSystem.Domain.Entities;
using GradingSystem.Worker.Options;
using Microsoft.Extensions.Options;

namespace GradingSystem.Worker.Services;

// No per-assignment lock here on purpose: each submission already runs against its own
// isolated database (grading_{jobId:N}) and sandbox directory, so two submissions of the
// same assignment ("mã đề") can safely grade concurrently. The one shared resource — TCP
// ports — is guarded separately by ArtifactRunner's port reservation registry. Re-verify
// both assumptions before reintroducing serialization here.
public class GradingPipeline(
    IServiceScopeFactory scopeFactory,
    ArtifactRunner artifactRunner,
    TestRunner testRunner,
    IOptions<WorkerOptions> opts,
    ILogger<GradingPipeline> logger)
{
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

        // Created up front (sandbox path only, no I/O) so that any processes/ports RunAsync
        // manages to start before a timeout fires are still reachable by CleanupAsync below —
        // RunAsync mutates this same instance instead of building+returning its own.
        var ctx = artifactRunner.CreateContext(job);

        // Scoped strictly to this single ProcessAsync call: GradingPipeline is a singleton,
        // so the linked CTS/timer must never be stored on shared state, only this local var.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(opts.Value.SubmissionTimeoutSeconds));

        try
        {
            job.Status    = JobStatus.Running;
            job.StartedAt = DateTime.UtcNow;
            uow.GradingJobs.Update(job);
            await uow.SaveChangesAsync(ct);

            logger.LogInformation("Processing job {JobId} for submission {SubId} (round: {Round})",
                job.Id, submission.Id, job.GradingRound);

            await artifactRunner.RunAsync(job, questions, ctx, timeoutCts.Token);
            await testRunner.RunAsync(job, ctx, uow, timeoutCts.Token);

            job.Status        = JobStatus.Done;
            submission.Status = SubmissionStatus.Done;

            logger.LogInformation("Job {JobId} completed successfully", job.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job {JobId} failed", job.Id);
            
            // Clear any partially tracked results from TestRunner to prevent duplicate key constraint violations
            uow.ClearChanges();
            
            // Re-fetch job and submission because ClearChanges detached them
            job = await uow.GradingJobs.GetByIdAsync(job.Id);
            submission = await uow.Submissions.GetByIdAsync(submission.Id);
            
            if (job != null && submission != null)
            {
                job.Status        = JobStatus.Failed;
                job.ErrorMessage  = ex.Message;
                submission.Status = SubmissionStatus.Error;
            }

            // Insert 0-score results for any question without a result for this job
            var existingResults = await uow.QuestionResults.FindAsync(r => r.GradingJobId == job.Id);
            var gradedIds = existingResults.Select(r => r.QuestionId).ToHashSet();

            // Only timeoutCts firing (and not the outer token) means the student app genuinely
            // ran past SubmissionTimeoutSeconds — a worker shutdown cancels the outer ct too,
            // so that case must not be reported with the same "timed out" wording.
            var isTimeout = timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested;
            var setupNote = isTimeout
                ? BulkUploadService.MakeNote($"Quá thời gian xử lý (timeout sau {opts.Value.SubmissionTimeoutSeconds}s) — bài làm đã bị dừng")
                : BulkUploadService.MakeNote($"Lỗi setup: {ex.Message}");
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
            try { await artifactRunner.CleanupAsync(ctx); }
            catch (Exception ex) { logger.LogWarning(ex, "Cleanup failed for job {JobId}", job.Id); }

            // Delete artifact zip immediately after grading to free storage
            DeleteArtifact(submission);

            job.FinishedAt = DateTime.UtcNow;
            uow.GradingJobs.Update(job);
            uow.Submissions.Update(submission);

            // CancellationToken.None: on worker shutdown ct is already cancelled by this point,
            // and this save is what persists the Failed status just set in the catch block above —
            // using ct here would let shutdown itself cancel away the very status write that
            // records the shutdown's effect, leaving the job stuck in Running after restart.
            await uow.SaveChangesAsync(CancellationToken.None);
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
