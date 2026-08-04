using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;
using GradingSystem.Domain.Entities;

namespace GradingSystem.Application.DTOs;

public class SubmissionDto
{
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string ArtifactZipPath { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SubmissionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal? TotalScore { get; set; }
    public int? MaxScore { get; set; }
    public string GradingRound { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public JobStatus? LatestJobStatus { get; set; }
}

public class ImportCustomResultRequest
{
    [Required]
    public Guid TemplateSubmissionId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Score { get; set; }

    [Required]
    [MinLength(1)]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? AdjustedBy { get; set; }
}
