using GradingSystem.Worker.Services;

namespace GradingSystem.Tests.Worker.Services;

/// <summary>
/// Phase 2 (pe-grading-scale-reliability): CPU-aware default formula and the
/// "explicit config wins" resolution rule must be testable without real hardware.
/// </summary>
public class ConcurrencyCalculatorTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(4, 3)]
    [InlineData(9, 8)]
    [InlineData(16, 8)]
    [InlineData(100, 8)]
    public void ComputeDefaultMaxConcurrentJobs_ClampsBetweenFloorAndCeiling(int processorCount, int expected)
    {
        var result = ConcurrencyCalculator.ComputeDefaultMaxConcurrentJobs(processorCount);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveMaxConcurrentJobs_WhenConfigured_UsesConfiguredValueRegardlessOfProcessorCount()
    {
        var result = ConcurrencyCalculator.ResolveMaxConcurrentJobs(
            rawConfigValue: "3", configuredValue: 3, processorCount: 16);

        Assert.Equal(3, result);
    }

    [Fact]
    public void ResolveMaxConcurrentJobs_WhenNotConfigured_UsesCpuAwareDefault()
    {
        var result = ConcurrencyCalculator.ResolveMaxConcurrentJobs(
            rawConfigValue: null, configuredValue: 3, processorCount: 5);

        Assert.Equal(4, result);
    }

    [Fact]
    public void ResolveMaxConcurrentJobs_WhenExplicitlySetToOldDefaultValue_StillTreatedAsConfigured()
    {
        // Distinguishes "unset" from "explicitly pinned to the old hardcoded default (3)" —
        // the raw key's presence is what matters, not whether the value happens to equal 3.
        var result = ConcurrencyCalculator.ResolveMaxConcurrentJobs(
            rawConfigValue: "3", configuredValue: 3, processorCount: 32);

        Assert.Equal(3, result);
    }
}
