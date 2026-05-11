namespace GradingSystem.Domain.Entities;

public enum SubmissionStatus { Pending, Grading, Done, Error }

public class Submission : BaseEntity
{
    public Guid AssignmentId { get; set; }
    public Assignment Assignment { get; set; } = null!;

    public Guid? ParticipantId { get; set; }
    public Participant? Participant { get; set; }

    public string StudentCode { get; set; } = string.Empty;
    public string ArtifactZipPath { get; set; } = string.Empty;  // /storage/submissions/{Id}/artifact.zip
    public bool HasArtifact { get; set; } = true;
    public string GradingRound { get; set; } = "Lần 1";
    public SubmissionStatus Status { get; set; } = SubmissionStatus.Pending;

    public ICollection<GradingJob> GradingJobs { get; set; } = [];
    public ICollection<QuestionResult> QuestionResults { get; set; } = [];
    public ReviewNote? ReviewNote { get; set; }
}
