namespace GradingSystem.Domain.Entities;

public class LabTestCaseResult : BaseEntity
{
    public Guid LabGradingJobId { get; set; }
    public LabGradingJob LabGradingJob { get; set; } = null!;
    public Guid LabTestCaseId { get; set; }
    public LabTestCase LabTestCase { get; set; } = null!;
    public bool Passed { get; set; }
    public decimal AwardedScore { get; set; }
    public int? ActualStatusCode { get; set; }
    public string? ActualResponse { get; set; }
    public string? ErrorMessage { get; set; }
    public decimal? ManualOverrideScore { get; set; }
    public string? OverrideReason { get; set; }
}
