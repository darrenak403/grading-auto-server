namespace PRN232.LMS.Services.Models;

/// <summary>Business model for Student - used in Service layer processing</summary>
public class StudentBusinessModel
{
    public int StudentId { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public DateTime DateOfBirth { get; set; }
    public int EnrollmentCount { get; set; }
    public List<EnrollmentBusinessModel>? Enrollments { get; set; }
}
