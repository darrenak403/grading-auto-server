namespace GradingSystem.Domain.Entities;

public class ExamSession : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<Assignment> Assignments { get; set; } = [];
    public ICollection<Participant> Participants { get; set; } = [];
}
