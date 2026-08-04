namespace PRN232.LMS.API.Models.Responses;

public class CourseResponse
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = null!;
    public int SemesterId { get; set; }
    public int EnrollmentCount { get; set; }
    public SemesterResponse? Semester { get; set; }
    public List<EnrollmentResponse>? Enrollments { get; set; }
}
