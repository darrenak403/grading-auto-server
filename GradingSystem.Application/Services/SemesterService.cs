using GradingSystem.Application.DTOs;
using GradingSystem.Application.Exceptions;
using GradingSystem.Application.Interfaces;
using GradingSystem.Domain.Entities;
namespace GradingSystem.Application.Services;

public class SemesterService(IUnitOfWork uow) : ISemesterService
{
    public async Task<IEnumerable<SemesterDto>> ListAsync(CancellationToken ct = default)
    {
        var all = await uow.Semesters.GetAllAsync();
        var labs = await uow.LabAssignments.FindAsync(l => l.SemesterId.HasValue);
        var counts = labs.GroupBy(l => l.SemesterId!.Value).ToDictionary(g => g.Key, g => g.Count());
        return all.OrderByDescending(s => s.CreatedAt).Select(s => MapDto(s, counts.GetValueOrDefault(s.Id)));
    }

    public async Task<SemesterDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var semester = await uow.Semesters.GetByIdAsync(id);
        if (semester is null) return null;

        var labList = (await uow.LabAssignments.FindAsync(l => l.SemesterId == id))
            .OrderBy(l => l.CreatedAt).ToList();

        var labIds = labList.Select(l => l.Id).ToHashSet();
        var testCases = labIds.Any()
            ? await uow.LabTestCases.FindAsync(t => labIds.Contains(t.LabAssignmentId))
            : [];
        var submissions = labIds.Any()
            ? await uow.LabSubmissions.FindAsync(s => labIds.Contains(s.LabAssignmentId))
            : [];

        var tcCounts = testCases.GroupBy(t => t.LabAssignmentId).ToDictionary(g => g.Key, g => g.Count());
        var subCounts = submissions.GroupBy(s => s.LabAssignmentId).ToDictionary(g => g.Key, g => g.Count());

        return new SemesterDetailDto
        {
            Id = semester.Id,
            Name = semester.Name,
            Code = semester.Code,
            StartDate = semester.StartDate,
            EndDate = semester.EndDate,
            LabAssignmentCount = labList.Count,
            CreatedAt = semester.CreatedAt,
            UpdatedAt = semester.UpdatedAt,
            LabAssignments = labList.Select(l => MapLabDto(l, semester, tcCounts.GetValueOrDefault(l.Id), subCounts.GetValueOrDefault(l.Id)))
        };
    }

    public async Task<SemesterDto> CreateAsync(CreateSemesterRequest req, CancellationToken ct = default)
    {
        var existing = await uow.Semesters.FindAsync(s => s.Code == req.Code);
        if (existing.Any())
            throw new ConflictException($"Semester with code '{req.Code}' already exists.");

        var semester = new Semester
        {
            Name = req.Name,
            Code = req.Code,
            StartDate = req.StartDate,
            EndDate = req.EndDate
        };
        await uow.Semesters.AddAsync(semester);
        await uow.SaveChangesAsync(ct);
        return MapDto(semester, 0);
    }

    public async Task<SemesterDto> UpdateAsync(Guid id, UpdateSemesterRequest req, CancellationToken ct = default)
    {
        var semester = await uow.Semesters.GetByIdAsync(id)
            ?? throw new NotFoundException($"Semester '{id}' not found.");

        if (semester.Code != req.Code)
        {
            var existing = await uow.Semesters.FindAsync(s => s.Code == req.Code);
            if (existing.Any())
                throw new ConflictException($"Semester with code '{req.Code}' already exists.");
        }

        semester.Name = req.Name;
        semester.Code = req.Code;
        semester.StartDate = req.StartDate;
        semester.EndDate = req.EndDate;
        semester.UpdatedAt = DateTime.UtcNow;
        uow.Semesters.Update(semester);
        await uow.SaveChangesAsync(ct);

        var count = (await uow.LabAssignments.FindAsync(l => l.SemesterId == id)).ToList().Count;
        return MapDto(semester, count);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var semester = await uow.Semesters.GetByIdAsync(id)
            ?? throw new NotFoundException($"Semester '{id}' not found.");

        var labList = (await uow.LabAssignments.FindAsync(l => l.SemesterId == id)).ToList();
        if (labList.Count > 0)
            throw new BadRequestException($"Cannot delete semester '{semester.Name}' — it has {labList.Count} lab assignment(s). Reassign or delete them first.");

        uow.Semesters.Remove(semester);
        await uow.SaveChangesAsync(ct);
    }

    private static SemesterDto MapDto(Semester s, int labCount) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Code = s.Code,
        StartDate = s.StartDate,
        EndDate = s.EndDate,
        LabAssignmentCount = labCount,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt
    };

    private static LabAssignmentDto MapLabDto(LabAssignment l, Semester s, int tcCount, int subCount) => new()
    {
        Id = l.Id,
        SemesterId = l.SemesterId,
        SemesterName = s.Name,
        Title = l.Title,
        Description = l.Description,
        PdfPath = l.PdfPath,
        Status = l.Status.ToString(),
        TestCaseCount = tcCount,
        SubmissionCount = subCount,
        CreatedAt = l.CreatedAt,
        UpdatedAt = l.UpdatedAt
    };
}
