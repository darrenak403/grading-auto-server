namespace givenAPI.Models
{
    public partial class CourseAssignment
    {
        public int CourseId { get; set; }
        public int InstructorId { get; set; }
        public DateTime? AssignmentDate { get; set; }
    }
}