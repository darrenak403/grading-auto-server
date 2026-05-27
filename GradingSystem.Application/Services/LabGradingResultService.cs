using GradingSystem.Application.DTOs;
using GradingSystem.Application.Exceptions;
using GradingSystem.Application.Interfaces;
using GradingSystem.Domain.Entities;

namespace GradingSystem.Application.Services;

public class LabGradingResultService(IUnitOfWork uow) : ILabGradingResultService
{
    public async Task<LabGradingResultDto?> GetResultsBySubmissionAsync(Guid submissionId, CancellationToken ct = default)
    {
        var submission = await uow.LabSubmissions.GetByIdAsync(submissionId);
        if (submission is null) return null;

        var jobs = await uow.LabGradingJobs.FindAsync(j => j.LabSubmissionId == submissionId);
        var latestJob = jobs.OrderByDescending(j => j.CreatedAt).FirstOrDefault();

        if (latestJob is null)
            return new LabGradingResultDto
            {
                SubmissionId = submissionId,
                StudentCode = submission.StudentCode,
                SubmissionStatus = submission.Status.ToString()
            };

        var results = await uow.LabTestCaseResults.FindAsync(r => r.LabGradingJobId == latestJob.Id);
        var resultList = results.OrderBy(r => r.CreatedAt).ToList();

        var testCaseIds = resultList.Select(r => r.LabTestCaseId).ToHashSet();
        var testCases = (await uow.LabTestCases.FindAsync(t => testCaseIds.Contains(t.Id)))
            .ToDictionary(t => t.Id);

        return new LabGradingResultDto
        {
            SubmissionId = submissionId,
            StudentCode = submission.StudentCode,
            SubmissionStatus = submission.Status.ToString(),
            LatestJobId = latestJob.Id,
            JobStatus = latestJob.Status.ToString(),
            TotalScore = resultList.Sum(r => r.ManualOverrideScore ?? r.AwardedScore),
            Results = resultList.Select(r =>
            {
                testCases.TryGetValue(r.LabTestCaseId, out var tc);
                return MapResult(r, tc);
            }).ToList()
        };
    }

    public async Task<LabTestCaseResultDto> AdjustScoreAsync(Guid submissionId, Guid resultId, decimal score, string reason, CancellationToken ct = default)
    {
        var result = await uow.LabTestCaseResults.GetByIdAsync(resultId)
            ?? throw new NotFoundException($"LabTestCaseResult '{resultId}' not found.");

        // Verify the result belongs to a job owned by the specified submission
        var job = await uow.LabGradingJobs.GetByIdAsync(result.LabGradingJobId)
            ?? throw new NotFoundException($"LabGradingJob for result '{resultId}' not found.");
        if (job.LabSubmissionId != submissionId)
            throw new NotFoundException($"Result '{resultId}' does not belong to submission '{submissionId}'.");
        if (score < 0)
            throw new BadRequestException("Score cannot be negative.");
        var tc = await uow.LabTestCases.GetByIdAsync(result.LabTestCaseId);
        if (tc is not null && score > tc.Score)
            throw new BadRequestException($"Score {score} exceeds maximum {tc.Score} for this test case.");
        result.ManualOverrideScore = score;
        result.OverrideReason = reason;
        result.UpdatedAt = DateTime.UtcNow;
        uow.LabTestCaseResults.Update(result);
        await uow.SaveChangesAsync(ct);
        return MapResult(result, tc);
    }

    private static LabTestCaseResultDto MapResult(LabTestCaseResult r, LabTestCase? tc) => new()
    {
        Id = r.Id,
        LabTestCaseId = r.LabTestCaseId,
        HttpMethod = tc?.HttpMethod ?? "",
        UrlTemplate = tc?.UrlTemplate ?? "",
        Passed = r.Passed,
        AwardedScore = r.AwardedScore,
        ActualStatusCode = r.ActualStatusCode,
        ActualResponse = r.ActualResponse,
        ErrorMessage = r.ErrorMessage,
        ManualOverrideScore = r.ManualOverrideScore,
        OverrideReason = r.OverrideReason
    };
}
