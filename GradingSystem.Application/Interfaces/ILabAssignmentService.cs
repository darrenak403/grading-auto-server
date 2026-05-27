using GradingSystem.Application.DTOs;

namespace GradingSystem.Application.Interfaces;

public interface ILabAssignmentService
{
    Task<IEnumerable<LabAssignmentDto>> ListAsync(CancellationToken ct = default);
    Task<LabAssignmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<LabAssignmentDto> CreateAsync(CreateLabAssignmentRequest req, CancellationToken ct = default);
    Task<LabAssignmentDto> UpdateAsync(Guid id, UpdateLabAssignmentRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<LabTestCaseDto>> GetTestCasesAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<LabAssignmentRosterItemDto>> GetRosterAsync(Guid id, CancellationToken ct = default);
    Task<LabGradingProgressDto> GetGradingProgressAsync(Guid id, CancellationToken ct = default);
    Task<int> TriggerGradingAsync(Guid id, CancellationToken ct = default);
}
