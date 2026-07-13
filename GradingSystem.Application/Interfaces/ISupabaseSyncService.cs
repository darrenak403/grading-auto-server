using GradingSystem.Application.DTOs;

namespace GradingSystem.Application.Interfaces;

public interface ISupabaseSyncService
{
    Task SyncSubmissionAsync(Guid submissionId, string? labIdOverride = null, string? classNameOverride = null, string? termId = null, CancellationToken ct = default);
    Task<int> SyncAssignmentAsync(Guid assignmentId, SyncSupabaseRequest? request = null, CancellationToken ct = default);
    Task<SyncSupabaseGradeResponse> SyncGradeAsync(SyncSupabaseGradeRequest request, CancellationToken ct = default);
    Task<SyncSupabaseGradesResponse> SyncGradesAsync(SyncSupabaseGradesRequest request, CancellationToken ct = default);
    Task<SupabaseDropdownOptionsDto> GetDropdownOptionsAsync(string? termId = null, string? className = null, CancellationToken ct = default);
}
