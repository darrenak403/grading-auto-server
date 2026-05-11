using GradingSystem.Application.DTOs;

namespace GradingSystem.Application.Interfaces;

public interface IQuestionResultService
{
    Task<QuestionResultDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<QuestionResultDto>> GetBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default);
    Task<QuestionResultDto> AdjustAsync(Guid id, AdjustScoreRequest req, CancellationToken ct = default);
    Task<QuestionResultDto> ClearAdjustAsync(Guid id, CancellationToken ct = default);
}
