namespace PRN232.LMS.API.Models.Responses;

public class SemesterResponse
{
    public int SemesterId { get; set; }
    public string SemesterName { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int CourseCount { get; set; }
    public List<CourseResponse>? Courses { get; set; }
}
