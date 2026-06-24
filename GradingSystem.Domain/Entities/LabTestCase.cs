namespace GradingSystem.Domain.Entities;

public enum LabTestCaseStatus { Draft, Approved, Rejected }

// Subset: all keys in ExpectJson must exist in actual (ignore extra keys, recursive)
// Exact: full JSON equality
// StatusOnly: only HTTP status code checked, body ignored (use for 404/400 tests)
public enum LabTestCaseMatchMode { Subset, Exact, StatusOnly }

public class LabTestCase : BaseEntity
{
    public Guid LabAssignmentId { get; set; }
    public LabAssignment LabAssignment { get; set; } = null!;
    public string HttpMethod { get; set; } = null!;
    public string UrlTemplate { get; set; } = null!;
    public string? Description { get; set; }
    public string? SaveTokenFrom { get; set; }
    public string? HeadersJson { get; set; }
    public string? InputJson { get; set; }
    public string? ExpectJson { get; set; }
    public int ExpectedStatusCode { get; set; } = 200;
    public LabTestCaseMatchMode MatchMode { get; set; } = LabTestCaseMatchMode.Subset;
    public decimal Score { get; set; }
    public LabTestCaseStatus Status { get; set; } = LabTestCaseStatus.Draft;
    public bool AiGenerated { get; set; }
    public int Order { get; set; }
}
