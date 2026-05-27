using GradingSystem.Application.DTOs;

namespace GradingSystem.Application.Interfaces;

public interface ILabSubmissionService
{
    Task<IEnumerable<LabSubmissionDto>> ListAsync(Guid? assignmentId = null, CancellationToken ct = default);
    Task<LabSubmissionDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<LabBatchUploadResult> BatchUploadAsync(Guid assignmentId, IEnumerable<LabUploadFile> files, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> DeleteAllByAssignmentAsync(Guid assignmentId, CancellationToken ct = default);
    Task RegradeAsync(Guid id, CancellationToken ct = default);
}
