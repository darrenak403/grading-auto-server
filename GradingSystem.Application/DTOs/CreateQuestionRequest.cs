using System.ComponentModel.DataAnnotations;
using GradingSystem.Domain.Entities;

namespace GradingSystem.Application.DTOs;

public class CreateQuestionRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    [Required]
    public QuestionType Type { get; set; }
    [Range(1, int.MaxValue)]
    public int MaxScore { get; set; }
    [Required]
    [MaxLength(100)]
    public string ArtifactFolderName { get; set; } = string.Empty;
}
