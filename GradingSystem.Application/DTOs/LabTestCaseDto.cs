namespace GradingSystem.Application.DTOs;

public record CreateLabTestCaseRequest(
    string HttpMethod,
    string UrlTemplate,
    string? Description,
    string? InputJson,
    string? ExpectJson,
    int ExpectedStatusCode = 200,
    string MatchMode = "Subset",
    decimal Score = 1.0m,
    int Order = 0
);

public record UpdateLabTestCaseRequest(
    string HttpMethod,
    string UrlTemplate,
    string? Description,
    string? InputJson,
    string? ExpectJson,
    int ExpectedStatusCode,
    string MatchMode,
    decimal Score,
    int Order
);

public class LabTestCaseDto
{
    public Guid Id { get; init; }
    public Guid LabAssignmentId { get; init; }
    public string HttpMethod { get; init; } = null!;
    public string UrlTemplate { get; init; } = null!;
    public string? Description { get; init; }
    public string? InputJson { get; init; }
    public string? ExpectJson { get; init; }
    public int ExpectedStatusCode { get; init; }
    public string MatchMode { get; init; } = null!;
    public decimal Score { get; init; }
    public string Status { get; init; } = null!;
    public bool AiGenerated { get; init; }
    public int Order { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
