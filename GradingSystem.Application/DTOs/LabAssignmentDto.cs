using System.Text.Json;

namespace GradingSystem.Application.DTOs;

public record CreateLabAssignmentRequest(string Title, string? Description, Guid? SemesterId = null);
public record UpdateLabAssignmentRequest(string Title, string? Description, Guid? SemesterId = null);
public record SyncSupabaseRequest(string? LabId, string? ClassName, string? TermId = null);
public record SyncSupabaseGradeRequest(
    string StudentCode,
    string ClassName,
    string LabCode,
    decimal Score,
    JsonElement Details,
    string? SourceUrl = null,
    string? TermId = null);
public record SyncSupabaseGradesRequest(
    string ClassName,
    string LabCode,
    IReadOnlyList<SyncSupabaseGradeItemRequest> Submissions,
    string? TermId = null);
public record SyncSupabaseGradeItemRequest(
    string StudentCode,
    decimal Score,
    JsonElement Details,
    string? SourceUrl = null);
public record SyncSupabaseGradeResponse(
    string ClassStudentId,
    string ClassLabId,
    string ItemType,
    string? FulfillsRequestId);
public record SyncSupabaseGradesResponse(
    int Total,
    int SyncedCount,
    int FailedCount,
    IReadOnlyList<SyncSupabaseGradeItemResult> Synced,
    IReadOnlyList<SyncSupabaseGradeItemFailure> Failed);
public record SyncSupabaseGradeItemResult(
    string StudentCode,
    string ClassStudentId,
    string ClassLabId,
    string ItemType,
    string? FulfillsRequestId);
public record SyncSupabaseGradeItemFailure(string StudentCode, string Error);
public record SupabaseDropdownOptionsDto(
    IReadOnlyList<SupabaseTermOptionDto> Terms,
    IReadOnlyList<SupabaseClassOptionDto> Classes,
    IReadOnlyList<SupabaseLabOptionDto> Labs);
public record SupabaseTermOptionDto(string Id, string? Code, string? Name);
public record SupabaseClassOptionDto(string Name, string? TermId, string? TermCode, string? TermName);
public record SupabaseLabOptionDto(string Code, string? Title, string? ClassName, string? TermId, string? TermCode, string? TermName, DateTimeOffset? Deadline);

public class LabAssignmentDto
{
    public Guid Id { get; init; }
    public Guid? SemesterId { get; init; }
    public string? SemesterName { get; init; }
    public string Title { get; init; } = null!;
    public string? Description { get; init; }
    public string? PdfPath { get; init; }
    public string Status { get; init; } = null!;
    public int TestCaseCount { get; init; }
    public int SubmissionCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
