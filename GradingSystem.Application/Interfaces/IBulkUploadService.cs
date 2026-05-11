using GradingSystem.Application.DTOs;

namespace GradingSystem.Application.Interfaces;

public interface IBulkUploadService
{
    Task<BulkUploadResultDto> ParseAndCreateAsync(
        Guid assignmentId,
        string gradingRound,
        Stream masterZipStream,
        CancellationToken ct = default);
}
