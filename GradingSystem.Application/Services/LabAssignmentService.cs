using GradingSystem.Application.DTOs;
using GradingSystem.Application.Exceptions;
using GradingSystem.Application.Interfaces;
using GradingSystem.Domain.Entities;

namespace GradingSystem.Application.Services;

public class LabAssignmentService(IUnitOfWork uow) : ILabAssignmentService
{
    public async Task<IEnumerable<LabAssignmentDto>> ListAsync(CancellationToken ct = default)
    {
        var all = (await uow.LabAssignments.GetAllAsync()).ToList();
        var allIds = all.Select(a => a.Id).ToHashSet();

        var testCases = allIds.Any()
            ? await uow.LabTestCases.FindAsync(t => allIds.Contains(t.LabAssignmentId))
            : [];
        var submissions = allIds.Any()
            ? await uow.LabSubmissions.FindAsync(s => allIds.Contains(s.LabAssignmentId))
            : [];

        var tcCount = testCases.GroupBy(t => t.LabAssignmentId).ToDictionary(g => g.Key, g => g.Count());
        var subCount = submissions.GroupBy(s => s.LabAssignmentId).ToDictionary(g => g.Key, g => g.Count());

        var semesterIds = all.Where(a => a.SemesterId.HasValue).Select(a => a.SemesterId!.Value).ToHashSet();
        var semesters = semesterIds.Any()
            ? (await uow.Semesters.FindAsync(s => semesterIds.Contains(s.Id))).ToDictionary(s => s.Id)
            : new Dictionary<Guid, Domain.Entities.Semester>();

        return all.Select(a =>
        {
            semesters.TryGetValue(a.SemesterId ?? Guid.Empty, out var sem);
            return Map(a, tcCount.GetValueOrDefault(a.Id), subCount.GetValueOrDefault(a.Id), sem);
        });
    }

