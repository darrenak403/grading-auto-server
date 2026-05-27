using GradingSystem.Application.DTOs;

namespace GradingSystem.Application.Interfaces;

public interface ILabTestCaseService
{
    Task<LabTestCaseDto> CreateAsync(Guid assignmentId, CreateLabTestCaseRequest req, CancellationToken ct = default);
    Task<LabTestCaseDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<LabTestCaseDto> UpdateAsync(Guid id, UpdateLabTestCaseRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<LabTestCaseDto> ApproveAsync(Guid id, CancellationToken ct = default);
    Task<LabTestCaseDto> RejectAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<LabTestCaseDto>> BulkCreateAsync(Guid assignmentId, IEnumerable<CreateLabTestCaseRequest> reqs, CancellationToken ct = default);
}
