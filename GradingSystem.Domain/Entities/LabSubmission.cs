namespace GradingSystem.Domain.Entities;

public enum LabSubmissionStatus { Pending, Grading, Done, BuildFailed, Error }

public class LabSubmission : BaseEntity
{
    public Guid LabAssignmentId { get; set; }
    public LabAssignment LabAssignment { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public string OriginalFileName { get; set; } = null!;
    public string FilePath { get; set; } = null!;
    public LabSubmissionStatus Status { get; set; } = LabSubmissionStatus.Pending;

    public ICollection<LabGradingJob> GradingJobs { get; set; } = [];
}
