namespace givenAPI.Models
{
    public partial class Instructor
    {
        public int InstructorId { get; set; }
        public string FullName { get; set; } = null!;
        public string? Expertise { get; set; }
        public DateTime? HireDate { get; set; }
    }
}