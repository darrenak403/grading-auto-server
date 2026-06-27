using GradingSystem.Application.Messaging;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace GradingSystem.Tests.Worker.Consumers;

/// <summary>
/// Phase 5 (pe-grading-scale-reliability), Step 7: proves that
/// <c>UseConcurrentMessageLimit</c> + <c>Endpoint.ConcurrentMessageLimit</c> — the exact pair
/// wired onto GradeJobConsumer in Program.cs — actually caps concurrent Consume calls, not just
/// PrefetchCount (the gap this plan's investigation found). Uses a stand-in consumer instead of
/// the real GradeJobConsumer to avoid pulling in its full database/grading dependency graph.
/// </summary>
public class ConcurrentMessageLimitTests
{
    private static int _activeCount;
    private static int _observedMaxConcurrent;

    private class TrackingConsumer : IConsumer<GradeJobMessage>
    {
        private readonly TaskCompletionSource _release;

        public TrackingConsumer(TaskCompletionSource release)
        {
            _release = release;
        }

        public async Task Consume(ConsumeContext<GradeJobMessage> context)
        {
            var current = Interlocked.Increment(ref _activeCount);
            InterlockedMax(ref _observedMaxConcurrent, current);
            try
            {
                await _release.Task;
            }
            finally
            {
                Interlocked.Decrement(ref _activeCount);
            }
        }

        private static void InterlockedMax(ref int target, int candidate)
        {
            int initial, computed;
            do
            {
                initial = target;
                computed = Math.Max(initial, candidate);
            } while (Interlocked.CompareExchange(ref target, computed, initial) != initial);
        }
    }

    [Fact]
    public async Task ConcurrentMessageLimit_CapsConcurrentConsumeCallsBelowMessageCount()
    {
        _activeCount = 0;
        _observedMaxConcurrent = 0;
        const int concurrencyLimit = 2;
        const int messageCount = 6;
        var release = new TaskCompletionSource();

        var services = new ServiceCollection();
        services.AddSingleton(new TrackingConsumer(release));
        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<TrackingConsumer>(c =>
            {
                c.UseConcurrentMessageLimit(concurrencyLimit);
            }).Endpoint(e =>
            {
                e.PrefetchCount = concurrencyLimit;
                e.ConcurrentMessageLimit = concurrencyLimit;
            });
        });

        var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            for (var i = 0; i < messageCount; i++)
                await harness.Bus.Publish(new GradeJobMessage(Guid.NewGuid()));

            // Give the bus enough time to dispatch as many concurrent Consume calls as it will.
            await Task.Delay(500);
            Assert.True(_activeCount > 0, "Expected at least one in-flight Consume call before releasing.");

            release.SetResult();
            await Task.Delay(500);
        }
        finally
        {
            await harness.Stop();
        }

        Assert.True(_observedMaxConcurrent <= concurrencyLimit,
            $"ConcurrentMessageLimit={concurrencyLimit} was not enforced: observed {_observedMaxConcurrent} concurrent Consume calls.");
    }
}
