using GradingSystem.Worker.Options;
using Microsoft.Extensions.Options;

namespace GradingSystem.Worker.Workers;

public sealed class StorageCleanupWorker(
    ILogger<StorageCleanupWorker> logger,
    IConfiguration config,
    IOptions<StorageCleanupOptions> options) : BackgroundService
{
    private readonly StorageCleanupOptions _opts = options.Value;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(_opts.IntervalHours));
        while (await timer.WaitForNextTickAsync(ct))
            RunCleanup();
    }

    private void RunCleanup()
    {
        var basePath = config["Storage:BasePath"];
        if (string.IsNullOrWhiteSpace(basePath)) return;

        var cutoff = DateTime.UtcNow - TimeSpan.FromDays(_opts.RetentionDays);
        int removed = 0;

        removed += PurgeFiles(Path.Combine(basePath, "exports"), cutoff);

        if (removed > 0)
            logger.LogInformation("Storage cleanup removed {Count} item(s) older than {Days}d",
                removed, _opts.RetentionDays);
    }

    private int PurgeDirectories(string root, DateTime cutoff)
    {
        if (!Directory.Exists(root)) return 0;
        int count = 0;
        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            try
            {
                if (Directory.GetLastWriteTimeUtc(dir) < cutoff)
                {
                    Directory.Delete(dir, recursive: true);
                    count++;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not delete directory {Dir}", dir);
            }
        }
        return count;
    }

    private int PurgeFiles(string root, DateTime cutoff)
    {
        if (!Directory.Exists(root)) return 0;
        int count = 0;
        foreach (var file in Directory.EnumerateFiles(root))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    File.Delete(file);
                    count++;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not delete file {File}", file);
            }
        }
        return count;
    }
}
