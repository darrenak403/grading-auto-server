using GradingSystem.Domain.Entities;

namespace GradingSystem.Application.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IGenericRepository<ExamSession> ExamSessions { get; }
    IGenericRepository<Participant> Participants { get; }
    IGenericRepository<Assignment> Assignments { get; }
    IGenericRepository<Question> Questions { get; }
    IGenericRepository<TestCase> TestCases { get; }
    IGenericRepository<Submission> Submissions { get; }
    IGenericRepository<GradingJob> GradingJobs { get; }
    IGenericRepository<QuestionResult> QuestionResults { get; }
    IGenericRepository<ReviewNote> ReviewNotes { get; }
    IGenericRepository<ExportJob> ExportJobs { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
