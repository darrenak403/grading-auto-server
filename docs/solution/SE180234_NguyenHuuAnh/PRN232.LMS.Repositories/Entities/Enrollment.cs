namespace PRN232.LMS.Repositories.Entities;

public class Enrollment
{
    public int EnrollmentId { get; set; }
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public DateTime EnrollDate { get; set; }
    public string Status { get; set; } = null!;

    // Navigation
    public Student Student { get; set; } = null!;
    public Course Course { get; set; } = null!;
}
