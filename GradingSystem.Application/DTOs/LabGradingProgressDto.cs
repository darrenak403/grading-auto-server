namespace GradingSystem.Application.DTOs;

public class LabGradingProgressDto
{
    public Guid AssignmentId { get; init; }
    public string AssignmentStatus { get; init; } = null!;
    public Guid? RunningSubmissionId { get; init; }
    public string? RunningStudentCode { get; init; }
    public Guid? RunningJobId { get; init; }
    public string? RunningJobStatus { get; init; }
    public int RunningPercent { get; init; }
    public int ExecutedTestCaseCount { get; init; }
    public int TotalTestCaseCount { get; init; }
    public int QueuedSubmissionCount { get; init; }
    public int CompletedSubmissionCount { get; init; }
    public bool IsGradingActive { get; init; }
}
