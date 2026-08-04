namespace PRN232.LMS.Services.Models;

/// <summary>Business model for Course - used in Service layer processing</summary>
public class CourseBusinessModel
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = null!;
    public int SemesterId { get; set; }
    public int EnrollmentCount { get; set; }
    public SemesterBusinessModel? Semester { get; set; }
    public List<EnrollmentBusinessModel>? Enrollments { get; set; }
}
