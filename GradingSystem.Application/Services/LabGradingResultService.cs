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

    public async Task<LabGradingResultDto> ImportCustomResultAsync(
        Guid submissionId,
        ImportLabCustomResultRequest request,
        CancellationToken ct = default)
    {
        var target = await uow.LabSubmissions.GetByIdAsync(submissionId)
            ?? throw new NotFoundException($"LabSubmission '{submissionId}' not found.");
        if (request.Score < 0)
            throw new BadRequestException("Score cannot be negative.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new BadRequestException("Reason is required.");

        var activeJobs = await uow.LabGradingJobs.FindAsync(j =>
            j.LabSubmissionId == target.Id &&
            (j.Status == LabGradingJobStatus.Pending || j.Status == LabGradingJobStatus.Running));
        if (activeJobs.Any())
            throw new BadRequestException("Submission is already being graded.");

        var (sourceResults, testCases, maxScore) = request.TemplateSubmissionId.HasValue
            ? await GetTemplateSourceAsync(target, request.TemplateSubmissionId.Value)
            : await GetBuiltInSampleSourceAsync(target.LabAssignmentId);

        if (request.Score > maxScore)
            throw new BadRequestException($"Score must be in range [0..{maxScore}].");

        var now = DateTime.UtcNow;
        var customJob = new LabGradingJob
        {
            LabSubmissionId = target.Id,
            Status = LabGradingJobStatus.Done,
            StartedAt = now,
            FinishedAt = now,
        };
        await uow.LabGradingJobs.AddAsync(customJob);

        var reason = request.Reason.Trim();
        var customScores = BuildCustomScores(sourceResults, testCases, maxScore, request.Score);
        var created = new List<LabTestCaseResult>();
        for (var i = 0; i < sourceResults.Count; i++)
        {
            var source = sourceResults[i];
            testCases.TryGetValue(source.LabTestCaseId, out var tc);
            var baseScore = GetBaseScore(source);
            var customScore = customScores[i];
            var isDeducted = customScore < baseScore;
            var result = new LabTestCaseResult
            {
                LabGradingJobId = customJob.Id,
                LabTestCaseId = source.LabTestCaseId,
                Passed = source.Passed && !isDeducted,
                AwardedScore = source.AwardedScore,
                ActualStatusCode = isDeducted ? 404 : source.ActualStatusCode,
                ActualResponse = isDeducted ? null : source.ActualResponse,
                ErrorMessage = isDeducted
                    ? BuildNotFoundErrorMessage(tc)
                    : source.ErrorMessage,
                ManualOverrideScore = customScore,
                OverrideReason = reason,
            };
            await uow.LabTestCaseResults.AddAsync(result);
            created.Add(result);
        }

        var roundingDelta = request.Score - created.Sum(EffectiveScore);
        if (roundingDelta != 0)
        {
            var last = created[^1];
            last.ManualOverrideScore = (last.ManualOverrideScore ?? last.AwardedScore) + roundingDelta;
        }

        target.Status = LabSubmissionStatus.Done;
        target.UpdatedAt = now;
        uow.LabSubmissions.Update(target);
        await uow.SaveChangesAsync(ct);

        return new LabGradingResultDto
        {
            SubmissionId = target.Id,
            StudentCode = target.StudentCode,
            SubmissionStatus = target.Status.ToString(),
            LatestJobId = customJob.Id,
            JobStatus = customJob.Status.ToString(),
            TotalScore = created.Sum(EffectiveScore),
            Results = created.Select(r =>
            {
                testCases.TryGetValue(r.LabTestCaseId, out var tc);
                return MapResult(r, tc);
            }).ToList()
        };
    }

    private static List<decimal> BuildCustomScores(
        IReadOnlyList<LabTestCaseResult> sourceResults,
        IReadOnlyDictionary<Guid, LabTestCase> testCases,
        decimal maxScore,
        decimal targetScore)
    {
        var scores = sourceResults
            .Select(GetBaseScore)
            .ToList();
        var deduction = maxScore - targetScore;
        if (deduction <= 0) return scores;

        var candidates = BuildDeductionCandidates(scores, sourceResults, testCases);
        if (candidates.Count == 0) return scores;

        var selected = ChooseDeductionTargets(candidates, deduction);
        var remaining = deduction;
        foreach (var item in selected)
        {
            if (remaining <= 0) break;
            var deduct = Math.Min(scores[item.Index], remaining);
            scores[item.Index] = decimal.Round(scores[item.Index] - deduct, 2, MidpointRounding.AwayFromZero);
            remaining -= deduct;
        }
        if (remaining > 0)
            throw new BadRequestException("Target score is too low to apply without deducting SOURCE test cases.");

        return scores;
    }

    private static List<DeductionCandidate> BuildDeductionCandidates(
        IReadOnlyList<decimal> scores,
        IReadOnlyList<LabTestCaseResult> sourceResults,
        IReadOnlyDictionary<Guid, LabTestCase> testCases)
    {
        var all = scores
            .Select((score, index) => new DeductionCandidate(score, index))
            .Where(x => x.Score > 0)
            .ToList();

        var selected = new List<DeductionCandidate>();
        AddCandidates(selected, all.Where(x => IsProtectedBusinessApi(sourceResults[x.Index], testCases)));
        AddCandidates(selected, all.Where(x => IsRefreshTokenTestCase(sourceResults[x.Index], testCases)));
        AddCandidates(selected, all.Where(x =>
            IsApiTestCase(sourceResults[x.Index], testCases) &&
            !IsLoginBootstrapTestCase(sourceResults[x.Index], testCases)));
        AddCandidates(selected, all.Where(x => IsApiTestCase(sourceResults[x.Index], testCases)));

        return selected;
    }

    private static void AddCandidates(
        List<DeductionCandidate> selected,
        IEnumerable<DeductionCandidate> candidates)
    {
        var existing = selected.Select(x => x.Index).ToHashSet();
        selected.AddRange(candidates
            .Where(x => !existing.Contains(x.Index))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Index));
    }

    private static List<DeductionCandidate> ChooseDeductionTargets(
        IReadOnlyList<DeductionCandidate> candidates,
        decimal deduction)
    {
        var preferredCount = candidates.Count >= 3 ? 3 : Math.Min(2, candidates.Count);
        for (var count = preferredCount; count >= 1; count--)
        {
            var picked = candidates.Take(count).ToList();
            if (picked.Sum(x => x.Score) >= deduction)
                return picked;
        }

        var expanded = new List<DeductionCandidate>();
        var capacity = 0m;
        foreach (var candidate in candidates)
        {
            expanded.Add(candidate);
            capacity += candidate.Score;
            if (capacity >= deduction) break;
        }

        return expanded;
    }

    private readonly record struct DeductionCandidate(decimal Score, int Index);

    private static decimal GetBaseScore(LabTestCaseResult source) =>
        EffectiveScore(source);

    private static bool IsApiTestCase(
        LabTestCaseResult source,
        IReadOnlyDictionary<Guid, LabTestCase> testCases) =>
        !testCases.TryGetValue(source.LabTestCaseId, out var tc) ||
        !tc.HttpMethod.Equals("SOURCE", StringComparison.OrdinalIgnoreCase);

    private static bool IsLoginBootstrapTestCase(
        LabTestCaseResult source,
        IReadOnlyDictionary<Guid, LabTestCase> testCases)
    {
        if (!testCases.TryGetValue(source.LabTestCaseId, out var tc))
            return false;

        var url = tc.UrlTemplate.Trim().ToLowerInvariant();
        return url.Contains("/auth/login") ||
               url.Contains("/auth/register") ||
               url.Contains("/login");
    }

    private static bool IsRefreshTokenTestCase(
        LabTestCaseResult source,
        IReadOnlyDictionary<Guid, LabTestCase> testCases)
    {
        if (!testCases.TryGetValue(source.LabTestCaseId, out var tc))
            return false;

        var url = tc.UrlTemplate.Trim().ToLowerInvariant();
        return url.Contains("/auth/refresh-token") ||
               url.Contains("/refresh-token");
    }

    private static bool IsProtectedBusinessApi(
        LabTestCaseResult source,
        IReadOnlyDictionary<Guid, LabTestCase> testCases) =>
        IsApiTestCase(source, testCases) &&
        !IsLoginBootstrapTestCase(source, testCases) &&
        !IsRefreshTokenTestCase(source, testCases);

    private static string BuildNotFoundErrorMessage(LabTestCase? testCase)
    {
        var expectedStatus = testCase?.ExpectedStatusCode ?? 200;
        return $"Expected status {expectedStatus}, got 404.";
    }

    private async Task<(
        List<LabTestCaseResult> SourceResults,
        Dictionary<Guid, LabTestCase> TestCases,
        decimal MaxScore)> GetTemplateSourceAsync(LabSubmission target, Guid templateSubmissionId)
    {
        var template = await uow.LabSubmissions.GetByIdAsync(templateSubmissionId)
            ?? throw new NotFoundException($"Template LabSubmission '{templateSubmissionId}' not found.");

        if (target.Id == template.Id)
            throw new BadRequestException("Template submission must be different from the target submission.");
        if (target.LabAssignmentId != template.LabAssignmentId)
            throw new BadRequestException("Template and target submissions must belong to the same lab assignment.");

        var templateJob = await GetLatestDoneJobAsync(template.Id)
            ?? throw new BadRequestException("Template submission has no completed results.");
        var templateResults = (await uow.LabTestCaseResults.FindAsync(r => r.LabGradingJobId == templateJob.Id))
            .OrderBy(r => r.CreatedAt)
            .ToList();
        if (templateResults.Count == 0)
            throw new BadRequestException("Template submission has no completed results.");

        var testCaseIds = templateResults.Select(r => r.LabTestCaseId).ToHashSet();
        var testCases = (await uow.LabTestCases.FindAsync(t => testCaseIds.Contains(t.Id)))
            .ToDictionary(t => t.Id);
        var rubricMaxScore = templateResults.Sum(r =>
            testCases.TryGetValue(r.LabTestCaseId, out var tc) ? tc.Score : r.AwardedScore);
        var templateScore = templateResults.Sum(EffectiveScore);
        var expectedFullScore = GetExpectedFullScore(rubricMaxScore);

        if (expectedFullScore <= 0 || templateScore < expectedFullScore)
            throw new BadRequestException("Template submission must have a full-score result.");

        var sourceResults = CloneSourceResults(templateResults);
        NormalizeSourceScoresToMax(sourceResults, testCases, expectedFullScore);

        return (sourceResults, testCases, expectedFullScore);
    }

    private async Task<(
        List<LabTestCaseResult> SourceResults,
        Dictionary<Guid, LabTestCase> TestCases,
        decimal MaxScore)> GetBuiltInSampleSourceAsync(Guid labAssignmentId)
    {
        var testCases = (await uow.LabTestCases.FindAsync(t =>
                t.LabAssignmentId == labAssignmentId && t.Status == LabTestCaseStatus.Approved))
            .OrderBy(t => t.Order)
            .ThenBy(t => t.CreatedAt)
            .ToList();
        if (testCases.Count == 0)
            throw new BadRequestException("Lab assignment has no approved test cases.");

        var rubricMaxScore = testCases.Sum(t => t.Score);
        var maxScore = GetExpectedFullScore(rubricMaxScore);
        if (maxScore <= 0)
            throw new BadRequestException("Lab assignment has no positive max score.");

        var sourceResults = testCases.Select(t => new LabTestCaseResult
        {
            LabTestCaseId = t.Id,
            Passed = true,
            AwardedScore = t.Score,
            ActualStatusCode = t.ExpectedStatusCode,
            ActualResponse = BuildSampleActualResponse(t),
            ErrorMessage = null,
        }).ToList();

        var testCaseMap = testCases.ToDictionary(t => t.Id);
        NormalizeSourceScoresToMax(sourceResults, testCaseMap, maxScore);

        return (sourceResults, testCaseMap, maxScore);
    }

    private static decimal GetExpectedFullScore(decimal rubricMaxScore) =>
        rubricMaxScore > 10m ? 10m : rubricMaxScore;

    private static List<LabTestCaseResult> CloneSourceResults(IEnumerable<LabTestCaseResult> results) =>
        results.Select(r => new LabTestCaseResult
        {
            LabTestCaseId = r.LabTestCaseId,
            Passed = r.Passed,
            AwardedScore = EffectiveScore(r),
            ActualStatusCode = r.ActualStatusCode,
            ActualResponse = r.ActualResponse,
            ErrorMessage = r.ErrorMessage,
        }).ToList();

    private static void NormalizeSourceScoresToMax(
        List<LabTestCaseResult> sourceResults,
        IReadOnlyDictionary<Guid, LabTestCase> testCases,
        decimal maxScore)
    {
        var surplus = sourceResults.Sum(EffectiveScore) - maxScore;
        if (surplus <= 0) return;

        surplus = DeductScoreSurplus(sourceResults, testCases, surplus, IsBonusTestCase);
        if (surplus <= 0) return;

        surplus = DeductScoreSurplus(
            sourceResults,
            testCases,
            surplus,
            (result, map) => !IsApiTestCase(result, map));
        if (surplus <= 0) return;

        _ = DeductScoreSurplus(
            sourceResults,
            testCases,
            surplus,
            (_, _) => true);
    }

    private static decimal DeductScoreSurplus(
        List<LabTestCaseResult> sourceResults,
        IReadOnlyDictionary<Guid, LabTestCase> testCases,
        decimal surplus,
        Func<LabTestCaseResult, IReadOnlyDictionary<Guid, LabTestCase>, bool> predicate)
    {
        foreach (var result in sourceResults.Where(r => predicate(r, testCases)))
        {
            if (surplus <= 0) break;

            var current = EffectiveScore(result);
            var deduct = Math.Min(current, surplus);
            result.AwardedScore = decimal.Round(current - deduct, 2, MidpointRounding.AwayFromZero);
            result.ManualOverrideScore = null;
            surplus -= deduct;
        }

        return surplus;
    }

    private static bool IsBonusTestCase(
        LabTestCaseResult source,
        IReadOnlyDictionary<Guid, LabTestCase> testCases)
    {
        if (!testCases.TryGetValue(source.LabTestCaseId, out var tc))
            return false;

        return (tc.Description?.Contains("Bonus", StringComparison.OrdinalIgnoreCase) ?? false) ||
               tc.UrlTemplate.Contains("Bonus", StringComparison.OrdinalIgnoreCase);
    }

    private static string? BuildSampleActualResponse(LabTestCase testCase)
    {
        if (!string.IsNullOrWhiteSpace(testCase.ExpectJson))
            return testCase.ExpectJson;
        if (testCase.HttpMethod.Equals("SOURCE", StringComparison.OrdinalIgnoreCase))
            return "Sample result";
        return null;
    }

    private async Task<LabGradingJob?> GetLatestDoneJobAsync(Guid submissionId)
    {
        var jobs = await uow.LabGradingJobs.FindAsync(j =>
            j.LabSubmissionId == submissionId && j.Status == LabGradingJobStatus.Done);
        return jobs
            .OrderByDescending(j => j.FinishedAt ?? j.CreatedAt)
            .ThenByDescending(j => j.Id)
            .FirstOrDefault();
    }

    private static decimal EffectiveScore(LabTestCaseResult result) =>
        result.ManualOverrideScore ?? result.AwardedScore;

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
