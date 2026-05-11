using System.Text.Json.Serialization;
using GradingSystem.Domain.Entities;

namespace GradingSystem.Application.DTOs;

public class ExportJobDto
{
    public Guid Id { get; set; }
    public Guid? AssignmentId { get; set; }
    public string? AssignmentCode { get; set; }
    public Guid? ExamSessionId { get; set; }
    public string? ExamSessionTitle { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ExportStatus Status { get; set; }
    public string? GradingRound { get; set; }
    public string? FilePath { get; set; }
    public string? ErrorMessage { get; set; }
}

public class CreateExportRequest
{
    public Guid AssignmentId { get; set; }
    public string? GradingRound { get; set; }
}

public class CreateSessionExportRequest
{
    public string? GradingRound { get; set; }
}
