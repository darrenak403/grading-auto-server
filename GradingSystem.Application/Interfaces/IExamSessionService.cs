using GradingSystem.Application.DTOs;

namespace GradingSystem.Application.Interfaces;

public interface IExamSessionService
{
    Task<ExamSessionDto> CreateAsync(CreateExamSessionRequest req, CancellationToken ct = default);
    Task<ExamSessionDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ExamSessionSummaryDto>> GetAllAsync(CancellationToken ct = default);
    Task<ExamSessionDto> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ParticipantDto>> GetParticipantsAsync(Guid sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<SessionSubmissionResultDto>> GetSessionResultsAsync(Guid sessionId, string? gradingRound, CancellationToken ct = default);
}
