using System.Text.Json;

namespace GradingSystem.Application.DTOs;

public class TestCaseDto
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string UrlTemplate { get; set; } = string.Empty;
    public JsonElement? Input { get; set; }
    public int? ExpectedStatus { get; set; }
    public bool? IsArray { get; set; }
    public List<string>? Fields { get; set; }
    public string? Value { get; set; }
    public string? Selector { get; set; }
    public string? SelectorText { get; set; }
    public int? SelectorMinCount { get; set; }
    public decimal Score { get; set; }
    public JsonElement? ExpectedBody { get; set; }
    public string? ElementId { get; set; }
    public string? ElementText { get; set; }
    public int Order { get; set; }
    public Dictionary<string, string>? Extract { get; set; }
}
