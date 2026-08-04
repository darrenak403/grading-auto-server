using GradingSystem.Application.DTOs;

namespace GradingSystem.Application.Interfaces;

public interface ISemesterService
{
    Task<IEnumerable<SemesterDto>> ListAsync(CancellationToken ct = default);
    Task<SemesterDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SemesterDto> CreateAsync(CreateSemesterRequest req, CancellationToken ct = default);
    Task<SemesterDto> UpdateAsync(Guid id, UpdateSemesterRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
