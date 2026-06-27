using GradingSystem.Application.DTOs;

namespace GradingSystem.Application.Interfaces;

public interface IAssignmentService
{
    Task<AssignmentDto> CreateAsync(CreateAssignmentRequest req, CancellationToken ct = default);
    Task<AssignmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AssignmentSummaryDto>> GetSummariesAsync(CancellationToken ct = default);

    Task<AssignmentDto> UpsertResourcesAsync(
        Guid id,
        UpsertAssignmentResourcesRequest request,
        CancellationToken ct = default);

    Task<AssignmentDto> DeleteAsync(Guid assignmentId, CancellationToken ct = default);
    Task<ImportParticipantsResultDto> ImportParticipantsAsync(Guid assignmentId, Stream csvStream, CancellationToken ct = default);
    Task<IReadOnlyList<ParticipantDto>> GetParticipantsAsync(Guid assignmentId, CancellationToken ct = default);
    /// <summary>Always operates on the assignment's latest existing grading round.</summary>
    Task<int> TriggerGradeAsync(Guid assignmentId, CancellationToken ct = default);
}
