using System.ComponentModel.DataAnnotations;

namespace PRN232.LMS.API.Models.Requests;

public class SemesterRequest
{
    [Required(ErrorMessage = "SemesterName is required")]
    [MaxLength(100)]
    public string SemesterName { get; set; } = null!;

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }
}
