using GradingSystem.Application.DTOs;

namespace GradingSystem.Application.Interfaces;

public interface ISubmissionService
{
    Task<SubmissionDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SubmissionDto>> GetByAssignmentIdAsync(Guid assignmentId, string? studentCode, string? gradingRound, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetRoundsAsync(Guid assignmentId, CancellationToken ct = default);
    Task<IEnumerable<QuestionResultDto>> GetResultsAsync(Guid submissionId, CancellationToken ct = default);
    Task<IReadOnlyList<QuestionResultDto>> ImportCustomResultAsync(
        Guid submissionId,
        ImportCustomResultRequest request,
        CancellationToken ct = default);
    Task<SubmissionDto> DeleteAsync(Guid submissionId, CancellationToken ct = default);
    Task<GradingJobDto> RegradeAsync(Guid submissionId, CancellationToken ct = default);
}
