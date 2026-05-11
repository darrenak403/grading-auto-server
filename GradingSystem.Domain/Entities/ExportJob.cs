namespace GradingSystem.Domain.Entities;

public enum ExportStatus { Pending, Done, Failed }

public class ExportJob : BaseEntity
{
    /// <summary>Set for per-assignment exports; null for session exports.</summary>
    public Guid? AssignmentId { get; set; }
    public Assignment? Assignment { get; set; }

    /// <summary>Set for full-session exports; null for assignment exports.</summary>
    public Guid? ExamSessionId { get; set; }
    public ExamSession? ExamSession { get; set; }

    /// <summary>Optional round filter; null = latest round per submission.</summary>
    public string? GradingRound { get; set; }
    public ExportStatus Status { get; set; } = ExportStatus.Pending;
    public string? FilePath { get; set; }
    public string? ErrorMessage { get; set; }
}
