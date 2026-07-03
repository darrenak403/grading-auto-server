namespace GradingSystem.Worker.Services;

/// <summary>
/// CPU-aware default for Worker:MaxConcurrentJobs. Leaves one core free since the host is
/// expected to be a shared dev/CI machine, not a dedicated grading server; floors at 1 so a
/// single-core host still makes forward progress; caps at 8 because SQL Server sandbox setup,
/// port allocation, and disk I/O become the bottleneck before CPU does at higher concurrency.
/// </summary>
public static class ConcurrencyCalculator
{
    private const int MinConcurrentJobs = 1;
    private const int MaxConcurrentJobsCeiling = 8;

    public static int ComputeDefaultMaxConcurrentJobs(int processorCount)
        => Math.Clamp(processorCount - 1, MinConcurrentJobs, MaxConcurrentJobsCeiling);

    /// <summary>
    /// <paramref name="rawConfigValue"/> must be the raw config string (e.g.
    /// <c>configuration["Worker:MaxConcurrentJobs"]</c>), not the bound <c>WorkerOptions</c> int —
    /// only the raw value can distinguish "unset" from "explicitly pinned to the old default (3)".
    /// </summary>
    public static int ResolveMaxConcurrentJobs(string? rawConfigValue, int configuredValue, int processorCount)
        => rawConfigValue is not null
            ? configuredValue
            : ComputeDefaultMaxConcurrentJobs(processorCount);
}
