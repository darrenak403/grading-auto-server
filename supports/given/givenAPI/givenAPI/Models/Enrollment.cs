namespace givenAPI.Models
{
    public partial class Enrollment
    {
        public int EnrollmentId { get; set; }
        public int? StudentId { get; set; }
        public int? SectionId { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public double? Grade { get; set; }
    }
}