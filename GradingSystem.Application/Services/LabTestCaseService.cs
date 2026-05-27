using GradingSystem.Application.DTOs;
using GradingSystem.Application.Exceptions;
using GradingSystem.Application.Interfaces;
using GradingSystem.Domain.Entities;

namespace GradingSystem.Application.Services;

public class LabTestCaseService(IUnitOfWork uow) : ILabTestCaseService
{
    public async Task<LabTestCaseDto> CreateAsync(Guid assignmentId, CreateLabTestCaseRequest req, CancellationToken ct = default)
    {
        _ = await uow.LabAssignments.GetByIdAsync(assignmentId)
            ?? throw new NotFoundException($"LabAssignment '{assignmentId}' not found.");
        if (!Enum.TryParse<LabTestCaseMatchMode>(req.MatchMode, ignoreCase: true, out var mode))
            throw new BadRequestException($"Invalid MatchMode '{req.MatchMode}'. Valid values: Subset, Exact, StatusOnly.");
        var tc = new LabTestCase
        {
            LabAssignmentId = assignmentId,
            HttpMethod = req.HttpMethod,
            UrlTemplate = req.UrlTemplate,
            Description = req.Description,
            InputJson = req.InputJson,
            ExpectJson = req.ExpectJson,
            ExpectedStatusCode = req.ExpectedStatusCode,
            MatchMode = mode,
            Score = req.Score,
            Order = req.Order,
            AiGenerated = false
        };
        await uow.LabTestCases.AddAsync(tc);
        await uow.SaveChangesAsync(ct);
        return Map(tc);
    }

