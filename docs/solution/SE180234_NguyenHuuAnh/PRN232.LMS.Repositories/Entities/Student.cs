namespace PRN232.LMS.Repositories.Entities;

public class Student
{
    public int StudentId { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public DateTime DateOfBirth { get; set; }

    // Navigation
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}
