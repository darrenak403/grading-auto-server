using System.ComponentModel.DataAnnotations;

namespace GradingSystem.Application.DTOs;

public class ReviewNoteDto
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ReviewedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdateReviewNoteRequest
{
    [Required]
    [MaxLength(5000)]
    public string Content { get; set; } = string.Empty;
    [MaxLength(200)]
    public string? ReviewedBy { get; set; }
}
