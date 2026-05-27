using System.ComponentModel.DataAnnotations;

namespace PRN232.LMS.API.Models.Requests;

public class StudentRequest
{
    [Required(ErrorMessage = "FullName is required")]
    [MaxLength(100)]
    public string FullName { get; set; } = null!;

    [Required(ErrorMessage = "Email is required")]
    [MaxLength(100)]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "DateOfBirth is required")]
    public DateTime DateOfBirth { get; set; }
}
