namespace PRN232.LMS.Services.Models;

/// <summary>Business model for Semester - used in Service layer processing</summary>
public class SemesterBusinessModel
{
    public int SemesterId { get; set; }
    public string SemesterName { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int CourseCount { get; set; }
    public List<CourseBusinessModel>? Courses { get; set; }
}
