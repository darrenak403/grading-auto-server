using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace GradingSystem.Application.DTOs;

public class CreateTestCaseRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }
    [Required]
    [MaxLength(10)]
    public string HttpMethod { get; set; } = string.Empty;
    [Required]
    [MaxLength(500)]
    public string UrlTemplate { get; set; } = string.Empty;
    public JsonElement? Input { get; set; }
    [Range(100, 599)]
    public int? ExpectedStatus { get; set; }
    public bool? IsArray { get; set; }
    public List<string>? Fields { get; set; }
    public string? Value { get; set; }
    public string? Selector { get; set; }
    public string? SelectorText { get; set; }
    [Range(1, 1000)]
    public int? SelectorMinCount { get; set; }
    [Range(0.01, double.MaxValue)]
    public decimal Score { get; set; }
    // Q1 newman: expected response body JSON
    public JsonElement? ExpectedBody { get; set; }
    // Q2 id-based: HTML element id to check
    public string? ElementId { get; set; }
    public string? ElementText { get; set; }
    // Q2 sequential flows: execution order and variable extraction
    public int Order { get; set; }
    public Dictionary<string, string>? Extract { get; set; }
}
