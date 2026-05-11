using GradingSystem.Application.DTOs;

namespace GradingSystem.Application.Interfaces;

public interface IExportService
{
    Task<ExportJobDto> CreateAsync(CreateExportRequest req, CancellationToken ct = default);
    Task<ExportJobDto> CreateSessionExportAsync(Guid sessionId, string? gradingRound, CancellationToken ct = default);
    Task<ExportJobDto?> GetByIdAsync(Guid exportJobId, CancellationToken ct = default);
    Task<string?> GetFilePathAsync(Guid exportJobId, CancellationToken ct = default);
}
