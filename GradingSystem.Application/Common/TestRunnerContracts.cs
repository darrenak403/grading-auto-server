namespace GradingSystem.Application.Common;

public class ExpectJson
{
    public int? Status { get; set; }
    public bool? IsArray { get; set; }
    public List<string>? Fields { get; set; }
    public string? Value { get; set; }
    public string? Selector { get; set; }
    public string? SelectorText { get; set; }
    public int? SelectorMinCount { get; set; }
    // Q1: exact JSON body comparison via newman
    public System.Text.Json.JsonElement? Body { get; set; }
    // Q2: check element existence/text by HTML id attribute
    public string? ElementId { get; set; }
    public string? ElementText { get; set; }
    // Q2 sequential flows: extract values from JSON response into variable context
    public Dictionary<string, string>? Extract { get; set; }
}

public class TestCaseResult
{
    public Guid TestCaseId { get; set; }
    public bool Pass { get; set; }
    public decimal AwardedScore { get; set; }
    public string HttpMethod { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int ActualStatus { get; set; }
    public string? ActualBody { get; set; }
    public string? FailReason { get; set; }
    // Q2: base64-encoded PNG screenshot captured after the test case ran
    public string? ScreenshotBase64 { get; set; }
}
