namespace GradingSystem.Application.DTOs;

public record CreateSemesterRequest(string Name, string Code, DateOnly? StartDate, DateOnly? EndDate);
public record UpdateSemesterRequest(string Name, string Code, DateOnly? StartDate, DateOnly? EndDate);

public class SemesterDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public int LabAssignmentCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public class SemesterDetailDto : SemesterDto
{
    public IEnumerable<LabAssignmentDto> LabAssignments { get; init; } = [];
}
