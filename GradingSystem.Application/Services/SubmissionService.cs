using GradingSystem.Application.Common;
using GradingSystem.Application.DTOs;
using GradingSystem.Application.Exceptions;
using GradingSystem.Application.Interfaces;
using GradingSystem.Domain.Entities;

namespace GradingSystem.Application.Services;

public class SubmissionService(IUnitOfWork uow) : ISubmissionService
{
    public async Task<SubmissionDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await uow.Submissions.GetByIdAsync(id);
        if (entity is null) return null;

        var results = await uow.QuestionResults.FindAsync(r => r.SubmissionId == id);
        return MapWithScore(entity, results.ToList());
    }

    public async Task<IReadOnlyList<SubmissionDto>> GetByAssignmentIdAsync(
        Guid assignmentId, string? studentCode, CancellationToken ct = default)
    {
        _ = await uow.Assignments.GetByIdAsync(assignmentId)
            ?? throw new NotFoundException($"Assignment '{assignmentId}' not found.");

        var submissions = await uow.Submissions.FindAsync(s => s.AssignmentId == assignmentId);

        if (!string.IsNullOrWhiteSpace(studentCode))
            submissions = submissions.Where(s =>
                s.StudentCode.Contains(studentCode, StringComparison.OrdinalIgnoreCase));

        var list = submissions.OrderBy(s => s.CreatedAt).ToList();

        // Batch-load all results for these submissions
        var ids = list.Select(s => s.Id).ToHashSet();
        var allResults = await uow.QuestionResults.FindAsync(r => ids.Contains(r.SubmissionId));
        var resultsBySubmission = allResults.GroupBy(r => r.SubmissionId)
                                            .ToDictionary(g => g.Key, g => g.ToList());

        return list.Select(s =>
        {
            resultsBySubmission.TryGetValue(s.Id, out var res);
            return MapWithScore(s, res);
        }).ToList();
    }

    public async Task<IEnumerable<QuestionResultDto>> GetResultsAsync(Guid submissionId, CancellationToken ct = default)
    {
        var submission = await uow.Submissions.GetByIdAsync(submissionId)
            ?? throw new NotFoundException($"Submission '{submissionId}' not found.");

        var results = await uow.QuestionResults.FindAsync(r => r.SubmissionId == submissionId);

        return results
            .OrderBy(r => r.CreatedAt)
            .Select(r => MapResult(r, submission.StudentCode));
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

    private static SubmissionDto Map(Submission e) => MapWithScore(e, null);

    private static SubmissionDto MapWithScore(Submission e, IList<QuestionResult>? results) => new()
    {
        Id              = e.Id,
        AssignmentId    = e.AssignmentId,
        StudentCode     = e.StudentCode,
        ArtifactZipPath = e.ArtifactZipPath,
        Status          = e.Status,
        CreatedAt       = e.CreatedAt,
        TotalScore      = results is { Count: > 0 } ? results.Sum(r => r.FinalScore) : null,
        MaxScore        = results is { Count: > 0 } ? results.Sum(r => r.MaxScore)   : null,
    };

    private static readonly System.Text.Json.JsonSerializerOptions _jsonOpts =
        new(System.Text.Json.JsonSerializerDefaults.Web);

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
            : System.Text.Json.JsonSerializer.Deserialize<List<Common.TestCaseResult>>(r.Detail, _jsonOpts),
        AdjustedScore = r.AdjustedScore,
        AdjustReason  = r.AdjustReason,
        AdjustedBy    = r.AdjustedBy,
        AdjustedAt    = r.AdjustedAt,
    };

}
