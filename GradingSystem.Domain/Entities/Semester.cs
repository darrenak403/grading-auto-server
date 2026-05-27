namespace GradingSystem.Domain.Entities;

public class Semester : BaseEntity
{
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public ICollection<LabAssignment> LabAssignments { get; set; } = [];
}
