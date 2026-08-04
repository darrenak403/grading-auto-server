using GradingSystem.Application.Interfaces;
using GradingSystem.Domain.Entities;

namespace GradingSystem.Tests.Fakes;

/// <summary>In-memory IUnitOfWork for Application-layer service unit tests.</summary>
public class FakeUnitOfWork : IUnitOfWork
{
    public FakeRepository<ExamSession> ExamSessionsRepo { get; } = new();
    public FakeRepository<Participant> ParticipantsRepo { get; } = new();
    public FakeRepository<Assignment> AssignmentsRepo { get; } = new();
    public FakeRepository<Question> QuestionsRepo { get; } = new();
    public FakeRepository<TestCase> TestCasesRepo { get; } = new();
    public FakeRepository<Submission> SubmissionsRepo { get; } = new();
    public FakeRepository<GradingJob> GradingJobsRepo { get; } = new();
    public FakeRepository<QuestionResult> QuestionResultsRepo { get; } = new();
    public FakeRepository<ReviewNote> ReviewNotesRepo { get; } = new();
    public FakeRepository<ExportJob> ExportJobsRepo { get; } = new();

    public FakeRepository<Semester> SemestersRepo { get; } = new();
    public FakeRepository<LabAssignment> LabAssignmentsRepo { get; } = new();
    public FakeRepository<LabTestCase> LabTestCasesRepo { get; } = new();
    public FakeRepository<LabSubmission> LabSubmissionsRepo { get; } = new();
    public FakeRepository<LabGradingJob> LabGradingJobsRepo { get; } = new();
    public FakeRepository<LabTestCaseResult> LabTestCaseResultsRepo { get; } = new();

    public IGenericRepository<ExamSession> ExamSessions => ExamSessionsRepo;
    public IGenericRepository<Participant> Participants => ParticipantsRepo;
    public IGenericRepository<Assignment> Assignments => AssignmentsRepo;
    public IGenericRepository<Question> Questions => QuestionsRepo;
    public IGenericRepository<TestCase> TestCases => TestCasesRepo;
    public IGenericRepository<Submission> Submissions => SubmissionsRepo;
    public IGenericRepository<GradingJob> GradingJobs => GradingJobsRepo;
    public IGenericRepository<QuestionResult> QuestionResults => QuestionResultsRepo;
    public IGenericRepository<ReviewNote> ReviewNotes => ReviewNotesRepo;
    public IGenericRepository<ExportJob> ExportJobs => ExportJobsRepo;

    public IGenericRepository<Semester> Semesters => SemestersRepo;
    public IGenericRepository<LabAssignment> LabAssignments => LabAssignmentsRepo;
    public IGenericRepository<LabTestCase> LabTestCases => LabTestCasesRepo;
    public IGenericRepository<LabSubmission> LabSubmissions => LabSubmissionsRepo;
    public IGenericRepository<LabGradingJob> LabGradingJobs => LabGradingJobsRepo;
    public IGenericRepository<LabTestCaseResult> LabTestCaseResults => LabTestCaseResultsRepo;

    public int SaveChangesCallCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        SaveChangesCallCount++;
        return Task.FromResult(0);
    }

    public void ClearChanges() { }

    public void Dispose() { }
}
