namespace GradingSystem.Application.DTOs;

public class AssignmentDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ExamSessionId { get; set; }
    public string? DatabaseSqlPath { get; set; }
    public string? GivenApiBaseUrl { get; set; }
    public bool HasGivenZip { get; set; }
    public DateTime CreatedAt { get; set; }
}
