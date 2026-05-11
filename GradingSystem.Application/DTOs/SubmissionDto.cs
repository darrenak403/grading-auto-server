using System.Text.Json.Serialization;
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
}
