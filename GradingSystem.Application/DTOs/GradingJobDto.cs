using System.Text.Json.Serialization;
using GradingSystem.Domain.Entities;

namespace GradingSystem.Application.DTOs;

public class GradingJobDto
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public JobStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}
