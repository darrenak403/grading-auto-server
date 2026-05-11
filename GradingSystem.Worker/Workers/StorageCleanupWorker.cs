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
        while (!ct.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            var vietnamOffset = TimeSpan.FromHours(7); // Múi giờ Việt Nam (UTC+7)
            var nowVietnam = now.ToOffset(vietnamOffset);

            // Tính toán thời điểm 00:00 của ngày tiếp theo theo giờ Việt Nam
            var nextMidnightVietnam = new DateTimeOffset(
                nowVietnam.Year, nowVietnam.Month, nowVietnam.Day,
                0, 0, 0, vietnamOffset).AddDays(1);

            var delay = nextMidnightVietnam - nowVietnam;

            logger.LogInformation("Next storage cleanup scheduled in {Hours}h {Minutes}m (at midnight Vietnam time)", delay.Hours, delay.Minutes);

            try
            {
                await Task.Delay(delay, ct);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            if (!ct.IsCancellationRequested)
            {
                RunCleanup();
            }
        }
    }

    private void RunCleanup()
    {
        var basePath = config["Storage:BasePath"];
        if (string.IsNullOrWhiteSpace(basePath)) return;

        int removedDirs = 0;
        int removedFiles = 0;

        logger.LogInformation("Starting midnight storage cleanup...");

        var cutoff7Days = DateTime.UtcNow - TimeSpan.FromDays(7);

        removedDirs += PurgeDirectories(Path.Combine(basePath, "submissions"), cutoff7Days);

        removedFiles += PurgeFiles(Path.Combine(basePath, "exports"), DateTime.UtcNow);

        logger.LogInformation("Midnight storage cleanup finished. Removed {DirCount} directories and {FileCount} files.", removedDirs, removedFiles);
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
