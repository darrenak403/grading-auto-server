using GradingSystem.Application.DTOs;

namespace GradingSystem.Application.Interfaces;

public interface ISubmissionService
{
    Task<SubmissionDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SubmissionDto>> GetByAssignmentIdAsync(Guid assignmentId, string? studentCode, CancellationToken ct = default);
    Task<IEnumerable<QuestionResultDto>> GetResultsAsync(Guid submissionId, CancellationToken ct = default);
    Task<SubmissionDto> DeleteAsync(Guid submissionId, CancellationToken ct = default);
}
