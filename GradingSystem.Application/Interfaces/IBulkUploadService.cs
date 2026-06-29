using GradingSystem.Application.DTOs;

namespace GradingSystem.Application.Interfaces;

public interface IBulkUploadService
{
    Task<BulkUploadResultDto> ParseAndCreateAsync(
        Guid assignmentId,
        string gradingRound,
        Stream masterZipStream,
        CancellationToken ct = default);

    /// <summary>Always targets the assignment's latest existing round, creating "Round 1" if none exists yet.</summary>
    Task<BulkUploadResultDto> ParseAndCreateForLatestRoundAsync(
        Guid assignmentId,
        Stream masterZipStream,
        CancellationToken ct = default);

    /// <summary>Computes the next sequential round label server-side and creates it, retrying on a unique-constraint collision.</summary>
    Task<BulkUploadResultDto> CreateNewRoundAsync(
        Guid assignmentId,
        Stream masterZipStream,
        CancellationToken ct = default);
}
