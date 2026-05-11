namespace GradingSystem.Domain.Entities;

public class QuestionResult : BaseEntity
{
    public Guid SubmissionId { get; set; }
    public Submission Submission { get; set; } = null!;

    public Guid? GradingJobId { get; set; }
    public GradingJob? GradingJob { get; set; }

    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;

    public decimal Score { get; set; }
    public int MaxScore { get; set; }
    public string? Detail { get; set; }        // JSON array of per-test-case results

    // Lecturer override
    public decimal? AdjustedScore { get; set; }    // null = dùng Score tự động
    public string? AdjustReason { get; set; }
    public string? AdjustedBy { get; set; }
    public DateTime? AdjustedAt { get; set; }

    public decimal FinalScore => AdjustedScore ?? Score;
}