    public async Task<LabTestCaseDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tc = await uow.LabTestCases.GetByIdAsync(id);
        return tc is null ? null : Map(tc);
    }

    public async Task<LabTestCaseDto> UpdateAsync(Guid id, UpdateLabTestCaseRequest req, CancellationToken ct = default)
    {
        var tc = await uow.LabTestCases.GetByIdAsync(id)
            ?? throw new NotFoundException($"LabTestCase '{id}' not found.");
        tc.HttpMethod = req.HttpMethod;
        tc.UrlTemplate = req.UrlTemplate;
        tc.Description = req.Description;
        tc.InputJson = req.InputJson;
        tc.ExpectJson = req.ExpectJson;
        tc.ExpectedStatusCode = req.ExpectedStatusCode;
        if (!Enum.TryParse<LabTestCaseMatchMode>(req.MatchMode, ignoreCase: true, out var mode))
            throw new BadRequestException($"Invalid MatchMode '{req.MatchMode}'. Valid values: Subset, Exact, StatusOnly.");
        tc.MatchMode = mode;
        tc.Score = req.Score;
        tc.Order = req.Order;
        tc.UpdatedAt = DateTime.UtcNow;
        uow.LabTestCases.Update(tc);
        await uow.SaveChangesAsync(ct);
        return Map(tc);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var tc = await uow.LabTestCases.GetByIdAsync(id)
            ?? throw new NotFoundException($"LabTestCase '{id}' not found.");
        uow.LabTestCases.Remove(tc);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<LabTestCaseDto> SetStatusAsync(Guid id, string status, CancellationToken ct = default)
    {
        if (!Enum.TryParse<LabTestCaseStatus>(status, ignoreCase: true, out var newStatus))
            throw new BadRequestException($"Invalid status '{status}'. Valid values: Draft, Approved, Rejected.");

        var tc = await uow.LabTestCases.GetByIdAsync(id)
            ?? throw new NotFoundException($"LabTestCase '{id}' not found.");

        tc.Status = newStatus;
        tc.UpdatedAt = DateTime.UtcNow;
        uow.LabTestCases.Update(tc);

        if (newStatus == LabTestCaseStatus.Approved)
        {
            var assignment = await uow.LabAssignments.GetByIdAsync(tc.LabAssignmentId);
            if (assignment is not null && assignment.Status == LabAssignmentStatus.Draft)
            {
                assignment.Status = LabAssignmentStatus.TestcasesReady;
                assignment.UpdatedAt = DateTime.UtcNow;
                uow.LabAssignments.Update(assignment);
            }
        }

        await uow.SaveChangesAsync(ct);
        return Map(tc);
    }

    public async Task<IEnumerable<LabTestCaseDto>> BulkCreateAsync(
        Guid assignmentId,
        IEnumerable<CreateLabTestCaseRequest> reqs,
        CancellationToken ct = default)
    {
        _ = await uow.LabAssignments.GetByIdAsync(assignmentId)
            ?? throw new NotFoundException($"LabAssignment '{assignmentId}' not found.");

        var existing = await uow.LabTestCases.FindAsync(t => t.LabAssignmentId == assignmentId);
        int nextOrder = existing.Any() ? existing.Max(t => t.Order) + 1 : 1;

        var created = new List<LabTestCase>();
        foreach (var req in reqs)
        {
            var tc = new LabTestCase
            {
                LabAssignmentId = assignmentId,
                HttpMethod = req.HttpMethod.ToUpperInvariant(),
                UrlTemplate = req.UrlTemplate,
                Description = req.Description,
                InputJson = req.InputJson,
                ExpectJson = req.ExpectJson,
                ExpectedStatusCode = req.ExpectedStatusCode,
                MatchMode = Enum.TryParse<LabTestCaseMatchMode>(req.MatchMode, ignoreCase: true, out var m)
                    ? m : LabTestCaseMatchMode.Subset,
                Score = req.Score,
                Status = LabTestCaseStatus.Draft,
                AiGenerated = false,
                Order = nextOrder++,
            };
            await uow.LabTestCases.AddAsync(tc);
            created.Add(tc);
        }

        await uow.SaveChangesAsync(ct);
        return created.Select(Map);
    }

    public async Task<int> ApproveAllAsync(Guid assignmentId, CancellationToken ct = default)
    {
        _ = await uow.LabAssignments.GetByIdAsync(assignmentId)
            ?? throw new NotFoundException($"LabAssignment '{assignmentId}' not found.");

        var drafts = (await uow.LabTestCases.FindAsync(t =>
            t.LabAssignmentId == assignmentId &&
            t.Status == LabTestCaseStatus.Draft))
            .ToList();

        if (drafts.Count == 0) return 0;

        var now = DateTime.UtcNow;
        foreach (var tc in drafts)
        {
            tc.Status = LabTestCaseStatus.Approved;
            tc.UpdatedAt = now;
            uow.LabTestCases.Update(tc);
        }

        var assignment = await uow.LabAssignments.GetByIdAsync(assignmentId);
        if (assignment is not null && assignment.Status == LabAssignmentStatus.Draft)
        {
            assignment.Status = LabAssignmentStatus.TestcasesReady;
            assignment.UpdatedAt = now;
            uow.LabAssignments.Update(assignment);
        }

        await uow.SaveChangesAsync(ct);
        return drafts.Count;
    }

    public async Task<int> DeleteAllByAssignmentAsync(Guid assignmentId, CancellationToken ct = default)
    {
        _ = await uow.LabAssignments.GetByIdAsync(assignmentId)
            ?? throw new NotFoundException($"LabAssignment '{assignmentId}' not found.");
        var testCases = (await uow.LabTestCases.FindAsync(t => t.LabAssignmentId == assignmentId)).ToList();
        if (testCases.Count == 0) return 0;

        var tcIds = testCases.Select(t => t.Id).ToHashSet();
        var results = (await uow.LabTestCaseResults.FindAsync(r => tcIds.Contains(r.LabTestCaseId))).ToList();
        foreach (var r in results)
            uow.LabTestCaseResults.Remove(r);
        foreach (var tc in testCases)
            uow.LabTestCases.Remove(tc);

        await uow.SaveChangesAsync(ct);
        return testCases.Count;
    }

    private static LabTestCaseDto Map(LabTestCase t) => new()
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
