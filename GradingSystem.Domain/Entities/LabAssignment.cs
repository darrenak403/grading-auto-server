namespace GradingSystem.Domain.Entities;

public enum LabAssignmentStatus { Draft, TestcasesReady, Grading, Done }

public class LabAssignment : BaseEntity
{
    public Guid? SemesterId { get; set; }
    public Semester? Semester { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? PdfPath { get; set; }
    public LabAssignmentStatus Status { get; set; } = LabAssignmentStatus.Draft;

    public ICollection<LabTestCase> TestCases { get; set; } = [];
    public ICollection<LabSubmission> Submissions { get; set; } = [];
}
