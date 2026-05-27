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

    public async Task<int> TriggerGradingAsync(Guid id, CancellationToken ct = default)
    {
        var assignment = await uow.LabAssignments.GetByIdAsync(id)
            ?? throw new NotFoundException($"LabAssignment '{id}' not found.");
        var submissions = (await uow.LabSubmissions.FindAsync(s => s.LabAssignmentId == id)).ToList();
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
            created++;
        }
        if (created > 0)
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
