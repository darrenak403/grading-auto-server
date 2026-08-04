using GradingSystem.Application.Interfaces;
using GradingSystem.Domain.Entities;

namespace GradingSystem.Infrastructure.Persistence;

public class UnitOfWork(GradingDbContext db) : IUnitOfWork
{
    public IGenericRepository<ExamSession>    ExamSessions    { get; } = new GenericRepository<ExamSession>(db);
    public IGenericRepository<Participant>    Participants    { get; } = new GenericRepository<Participant>(db);
    public IGenericRepository<Assignment>     Assignments     { get; } = new GenericRepository<Assignment>(db);
    public IGenericRepository<Question>       Questions       { get; } = new GenericRepository<Question>(db);
    public IGenericRepository<TestCase>       TestCases       { get; } = new GenericRepository<TestCase>(db);
    public IGenericRepository<Submission>     Submissions     { get; } = new GenericRepository<Submission>(db);
    public IGenericRepository<GradingJob>     GradingJobs     { get; } = new GenericRepository<GradingJob>(db);
    public IGenericRepository<QuestionResult> QuestionResults { get; } = new GenericRepository<QuestionResult>(db);
    public IGenericRepository<ReviewNote>     ReviewNotes     { get; } = new GenericRepository<ReviewNote>(db);
    public IGenericRepository<ExportJob>      ExportJobs      { get; } = new GenericRepository<ExportJob>(db);

    // Lab grading
    public IGenericRepository<Semester>          Semesters          { get; } = new GenericRepository<Semester>(db);
    public IGenericRepository<LabAssignment>     LabAssignments     { get; } = new GenericRepository<LabAssignment>(db);
    public IGenericRepository<LabTestCase>       LabTestCases       { get; } = new GenericRepository<LabTestCase>(db);
    public IGenericRepository<LabSubmission>     LabSubmissions     { get; } = new GenericRepository<LabSubmission>(db);
    public IGenericRepository<LabGradingJob>     LabGradingJobs     { get; } = new GenericRepository<LabGradingJob>(db);
    public IGenericRepository<LabTestCaseResult> LabTestCaseResults { get; } = new GenericRepository<LabTestCaseResult>(db);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
    
    public void ClearChanges() => db.ChangeTracker.Clear();

    public void Dispose() => db.Dispose();
}
