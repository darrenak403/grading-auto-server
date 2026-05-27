using GradingSystem.Application.Interfaces;
using GradingSystem.Application.Messaging;
using GradingSystem.Domain.Entities;
using GradingSystem.Worker.Options;
using MassTransit;
using Microsoft.Extensions.Options;

namespace GradingSystem.Worker.Workers;

public class LabGradingWorker(
    IServiceScopeFactory scopeFactory,
    IBus bus,
    IOptions<WorkerOptions> opts,
    ILogger<LabGradingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("LabGradingWorker started — recovering pending jobs");
        await RecoverPendingJobsAsync(ct);

        while (!ct.IsCancellationRequested)
        {
            await EnqueuePendingJobsAsync(ct);
            await Task.Delay(TimeSpan.FromSeconds(opts.Value.PollIntervalSeconds), ct);
        }
    }

    private async Task RecoverPendingJobsAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Jobs left Running when worker crashed — reset to Pending so they get re-graded
        var stale = (await uow.LabGradingJobs.FindAsync(j => j.Status == LabGradingJobStatus.Running)).ToList();
        foreach (var job in stale)
        {
            job.Status = LabGradingJobStatus.Pending;
            uow.LabGradingJobs.Update(job);
            logger.LogInformation("Reset stale Running lab job {JobId} → Pending", job.Id);
        }

        if (stale.Count > 0)
            await uow.SaveChangesAsync(ct);

        await EnqueuePendingJobsAsync(ct, uow);
    }

    private async Task EnqueuePendingJobsAsync(CancellationToken ct, IUnitOfWork? existingUow = null)
    {
        if (existingUow is not null)
        {
            await PublishPendingAsync(existingUow, ct);
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await PublishPendingAsync(uow, ct);
    }

    private async Task PublishPendingAsync(IUnitOfWork uow, CancellationToken ct)
    {
        var pending = (await uow.LabGradingJobs.FindAsync(j => j.Status == LabGradingJobStatus.Pending))
            .OrderBy(j => j.CreatedAt)
            .ToList();

        foreach (var job in pending)
        {
            await bus.Publish(new LabGradeJobMessage(job.Id), ct);
            logger.LogInformation("Enqueued lab job {JobId}", job.Id);
        }
    }
}
