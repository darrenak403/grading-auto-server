namespace GradingSystem.Application.DTOs;

public class LabTestCaseResultDto
{
    public Guid Id { get; init; }
    public Guid LabTestCaseId { get; init; }
    public string HttpMethod { get; init; } = null!;
    public string UrlTemplate { get; init; } = null!;
    public bool Passed { get; init; }
    public decimal AwardedScore { get; init; }
    public int? ActualStatusCode { get; init; }
    public string? ActualResponse { get; init; }
    public string? ErrorMessage { get; init; }
    public decimal? ManualOverrideScore { get; init; }
    public string? OverrideReason { get; init; }
}

public class LabGradingResultDto
{
    public Guid SubmissionId { get; init; }
    public string StudentCode { get; init; } = null!;
    public string SubmissionStatus { get; init; } = null!;
    public Guid? LatestJobId { get; init; }
    public string? JobStatus { get; init; }
    public decimal TotalScore { get; init; }
    public List<LabTestCaseResultDto> Results { get; init; } = [];
}
