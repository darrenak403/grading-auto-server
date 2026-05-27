namespace GradingSystem.Application.DTOs;

public record CreateLabAssignmentRequest(string Title, string? Description, Guid? SemesterId = null);
public record UpdateLabAssignmentRequest(string Title, string? Description, Guid? SemesterId = null);

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
