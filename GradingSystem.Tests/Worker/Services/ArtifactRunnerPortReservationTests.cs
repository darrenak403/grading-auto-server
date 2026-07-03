using GradingSystem.Worker.Options;
using GradingSystem.Worker.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GradingSystem.Tests.Worker.Services;

/// <summary>
/// Phase 1 (pe-grading-scale-reliability): proves PickPort/ReleasePort atomically
/// claim/release ports instead of the old check-then-act probe, closing the TOCTOU
/// race that lock removal would otherwise expose.
/// </summary>
public class ArtifactRunnerPortReservationTests
{
    private static ArtifactRunner CreateRunner(int rangeStart, int rangeEnd)
    {
        var opts = Options.Create(new WorkerOptions
        {
            ArtifactPortRangeStart = rangeStart,
            ArtifactPortRangeEnd = rangeEnd,
        });
        var config = new ConfigurationBuilder().Build();
        return new ArtifactRunner(opts, config, NullLogger<ArtifactRunner>.Instance);
    }

    [Fact]
    public void PickPort_ConcurrentCalls_NeverReturnDuplicatePorts()
    {
        var runner = CreateRunner(18000, 18100);
        const int callCount = 50;
        var results = new int[callCount];

        Parallel.For(0, callCount, i => results[i] = runner.PickPort());

        Assert.Equal(callCount, results.Distinct().Count());
    }

    [Fact]
    public void PickPort_AfterReleasePort_CanReuseThatPort()
    {
        // single-port range forces reuse to prove release actually frees the reservation
        var runner = CreateRunner(18200, 18200);

        var first = runner.PickPort();
        runner.ReleasePort(first);
        var second = runner.PickPort();

        Assert.Equal(first, second);
    }

    [Fact]
    public void PickPort_WithoutRelease_DoesNotReturnSamePortTwiceEvenIfOsLevelFree()
    {
        // single-port range: OS-level TcpListener probe alone would happily say "free"
        // a second time since the first PickPort already released its probe listener.
        // The in-memory reservation registry must still block re-allocation.
        var runner = CreateRunner(18300, 18300);

        runner.PickPort();

        Assert.Throws<InvalidOperationException>(() => runner.PickPort());
    }
}
