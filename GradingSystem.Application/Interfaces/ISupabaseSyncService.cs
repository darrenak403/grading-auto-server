using GradingSystem.Application.DTOs;

namespace GradingSystem.Application.Interfaces;

public interface ISupabaseSyncService
{
    Task SyncSubmissionAsync(Guid submissionId, string? labIdOverride = null, string? classNameOverride = null, CancellationToken ct = default);
    Task<int> SyncAssignmentAsync(Guid assignmentId, SyncSupabaseRequest? request = null, CancellationToken ct = default);
}
