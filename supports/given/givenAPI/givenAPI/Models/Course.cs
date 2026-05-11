namespace givenAPI.Models
{
    public partial class Course
    {
        public int CourseId { get; set; }
        public string CourseName { get; set; } = null!;
        public int? Credits { get; set; }
        public string? Department { get; set; }
    }
}