namespace GradingSystem.Domain.Entities;

public enum QuestionType { Api, Razor }

public class Question : BaseEntity
{
    public Guid AssignmentId { get; set; }
    public Assignment Assignment { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public QuestionType Type { get; set; }   // Api = Q1, Razor = Q2
    public int MaxScore { get; set; }
    // Must match the real per-question folder name inside a student's submission zip
    // (the submission client names these by question number, e.g. "1", "2" — NOT "Q1"/"Q2").
    public string ArtifactFolderName { get; set; } = string.Empty;

    public ICollection<TestCase> TestCases { get; set; } = [];
}
