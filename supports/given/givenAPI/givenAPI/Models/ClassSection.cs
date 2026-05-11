namespace givenAPI.Models
{
    public partial class ClassSection
    {
        public int SectionId { get; set; }
        public int? CourseId { get; set; }
        public string? RoomNumber { get; set; }
        public string? Semester { get; set; }
        public int? MaxCapacity { get; set; }
    }
}