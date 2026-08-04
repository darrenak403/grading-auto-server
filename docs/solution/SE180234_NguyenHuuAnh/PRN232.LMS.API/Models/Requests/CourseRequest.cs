using System.ComponentModel.DataAnnotations;

namespace PRN232.LMS.API.Models.Requests;

public class CourseRequest
{
    [Required(ErrorMessage = "CourseName is required")]
    [MaxLength(100)]
    public string CourseName { get; set; } = null!;

    [Required(ErrorMessage = "SemesterId is required")]
    [Range(1, int.MaxValue, ErrorMessage = "SemesterId must be a positive integer")]
    public int SemesterId { get; set; }
}
