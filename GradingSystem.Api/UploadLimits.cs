namespace GradingSystem.Api;

internal static class UploadLimits
{
    public const long MaxBulkUploadBytes = 2L * 1024 * 1024 * 1024; // 2 GB
}
