namespace GradingSystem.Domain.Entities;

public enum JobStatus { Pending, Running, Done, Failed }

public class GradingJob : BaseEntity
{
    public Guid SubmissionId { get; set; }
    public Submission Submission { get; set; } = null!;

    public string GradingRound { get; set; } = "Lần 1";
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public string? ErrorMessage { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}
