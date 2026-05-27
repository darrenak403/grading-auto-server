using System.ComponentModel.DataAnnotations;

namespace PRN232.LMS.API.Models.Requests;

public class EnrollmentRequest
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "StudentId must be a positive integer")]
    public int StudentId { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "CourseId must be a positive integer")]
    public int CourseId { get; set; }

    [Required]
    public DateTime EnrollDate { get; set; }

    [Required]
    [RegularExpression("^(Active|Completed|Withdrawn|Pending)$",
        ErrorMessage = "Status must be one of: Active, Completed, Withdrawn, Pending")]
    public string Status { get; set; } = null!;
}
