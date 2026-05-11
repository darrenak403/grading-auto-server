using System.Text.Json.Serialization;
using GradingSystem.Application.Common;
using GradingSystem.Domain.Entities;

namespace GradingSystem.Application.DTOs;

public class ExamSessionDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<AssignmentSummaryDto> Assignments { get; set; } = [];
}

public class ExamSessionSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateExamSessionRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class ParticipantDto
{
    public Guid Id { get; set; }
    public Guid ExamSessionId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string StudentCode { get; set; } = string.Empty;
    public Guid AssignmentId { get; set; }
    public string AssignmentCode { get; set; } = string.Empty;
    public string AssignmentTitle { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class SessionSubmissionResultDto
{
    public Guid SubmissionId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string StudentCode { get; set; } = string.Empty;
    public string AssignmentCode { get; set; } = string.Empty;
    public string GradingRound { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SubmissionStatus Status { get; set; }
    public bool HasArtifact { get; set; }
    public decimal TotalScore { get; set; }
    public int MaxScore { get; set; }
    public List<QuestionSummaryResult> Questions { get; set; } = [];
    public string? Notes { get; set; }
}

public class QuestionSummaryResult
{
    public Guid QuestionId { get; set; }
    public string QuestionTitle { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public decimal FinalScore { get; set; }
    public int MaxScore { get; set; }
    public decimal? AdjustedScore { get; set; }
    public string? AdjustReason { get; set; }
    public List<TestCaseResult>? TestCaseResults { get; set; }
}

public class ImportParticipantsResultDto
{
    public int Created { get; set; }
    public int Skipped { get; set; }
    public List<string> Errors { get; set; } = [];
}
