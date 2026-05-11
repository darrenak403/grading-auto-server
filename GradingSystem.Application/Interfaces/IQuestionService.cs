using GradingSystem.Application.DTOs;

namespace GradingSystem.Application.Interfaces;

public interface IQuestionService
{
    Task<IReadOnlyList<QuestionDto>> CreateManyAsync(
        Guid assignmentId,
        IReadOnlyList<CreateQuestionRequest> requests,
        CancellationToken ct = default);
    Task<IEnumerable<QuestionDto>> GetByAssignmentIdAsync(Guid assignmentId, CancellationToken ct = default);
    Task<QuestionDto> DeleteAsync(Guid questionId, CancellationToken ct = default);
}
