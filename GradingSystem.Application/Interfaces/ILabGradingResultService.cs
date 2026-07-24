using GradingSystem.Application.DTOs;

namespace GradingSystem.Application.Interfaces;

public interface ILabGradingResultService
{
    Task<LabGradingResultDto?> GetResultsBySubmissionAsync(Guid submissionId, CancellationToken ct = default);
    Task<LabTestCaseResultDto> AdjustScoreAsync(Guid submissionId, Guid resultId, decimal score, string reason, CancellationToken ct = default);
    Task<LabGradingResultDto> ImportCustomResultAsync(Guid submissionId, ImportLabCustomResultRequest request, CancellationToken ct = default);
}
