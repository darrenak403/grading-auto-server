using GradingSystem.Application.DTOs;

namespace GradingSystem.Application.Interfaces;

public interface IGradingJobService
{
    Task<GradingJobDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<GradingJobDto>> GetBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default);
}
