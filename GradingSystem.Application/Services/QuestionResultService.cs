using System.Text.Json;
using GradingSystem.Application.Common;
using GradingSystem.Application.DTOs;
using GradingSystem.Application.Exceptions;
using GradingSystem.Application.Interfaces;

namespace GradingSystem.Application.Services;

public class QuestionResultService(IUnitOfWork unitOfWork) : IQuestionResultService
{
    public async Task<QuestionResultDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await unitOfWork.QuestionResults.GetByIdAsync(id);
        return entity is null ? null : await BuildDtoAsync(entity);
    }

    public async Task<IReadOnlyList<QuestionResultDto>> GetBySubmissionIdAsync(
        Guid submissionId, CancellationToken ct = default)
    {
        // Resolve latest Done grading job for this submission
        var jobs = (await unitOfWork.GradingJobs.FindAsync(j => j.SubmissionId == submissionId))
            .Where(j => j.Status == Domain.Entities.JobStatus.Done)
            .OrderByDescending(j => j.FinishedAt)
            .ToList();

        IEnumerable<Domain.Entities.QuestionResult> entities;

        if (jobs.Count > 0)
        {
            var latestJobId = jobs.First().Id;
            entities = await unitOfWork.QuestionResults.FindAsync(
                r => r.SubmissionId == submissionId && r.GradingJobId == latestJobId);
        }
        else
        {
            // Fall back: return any results (e.g. missing-submission 0-score rows without a job)
            entities = await unitOfWork.QuestionResults.FindAsync(
                r => r.SubmissionId == submissionId && r.GradingJobId == null);
        }

        var list = entities.OrderBy(r => r.CreatedAt).ToList();
        if (list.Count == 0) return [];

        var submission = await unitOfWork.Submissions.GetByIdAsync(submissionId);
        var studentCode = submission?.StudentCode ?? string.Empty;
        var studentId   = StudentCode.ParseId(studentCode);

        var questionIds = list.Select(r => r.QuestionId).Distinct().ToList();
        var questions = (await unitOfWork.Questions.FindAsync(q => questionIds.Contains(q.Id)))
            .ToDictionary(q => q.Id, q => q.Title);

        return list.Select(e => MapDto(e, studentCode, studentId,
            questions.GetValueOrDefault(e.QuestionId, string.Empty))).ToList();
    }

    public async Task<QuestionResultDto> AdjustAsync(Guid id, AdjustScoreRequest req, CancellationToken ct = default)
    {
        var result = await unitOfWork.QuestionResults.GetByIdAsync(id)
            ?? throw new NotFoundException($"QuestionResult '{id}' not found.");

        if (req.AdjustedScore < 0m || req.AdjustedScore > result.MaxScore)
        {
            throw new BadRequestException($"AdjustedScore must be in range [0..{result.MaxScore}].");
        }

        if (string.IsNullOrWhiteSpace(req.AdjustReason))
        {
            throw new BadRequestException("AdjustReason is required.");
        }

        result.AdjustedScore = req.AdjustedScore;
        result.AdjustReason = req.AdjustReason.Trim();
        result.AdjustedBy = req.AdjustedBy?.Trim();
        result.AdjustedAt = DateTime.UtcNow;

        unitOfWork.QuestionResults.Update(result);
        await unitOfWork.SaveChangesAsync(ct);

        return await BuildDtoAsync(result);
    }

    public async Task<QuestionResultDto> ClearAdjustAsync(Guid id, CancellationToken ct = default)
    {
        var result = await unitOfWork.QuestionResults.GetByIdAsync(id)
            ?? throw new NotFoundException($"QuestionResult '{id}' not found.");

        result.AdjustedScore = null;
        result.AdjustReason = null;
        result.AdjustedBy = null;
        result.AdjustedAt = null;

        unitOfWork.QuestionResults.Update(result);
        await unitOfWork.SaveChangesAsync(ct);

        return await BuildDtoAsync(result);
    }

    private async Task<QuestionResultDto> BuildDtoAsync(Domain.Entities.QuestionResult entity)
    {
        var submission = await unitOfWork.Submissions.GetByIdAsync(entity.SubmissionId)
            ?? throw new NotFoundException($"Submission '{entity.SubmissionId}' not found.");

        var question = await unitOfWork.Questions.GetByIdAsync(entity.QuestionId);
        return MapDto(entity, submission.StudentCode, StudentCode.ParseId(submission.StudentCode),
            question?.Title ?? string.Empty);
    }

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static QuestionResultDto MapDto(Domain.Entities.QuestionResult e, string studentCode, string studentId,
        string questionTitle) => new()
    {
        Id              = e.Id,
        SubmissionId    = e.SubmissionId,
        QuestionId      = e.QuestionId,
        QuestionTitle   = questionTitle,
        StudentCode     = studentCode,
        StudentId       = studentId,
        Score           = e.Score,
        MaxScore        = e.MaxScore,
        FinalScore      = e.FinalScore,
        TestCaseResults = string.IsNullOrEmpty(e.Detail)
            ? null
            : JsonSerializer.Deserialize<List<TestCaseResult>>(e.Detail, _jsonOpts),
        AdjustedScore   = e.AdjustedScore,
        AdjustReason    = e.AdjustReason,
        AdjustedBy      = e.AdjustedBy,
        AdjustedAt      = e.AdjustedAt,
    };
}
