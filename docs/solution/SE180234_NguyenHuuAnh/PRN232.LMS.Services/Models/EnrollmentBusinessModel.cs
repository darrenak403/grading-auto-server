namespace PRN232.LMS.Services.Models;

/// <summary>Business model for Enrollment - used in Service layer processing</summary>
public class EnrollmentBusinessModel
{
    public int EnrollmentId { get; set; }
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public DateTime EnrollDate { get; set; }
    public string Status { get; set; } = null!;
    public StudentBusinessModel? Student { get; set; }
    public CourseBusinessModel? Course { get; set; }
}