    public async Task<LabAssignmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var a = await uow.LabAssignments.GetByIdAsync(id);
        if (a is null) return null;
        var tcCount = (await uow.LabTestCases.FindAsync(t => t.LabAssignmentId == id)).Count();
        var subCount = (await uow.LabSubmissions.FindAsync(s => s.LabAssignmentId == id)).Count();
        var sem = a.SemesterId.HasValue ? await uow.Semesters.GetByIdAsync(a.SemesterId.Value) : null;
        return Map(a, tcCount, subCount, sem);
    }

    public async Task<LabAssignmentDto> CreateAsync(CreateLabAssignmentRequest req, CancellationToken ct = default)
    {
        Domain.Entities.Semester? sem = null;
        if (req.SemesterId.HasValue)
        {
            sem = await uow.Semesters.GetByIdAsync(req.SemesterId.Value)
                ?? throw new NotFoundException($"Semester '{req.SemesterId}' not found.");
        }

        var assignment = new LabAssignment { Title = req.Title, Description = req.Description, SemesterId = req.SemesterId };
        await uow.LabAssignments.AddAsync(assignment);
        await uow.SaveChangesAsync(ct);
        return Map(assignment, 0, 0, sem);
    }

    public async Task<LabAssignmentDto> UpdateAsync(Guid id, UpdateLabAssignmentRequest req, CancellationToken ct = default)
    {
        var assignment = await uow.LabAssignments.GetByIdAsync(id)
            ?? throw new NotFoundException($"LabAssignment '{id}' not found.");

        Domain.Entities.Semester? sem = null;
        if (req.SemesterId.HasValue)
        {
            sem = await uow.Semesters.GetByIdAsync(req.SemesterId.Value)
                ?? throw new NotFoundException($"Semester '{req.SemesterId}' not found.");
        }

        assignment.Title = req.Title;
        assignment.Description = req.Description;
        assignment.SemesterId = req.SemesterId;
        assignment.UpdatedAt = DateTime.UtcNow;
        uow.LabAssignments.Update(assignment);
        await uow.SaveChangesAsync(ct);
        var tcCount = (await uow.LabTestCases.FindAsync(t => t.LabAssignmentId == id)).Count();
        var subCount = (await uow.LabSubmissions.FindAsync(s => s.LabAssignmentId == id)).Count();
        return Map(assignment, tcCount, subCount, sem);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var assignment = await uow.LabAssignments.GetByIdAsync(id)
            ?? throw new NotFoundException($"LabAssignment '{id}' not found.");
        uow.LabAssignments.Remove(assignment);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<LabTestCaseDto>> GetTestCasesAsync(Guid id, CancellationToken ct = default)
    {
        _ = await uow.LabAssignments.GetByIdAsync(id)
            ?? throw new NotFoundException($"LabAssignment '{id}' not found.");
        var testCases = await uow.LabTestCases.FindAsync(t => t.LabAssignmentId == id);
        return testCases.OrderBy(t => t.Order).Select(MapTestCase);
    }

    public async Task<IReadOnlyList<LabAssignmentRosterItemDto>> GetRosterAsync(Guid id, CancellationToken ct = default)
    {
        _ = await uow.LabAssignments.GetByIdAsync(id)
            ?? throw new NotFoundException($"LabAssignment '{id}' not found.");

        var submissions = (await uow.LabSubmissions.FindAsync(s => s.LabAssignmentId == id))
            .OrderBy(s => s.StudentCode)
            .ToList();
        if (submissions.Count == 0) return [];

        var approvedTestCases = await uow.LabTestCases.FindAsync(t =>
            t.LabAssignmentId == id && t.Status == LabTestCaseStatus.Approved);
        var maxScore = approvedTestCases.Sum(t => t.Score);

        var submissionIds = submissions.Select(s => s.Id).ToHashSet();
        var jobs = await uow.LabGradingJobs.FindAsync(j => submissionIds.Contains(j.LabSubmissionId));

        var latestJobBySubmission = jobs
            .GroupBy(j => j.LabSubmissionId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(j => j.CreatedAt).ThenByDescending(j => j.Id).First());

        var latestJobIds = latestJobBySubmission.Values.Select(j => j.Id).ToHashSet();
        var latestResults = latestJobIds.Count == 0
            ? []
            : await uow.LabTestCaseResults.FindAsync(r => latestJobIds.Contains(r.LabGradingJobId));
        var totalScoreByJobId = latestResults
            .GroupBy(r => r.LabGradingJobId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.ManualOverrideScore ?? r.AwardedScore));

        return submissions.Select(s =>
        {
            latestJobBySubmission.TryGetValue(s.Id, out var latestJob);
            decimal? totalScore = null;
            if (latestJob is not null &&
                (latestJob.Status == LabGradingJobStatus.Done || latestJob.Status == LabGradingJobStatus.Failed) &&
                totalScoreByJobId.TryGetValue(latestJob.Id, out var score))
            {
                totalScore = score;
            }

            return new LabAssignmentRosterItemDto
            {
                SubmissionId = s.Id,
                StudentCode = s.StudentCode,
                OriginalFileName = s.OriginalFileName,
                SubmissionStatus = s.Status.ToString(),
                LatestJobId = latestJob?.Id,
                JobStatus = latestJob?.Status.ToString(),
                TotalScore = totalScore,
                MaxScore = maxScore,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt
            };
        }).ToList();
    }

    public async Task<LabGradingProgressDto> GetGradingProgressAsync(Guid id, CancellationToken ct = default)
    {
        var assignment = await uow.LabAssignments.GetByIdAsync(id)
            ?? throw new NotFoundException($"LabAssignment '{id}' not found.");

        var submissions = (await uow.LabSubmissions.FindAsync(s => s.LabAssignmentId == id))
            .OrderBy(s => s.StudentCode)
            .ThenBy(s => s.CreatedAt)
            .ToList();
        var totalTestCaseCount = (await uow.LabTestCases.FindAsync(t =>
            t.LabAssignmentId == id && t.Status == LabTestCaseStatus.Approved)).Count();

        if (submissions.Count == 0)
        {
            return new LabGradingProgressDto
            {
                AssignmentId = assignment.Id,
                AssignmentStatus = assignment.Status.ToString(),
                TotalTestCaseCount = totalTestCaseCount
            };
        }

        var submissionIds = submissions.Select(s => s.Id).ToHashSet();
        var jobs = await uow.LabGradingJobs.FindAsync(j => submissionIds.Contains(j.LabSubmissionId));

        var latestJobBySubmission = jobs
            .GroupBy(j => j.LabSubmissionId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(j => j.CreatedAt).ThenByDescending(j => j.Id).First());

        LabGradingJob? runningJob = jobs
            .Where(j => j.Status == LabGradingJobStatus.Running)
            .OrderBy(j => j.StartedAt ?? j.CreatedAt)
            .ThenBy(j => j.CreatedAt)
            .FirstOrDefault();

        if (runningJob is null)
        {
            runningJob = jobs
                .Where(j => j.Status == LabGradingJobStatus.Pending)
                .OrderBy(j => j.CreatedAt)
                .FirstOrDefault();
        }

        var completedSubmissionCount = latestJobBySubmission.Values.Count(j =>
            j.Status == LabGradingJobStatus.Done || j.Status == LabGradingJobStatus.Failed);
        var pendingSubmissionCount = jobs.Count(j => j.Status == LabGradingJobStatus.Pending);
        var queuedSubmissionCount = pendingSubmissionCount -
            (runningJob is not null && runningJob.Status == LabGradingJobStatus.Pending ? 1 : 0);

        var runningSubmission = runningJob is null
            ? null
            : submissions.FirstOrDefault(s => s.Id == runningJob.LabSubmissionId);

        var executedTestCaseCount = 0;
        if (runningJob is not null)
        {
            executedTestCaseCount = (await uow.LabTestCaseResults
                .FindAsync(r => r.LabGradingJobId == runningJob.Id))
                .Count();
        }

        var runningPercent = 0;
        if (runningJob is not null && totalTestCaseCount > 0)
        {
            runningPercent = (int)Math.Round(executedTestCaseCount * 100d / totalTestCaseCount);
            runningPercent = Math.Clamp(runningPercent, 0, 100);
        }

        return new LabGradingProgressDto
        {
            AssignmentId = assignment.Id,
            AssignmentStatus = assignment.Status.ToString(),
            RunningSubmissionId = runningSubmission?.Id,
            RunningStudentCode = runningSubmission?.StudentCode,
            RunningJobId = runningJob?.Id,
            RunningJobStatus = runningJob?.Status.ToString(),
            RunningPercent = runningPercent,
            ExecutedTestCaseCount = executedTestCaseCount,
            TotalTestCaseCount = totalTestCaseCount,
            QueuedSubmissionCount = Math.Max(0, queuedSubmissionCount),
            CompletedSubmissionCount = completedSubmissionCount,
            IsGradingActive = runningJob is not null
        };
    }

    public async Task<int> TriggerGradingAsync(Guid id, CancellationToken ct = default)
    {
        var assignment = await uow.LabAssignments.GetByIdAsync(id)
            ?? throw new NotFoundException($"LabAssignment '{id}' not found.");
        var submissions = (await uow.LabSubmissions.FindAsync(s => s.LabAssignmentId == id))
            .OrderBy(s => s.StudentCode)
            .ThenBy(s => s.CreatedAt)
            .ToList();
        if (submissions.Count == 0) return 0;

        var submissionIds = submissions.Select(s => s.Id).ToHashSet();
        var activeJobs = (await uow.LabGradingJobs.FindAsync(j =>
            submissionIds.Contains(j.LabSubmissionId) &&
            (j.Status == LabGradingJobStatus.Pending || j.Status == LabGradingJobStatus.Running)))
            .Select(j => j.LabSubmissionId).ToHashSet();

        int created = 0;
        foreach (var submission in submissions)
        {
            if (activeJobs.Contains(submission.Id)) continue;
            await uow.LabGradingJobs.AddAsync(new LabGradingJob { LabSubmissionId = submission.Id });
            submission.Status = LabSubmissionStatus.Pending;
            submission.UpdatedAt = DateTime.UtcNow;
            uow.LabSubmissions.Update(submission);
            created++;
        }
        if (created > 0 || activeJobs.Count > 0)
        {
            assignment.Status = LabAssignmentStatus.Grading;
            assignment.UpdatedAt = DateTime.UtcNow;
            uow.LabAssignments.Update(assignment);
            await uow.SaveChangesAsync(ct);
        }
        return created;
    }

    private static LabAssignmentDto Map(LabAssignment a, int tcCount, int subCount, Domain.Entities.Semester? sem) => new()
    {
        Id = a.Id,
        SemesterId = a.SemesterId,
        SemesterName = sem?.Name,
        Title = a.Title,
        Description = a.Description,
        PdfPath = a.PdfPath,
        Status = a.Status.ToString(),
        TestCaseCount = tcCount,
        SubmissionCount = subCount,
        CreatedAt = a.CreatedAt,
        UpdatedAt = a.UpdatedAt
    };

    private static LabTestCaseDto MapTestCase(LabTestCase t) => new()
    {
        Id = t.Id,
        LabAssignmentId = t.LabAssignmentId,
        HttpMethod = t.HttpMethod,
        UrlTemplate = t.UrlTemplate,
        Description = t.Description,
        SaveTokenFrom = t.SaveTokenFrom,
        Headers = t.HeadersJson,
        InputJson = t.InputJson,
        ExpectJson = t.ExpectJson,
        ExpectedStatusCode = t.ExpectedStatusCode,
        MatchMode = t.MatchMode.ToString(),
        Score = t.Score,
        Status = t.Status.ToString(),
        AiGenerated = t.AiGenerated,
        Order = t.Order,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };
}
