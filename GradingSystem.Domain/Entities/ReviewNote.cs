namespace GradingSystem.Domain.Entities;

public class ReviewNote : BaseEntity
{
    public Guid SubmissionId { get; set; }
    public Submission Submission { get; set; } = null!;

    public string Content { get; set; } = string.Empty;
    public string? ReviewedBy { get; set; }
}
