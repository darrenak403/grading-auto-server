using System.ComponentModel.DataAnnotations;

namespace PRN232.LMS.API.Models.Requests;

public class SubjectRequest
{
    [Required(ErrorMessage = "SubjectCode is required")]
    [MaxLength(20)]
    public string SubjectCode { get; set; } = null!;

    [Required(ErrorMessage = "SubjectName is required")]
    [MaxLength(100)]
    public string SubjectName { get; set; } = null!;

    [Required]
    [Range(1, 10, ErrorMessage = "Credit must be between 1 and 10")]
    public int Credit { get; set; }
}
