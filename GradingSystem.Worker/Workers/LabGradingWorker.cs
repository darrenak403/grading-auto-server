using GradingSystem.Application.Interfaces;
using GradingSystem.Application.Messaging;
using GradingSystem.Domain.Entities;
using GradingSystem.Worker.Options;
using GradingSystem.Worker.Services.Lab;
using MassTransit;
using Microsoft.Extensions.Options;

namespace GradingSystem.Worker.Workers;

public class LabGradingWorker(
    IServiceScopeFactory scopeFactory,
    IBus bus,
    IOptions<WorkerOptions> opts,
    DockerComposeRunner docker,
    ILogger<LabGradingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("LabGradingWorker started — recovering pending jobs");

        try
        {
            await RecoverPendingJobsAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "LabGradingWorker: recovery pass failed — will retry on next poll");
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await EnqueuePendingJobsAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "LabGradingWorker: poll cycle failed — will retry in {Interval}s",
                    opts.Value.PollIntervalSeconds);
            }

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
            try { await docker.StopAsync(job.Id); }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to clean stale Docker resources for lab job {JobId}", job.Id); }

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
        var running = (await uow.LabGradingJobs.FindAsync(j => j.Status == LabGradingJobStatus.Running))
            .OrderBy(j => j.StartedAt ?? j.CreatedAt)
            .ThenBy(j => j.CreatedAt)
            .FirstOrDefault();
        if (running is not null)
        {
            logger.LogDebug("Lab job {JobId} is still running — waiting before publishing the next job", running.Id);
            return;
        }

        var job = (await uow.LabGradingJobs.FindAsync(j => j.Status == LabGradingJobStatus.Pending))
            .OrderBy(j => j.CreatedAt)
            .FirstOrDefault();
        if (job is null) return;

        // Reserve exactly one job. The pipeline accepts Running jobs, and no next job is
        // published until this one is saved as Done/Failed.
        job.Status = LabGradingJobStatus.Running;
        uow.LabGradingJobs.Update(job);
        await uow.SaveChangesAsync(ct);

        try
        {
            await bus.Publish(new LabGradeJobMessage(job.Id), ct);
            logger.LogInformation("Enqueued lab job {JobId}", job.Id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to publish lab job {JobId} — resetting to Pending", job.Id);
            job.Status = LabGradingJobStatus.Pending;
            uow.LabGradingJobs.Update(job);
            await uow.SaveChangesAsync(ct);
        }
    }
}
