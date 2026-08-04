namespace PRN232.LMS.Repositories.Entities;

public class Course
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = null!;
    public int SemesterId { get; set; }

    // Navigation
    public Semester Semester { get; set; } = null!;
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}
