using GradingSystem.Application.Interfaces;
using GradingSystem.Application.Messaging;
using GradingSystem.Domain.Entities;
using GradingSystem.Worker.Options;
using GradingSystem.Worker.Services;
using MassTransit;
using Microsoft.Extensions.Options;

namespace GradingSystem.Worker.Workers;

public class GradingWorker(
    IServiceScopeFactory scopeFactory,
    IBus bus,
    ExportRunner exportRunner,
    IOptions<WorkerOptions> opts,
    ILogger<GradingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("GradingWorker started — recovering pending jobs");

        // Crash recovery: re-enqueue any Pending jobs left from a previous run
        await RecoverPendingJobsAsync(ct);

        logger.LogInformation("GradingWorker polling exports every {Interval}s",
            opts.Value.PollIntervalSeconds);

        while (!ct.IsCancellationRequested)
        {
            await ProcessNextExportJobAsync(ct);
            await Task.Delay(TimeSpan.FromSeconds(opts.Value.PollIntervalSeconds), ct);
        }
    }

    private async Task RecoverPendingJobsAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var pending = (await uow.GradingJobs.FindAsync(j => j.Status == JobStatus.Pending))
                      .OrderBy(j => j.CreatedAt)
                      .ToList();

        foreach (var job in pending)
        {
            await bus.Publish(new GradeJobMessage(job.Id), ct);
            logger.LogInformation("Re-queued recovered job {JobId}", job.Id);
        }
    }

    private async Task ProcessNextExportJobAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var exportJob = (await uow.ExportJobs.FindAsync(j => j.Status == ExportStatus.Pending))
                        .OrderBy(j => j.CreatedAt)
                        .FirstOrDefault();

        if (exportJob == null) return;

        logger.LogInformation("Processing export job {JobId} for assignment {AssignmentId}",
            exportJob.Id, exportJob.AssignmentId);

        try
        {
            var path = await exportRunner.GenerateAsync(exportJob, uow, ct);
            exportJob.Status   = ExportStatus.Done;
            exportJob.FilePath = path;

            logger.LogInformation("Export job {JobId} completed: {Path}", exportJob.Id, path);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Export job {JobId} failed", exportJob.Id);
            exportJob.Status       = ExportStatus.Failed;
            exportJob.ErrorMessage = ex.Message;
        }

        uow.ExportJobs.Update(exportJob);
        await uow.SaveChangesAsync(ct);
    }
}
