using GradingSystem.Domain.Entities;

namespace GradingSystem.Application.DTOs;

public class QuestionDto
{
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public QuestionType Type { get; set; }
    public int MaxScore { get; set; }
    public string ArtifactFolderName { get; set; } = string.Empty;
}
