namespace GradingSystem.Application.Models;

public class LabTestCaseDraft
{
    public string HttpMethod { get; set; } = null!;
    public string UrlTemplate { get; set; } = null!;
    public string? Description { get; set; }
    public string? InputJson { get; set; }
    public string? ExpectedJson { get; set; }
    public decimal SuggestedScore { get; set; }
    public int ExpectedStatusCode { get; set; } = 200;
    /// <summary>Subset | Exact | StatusOnly</summary>
    public string MatchMode { get; set; } = "Subset";
}
