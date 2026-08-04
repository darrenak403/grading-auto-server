using GradingSystem.Application.DTOs;

namespace GradingSystem.Application.Interfaces;

public interface IExamSessionService
{
    Task<ExamSessionDto> CreateAsync(CreateExamSessionRequest req, CancellationToken ct = default);
    Task<ExamSessionDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ExamSessionSummaryDto>> GetAllAsync(CancellationToken ct = default);
    Task<ExamSessionDto> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ParticipantDto>> GetParticipantsAsync(Guid sessionId, Guid? assignmentId = null, CancellationToken ct = default);
    Task<IReadOnlyList<SessionSubmissionResultDto>> GetSessionResultsAsync(Guid sessionId, string? gradingRound, Guid? assignmentId = null, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetRoundsAsync(Guid sessionId, Guid? assignmentId = null, CancellationToken ct = default);
    Task<ImportSessionParticipantsResultDto> ImportParticipantsByCodeAsync(Guid sessionId, Stream csvStream, CancellationToken ct = default);
}
