namespace GradingSystem.Domain.Entities;

public enum LabGradingJobStatus { Pending, Running, Done, Failed }

public class LabGradingJob : BaseEntity
{
    public Guid LabSubmissionId { get; set; }
    public LabSubmission LabSubmission { get; set; } = null!;
    public LabGradingJobStatus Status { get; set; } = LabGradingJobStatus.Pending;
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public ICollection<LabTestCaseResult> Results { get; set; } = [];
}
