namespace GradingSystem.Application.DTOs;

public class LabAssignmentRosterItemDto
{
    public Guid SubmissionId { get; init; }
    public string StudentCode { get; init; } = null!;
    public string OriginalFileName { get; init; } = null!;
    public string SubmissionStatus { get; init; } = null!;
    public Guid? LatestJobId { get; init; }
    public string? JobStatus { get; init; }
    public decimal? TotalScore { get; init; }
    public decimal MaxScore { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
