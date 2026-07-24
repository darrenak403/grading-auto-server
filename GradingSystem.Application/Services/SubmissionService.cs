using GradingSystem.Application.Common;
using GradingSystem.Application.DTOs;
using GradingSystem.Application.Exceptions;
using GradingSystem.Application.Interfaces;
using GradingSystem.Application.Messaging;
using GradingSystem.Domain.Entities;
using MassTransit;
using System.Text.Json;

namespace GradingSystem.Application.Services;

public class SubmissionService(IUnitOfWork uow, IPublishEndpoint publishEndpoint) : ISubmissionService
{
    public async Task<SubmissionDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await uow.Submissions.GetByIdAsync(id);
        if (entity is null) return null;

        var results = await uow.QuestionResults.FindAsync(r => r.SubmissionId == id);
        var jobs = await uow.GradingJobs.FindAsync(j => j.SubmissionId == id);
        var latestStatus = LatestJobStatus(jobs.ToList());
        return MapWithScore(entity, results.ToList(), latestStatus);
    }

    public async Task<IReadOnlyList<SubmissionDto>> GetByAssignmentIdAsync(
        Guid assignmentId, string? studentCode, string? gradingRound, CancellationToken ct = default)
    {
        _ = await uow.Assignments.GetByIdAsync(assignmentId)
            ?? throw new NotFoundException($"Assignment '{assignmentId}' not found.");

        var submissions = await uow.Submissions.FindAsync(s => s.AssignmentId == assignmentId);

        if (!string.IsNullOrWhiteSpace(studentCode))
            submissions = submissions.Where(s =>
                s.StudentCode.Contains(studentCode, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(gradingRound))
            submissions = submissions.Where(s => s.GradingRound == gradingRound);

        var list = submissions.OrderBy(s => s.CreatedAt).ToList();

        // Batch-load all results and jobs for these submissions
        var ids = list.Select(s => s.Id).ToHashSet();
        var allResults = await uow.QuestionResults.FindAsync(r => ids.Contains(r.SubmissionId));
        var resultsBySubmission = allResults.GroupBy(r => r.SubmissionId)
                                            .ToDictionary(g => g.Key, g => g.ToList());

        var allJobs = await uow.GradingJobs.FindAsync(j => ids.Contains(j.SubmissionId));
        var jobsBySubmission = allJobs.GroupBy(j => j.SubmissionId)
                                      .ToDictionary(g => g.Key, g => g.ToList());

        return list.Select(s =>
        {
            resultsBySubmission.TryGetValue(s.Id, out var res);
            jobsBySubmission.TryGetValue(s.Id, out var jobs);
            return MapWithScore(s, res, LatestJobStatus(jobs));
        }).ToList();
    }

    public async Task<IReadOnlyList<string>> GetRoundsAsync(Guid assignmentId, CancellationToken ct = default)
    {
        _ = await uow.Assignments.GetByIdAsync(assignmentId)
            ?? throw new NotFoundException($"Assignment '{assignmentId}' not found.");

        var submissions = await uow.Submissions.FindAsync(s => s.AssignmentId == assignmentId);

        return submissions
            .GroupBy(s => s.GradingRound)
            .Select(g => new { Round = g.Key, FirstSeen = g.Min(s => s.CreatedAt) })
            .OrderBy(x => x.FirstSeen)
            .Select(x => x.Round)
            .ToList();
    }

    // Most recently created grading job, regardless of status — used for round-selector
    // "latest job status" display and Retry-button gating (Failed-only, see RegradeAsync).
    private static JobStatus? LatestJobStatus(IList<GradingJob>? jobs) =>
        jobs is { Count: > 0 }
            ? jobs.OrderByDescending(j => j.CreatedAt).ThenByDescending(j => j.Id).First().Status
            : null;

    public async Task<IEnumerable<QuestionResultDto>> GetResultsAsync(Guid submissionId, CancellationToken ct = default)
    {
        var submission = await uow.Submissions.GetByIdAsync(submissionId)
            ?? throw new NotFoundException($"Submission '{submissionId}' not found.");

        var results = await uow.QuestionResults.FindAsync(r => r.SubmissionId == submissionId);

        return results
            .OrderBy(r => r.CreatedAt)
            .Select(r => MapResult(r, submission.StudentCode));
    }

    public async Task<IReadOnlyList<QuestionResultDto>> ImportCustomResultAsync(
        Guid submissionId,
        ImportCustomResultRequest request,
        CancellationToken ct = default)
    {
        var target = await uow.Submissions.GetByIdAsync(submissionId)
            ?? throw new NotFoundException($"Submission '{submissionId}' not found.");
        var template = await uow.Submissions.GetByIdAsync(request.TemplateSubmissionId)
            ?? throw new NotFoundException($"Template submission '{request.TemplateSubmissionId}' not found.");

        if (target.Id == template.Id)
            throw new BadRequestException("Template submission must be different from the target submission.");
        if (target.AssignmentId != template.AssignmentId)
            throw new BadRequestException("Template and target submissions must belong to the same assignment.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new BadRequestException("Reason is required.");

        var activeJobs = await uow.GradingJobs.FindAsync(j =>
            j.SubmissionId == target.Id && (j.Status == JobStatus.Pending || j.Status == JobStatus.Running));
        if (activeJobs.Any())
            throw new BadRequestException("Submission is already being graded.");

        var templateResults = await GetLatestCompletedResultsAsync(template.Id);
        if (templateResults.Count == 0)
            throw new BadRequestException("Template submission has no completed results.");

        var maxScore = templateResults.Sum(r => r.MaxScore);
        var templateScore = templateResults.Sum(r => r.FinalScore);
        if (maxScore <= 0 || templateScore != maxScore)
            throw new BadRequestException("Template submission must have a full-score result.");
        if (request.Score < 0 || request.Score > maxScore)
            throw new BadRequestException($"Score must be in range [0..{maxScore}].");

        var staleResults = await uow.QuestionResults.FindAsync(r => r.SubmissionId == target.Id);
        foreach (var stale in staleResults)
            uow.QuestionResults.Remove(stale);

        var now = DateTime.UtcNow;
        var customJob = new GradingJob
        {
            SubmissionId = target.Id,
            GradingRound = target.GradingRound,
            Status = JobStatus.Done,
            StartedAt = now,
            FinishedAt = now,
        };
        await uow.GradingJobs.AddAsync(customJob);

        var scale = request.Score / maxScore;
        var created = new List<QuestionResult>();
        foreach (var source in templateResults)
        {
            var result = new QuestionResult
            {
                SubmissionId = target.Id,
                GradingJobId = customJob.Id,
                QuestionId = source.QuestionId,
                Score = decimal.Round(source.MaxScore * scale, 2, MidpointRounding.AwayFromZero),
                MaxScore = source.MaxScore,
                Detail = ScaleDetail(source.Detail, scale),
                AdjustedScore = decimal.Round(source.MaxScore * scale, 2, MidpointRounding.AwayFromZero),
                AdjustReason = request.Reason.Trim(),
                AdjustedBy = request.AdjustedBy?.Trim(),
                AdjustedAt = now,
            };
            await uow.QuestionResults.AddAsync(result);
            created.Add(result);
        }

        var roundingDelta = request.Score - created.Sum(r => r.FinalScore);
        if (roundingDelta != 0)
        {
            var last = created[^1];
            last.Score += roundingDelta;
            last.AdjustedScore = last.Score;
        }

        target.Status = SubmissionStatus.Done;
        uow.Submissions.Update(target);
        await uow.SaveChangesAsync(ct);

        return created.Select(r => MapResult(r, target.StudentCode)).ToList();
    }

    private async Task<List<QuestionResult>> GetLatestCompletedResultsAsync(Guid submissionId)
    {
        var latestJob = (await uow.GradingJobs.FindAsync(
                j => j.SubmissionId == submissionId && j.Status == JobStatus.Done))
            .OrderByDescending(j => j.FinishedAt ?? j.CreatedAt)
            .ThenByDescending(j => j.Id)
            .FirstOrDefault();

        var results = latestJob is null
            ? await uow.QuestionResults.FindAsync(
                r => r.SubmissionId == submissionId && r.GradingJobId == null)
            : await uow.QuestionResults.FindAsync(
                r => r.SubmissionId == submissionId && r.GradingJobId == latestJob.Id);
        return results.OrderBy(r => r.CreatedAt).ToList();
    }

    private static string? ScaleDetail(string? detail, decimal scale)
    {
        if (string.IsNullOrWhiteSpace(detail)) return detail;

        List<TestCaseResult>? testCases;
        try
        {
            testCases = JsonSerializer.Deserialize<List<TestCaseResult>>(detail, _jsonOpts);
        }
        catch (JsonException)
        {
            throw new BadRequestException("Template submission contains invalid test-case result data.");
        }

        if (testCases is null) return detail;
        foreach (var testCase in testCases)
            testCase.AwardedScore = decimal.Round(
                testCase.AwardedScore * scale, 2, MidpointRounding.AwayFromZero);
        return JsonSerializer.Serialize(testCases, _jsonOpts);
    }

    public async Task<SubmissionDto> DeleteAsync(Guid submissionId, CancellationToken ct = default)
    {
        var submission = await uow.Submissions.GetByIdAsync(submissionId)
            ?? throw new NotFoundException($"Submission '{submissionId}' not found.");

        FileHelper.SafeDelete(submission.ArtifactZipPath);

        // Delete related grading jobs
        var jobs = await uow.GradingJobs.FindAsync(j => j.SubmissionId == submissionId);
        foreach (var job in jobs)
            uow.GradingJobs.Remove(job);

        // Delete related question results
        var results = await uow.QuestionResults.FindAsync(r => r.SubmissionId == submissionId);
        foreach (var result in results)
            uow.QuestionResults.Remove(result);

        // Delete the submission
        uow.Submissions.Remove(submission);
        await uow.SaveChangesAsync(ct);

        return Map(submission);
    }

    public async Task<GradingJobDto> RegradeAsync(Guid submissionId, CancellationToken ct = default)
    {
        var submission = await uow.Submissions.GetByIdAsync(submissionId)
            ?? throw new NotFoundException($"Submission '{submissionId}' not found.");

        if (!submission.HasArtifact || string.IsNullOrEmpty(submission.ArtifactZipPath))
            throw new BadRequestException("Submission has no artifact to grade.");

        var jobs = (await uow.GradingJobs.FindAsync(j => j.SubmissionId == submissionId)).ToList();

        // A Running job must be allowed to finish; a Pending job already represents the next run.
        // Refuse to enqueue another job (and purge results) while one is still in flight, otherwise
        // two Worker consumers could write QuestionResult rows for the same submission concurrently.
        var hasActiveJob = jobs.Any(j => j.Status == JobStatus.Pending || j.Status == JobStatus.Running);
        if (hasActiveJob)
            throw new BadRequestException("Submission is already being graded.");

        // Retry only exists to recover from an infra failure: refuse to re-run a submission
        // that has never been graded yet, or whose latest attempt already succeeded.
        if (LatestJobStatus(jobs) != JobStatus.Failed)
            throw new BadRequestException("Submission can only be retried after a failed grading attempt.");

        // Clear stale QuestionResult rows from any previous grading attempt so
        // results don't accumulate across regrades (same purge pattern as bulk TriggerGradeAsync).
        var staleResults = await uow.QuestionResults.FindAsync(r => r.SubmissionId == submissionId);
        foreach (var stale in staleResults)
            uow.QuestionResults.Remove(stale);

        var job = new GradingJob
        {
            SubmissionId = submission.Id,
            GradingRound = submission.GradingRound,
            Status       = JobStatus.Pending,
        };
        submission.Status = SubmissionStatus.Grading;
        uow.Submissions.Update(submission);
        await uow.GradingJobs.AddAsync(job);
        await uow.SaveChangesAsync(ct);
        await publishEndpoint.Publish(new GradeJobMessage(job.Id), ct);

        return new GradingJobDto
        {
            Id           = job.Id,
            SubmissionId = job.SubmissionId,
            Status       = job.Status,
            ErrorMessage = job.ErrorMessage,
            StartedAt    = job.StartedAt,
            FinishedAt   = job.FinishedAt,
        };
    }

    private static SubmissionDto Map(Submission e) => MapWithScore(e, null, null);

    private static SubmissionDto MapWithScore(
        Submission e, IList<QuestionResult>? results, JobStatus? latestJobStatus) => new()
    {
        Id              = e.Id,
        AssignmentId    = e.AssignmentId,
        StudentCode     = e.StudentCode,
        ArtifactZipPath = e.ArtifactZipPath,
        Status          = e.Status,
        CreatedAt       = e.CreatedAt,
        TotalScore      = results is { Count: > 0 } ? results.Sum(r => r.FinalScore) : null,
        MaxScore        = results is { Count: > 0 } ? results.Sum(r => r.MaxScore)   : null,
        GradingRound    = e.GradingRound,
        LatestJobStatus = latestJobStatus,
    };

    private static readonly JsonSerializerOptions _jsonOpts =
        new(JsonSerializerDefaults.Web);

    private static QuestionResultDto MapResult(QuestionResult r, string studentCode) => new()
    {
        Id            = r.Id,
        SubmissionId  = r.SubmissionId,
        QuestionId    = r.QuestionId,
        StudentCode   = studentCode,
        StudentId     = StudentCode.ParseId(studentCode),
        Score         = r.Score,
        MaxScore      = r.MaxScore,
        FinalScore    = r.FinalScore,
        TestCaseResults = string.IsNullOrEmpty(r.Detail)
            ? null
            : JsonSerializer.Deserialize<List<Common.TestCaseResult>>(r.Detail, _jsonOpts),
        AdjustedScore = r.AdjustedScore,
        AdjustReason  = r.AdjustReason,
        AdjustedBy    = r.AdjustedBy,
        AdjustedAt    = r.AdjustedAt,
    };

}
