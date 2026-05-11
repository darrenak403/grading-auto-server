namespace givenAPI.Models
{
    public partial class Student
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = null!;
        public string? Email { get; set; }
        public DateTime? DateOfBirth { get; set; }
    }
}