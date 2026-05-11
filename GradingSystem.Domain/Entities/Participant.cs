namespace GradingSystem.Domain.Entities;

public class Participant : BaseEntity
{
    public Guid ExamSessionId { get; set; }
    public ExamSession ExamSession { get; set; } = null!;

    /// <summary>Full folder name from the bulk-upload zip, e.g. "hoalvpse181951"</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Parsed student code suffix, e.g. "pse181951"</summary>
    public string StudentCode { get; set; } = string.Empty;

    public Guid AssignmentId { get; set; }
    public Assignment Assignment { get; set; } = null!;

    public ICollection<Submission> Submissions { get; set; } = [];
}
