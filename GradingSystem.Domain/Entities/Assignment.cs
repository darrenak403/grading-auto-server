namespace GradingSystem.Domain.Entities;

public class Assignment : BaseEntity
{
    /// <summary>Human-readable unique code, e.g. "101", "MA01". Used instead of Guid in external APIs.</summary>
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    // File paths stored under /storage/assignments/{Id}/
    public string? DatabaseSqlPath { get; set; }   // Q1: database.sql
    public string? GivenZipPath { get; set; }      // Q2: given API source zip

    // Shared URL used for all Q2 submissions in this assignment.
    public string? GivenApiBaseUrl { get; set; }

    // Optional grouping under an ExamSession (mã đề belongs to a session)
    public Guid? ExamSessionId { get; set; }
    public ExamSession? ExamSession { get; set; }

    public ICollection<Question> Questions { get; set; } = [];
    public ICollection<Submission> Submissions { get; set; } = [];
    public ICollection<Participant> Participants { get; set; } = [];
}
