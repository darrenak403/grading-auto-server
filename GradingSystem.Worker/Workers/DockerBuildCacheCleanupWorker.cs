using System.Diagnostics;
using GradingSystem.Worker.Options;
using Microsoft.Extensions.Options;

namespace GradingSystem.Worker.Workers;

public sealed class DockerBuildCacheCleanupWorker(
    ILogger<DockerBuildCacheCleanupWorker> logger,
    IOptions<WorkerOptions> options) : BackgroundService
{
    private const int MaxOutputChars = 12_000;
    private readonly WorkerOptions _opts = options.Value;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var intervalHours = _opts.LabDockerBuildCacheFullPruneIntervalHours;
        if (intervalHours <= 0)
        {
            logger.LogInformation("Docker build cache full prune worker disabled.");
            return;
        }

        var interval = TimeSpan.FromHours(intervalHours);
        logger.LogInformation("Docker build cache full prune scheduled every {IntervalHours}h.", intervalHours);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, ct);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            if (!ct.IsCancellationRequested)
                await PruneBuildCacheAsync(ct);
        }
    }

    private async Task PruneBuildCacheAsync(CancellationToken ct)
    {
        logger.LogInformation("Starting scheduled Docker build cache full prune...");

        var result = await RunDockerAsync(
            ["builder", "prune", "-a", "-f"],
            TimeSpan.FromMinutes(5),
            ct);

        if (result.ExitCode == 0)
        {
            logger.LogInformation(
                "Scheduled Docker build cache full prune finished: {Output}",
                string.IsNullOrWhiteSpace(result.Output) ? "(no output)" : result.Output.Trim());
            return;
        }

        logger.LogWarning(
            "Scheduled Docker build cache full prune failed (exit {ExitCode}): {Output}",
            result.ExitCode,
            result.Output);
    }

    private static async Task<DockerCommandResult> RunDockerAsync(
        IReadOnlyList<string> args,
        TimeSpan timeout,
        CancellationToken ct)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        var psi = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        try
        {
            using var proc = Process.Start(psi);
            if (proc is null)
                return new DockerCommandResult(-1, "Failed to start docker process.");

            var stdoutTask = proc.StandardOutput.ReadToEndAsync(linked.Token);
            var stderrTask = proc.StandardError.ReadToEndAsync(linked.Token);

            await proc.WaitForExitAsync(linked.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            return new DockerCommandResult(proc.ExitCode, CombineOutput(stdout, stderr));
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            return new DockerCommandResult(-1, $"docker {string.Join(' ', args)} timed out after {timeout.TotalMinutes:0}m.");
        }
        catch (Exception ex)
        {
            return new DockerCommandResult(-1, ex.Message);
        }
    }

    private static string CombineOutput(string stdout, string stderr)
    {
        var output = string.Join('\n',
            new[] { stdout, stderr }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return output.Length <= MaxOutputChars
            ? output
            : output[^MaxOutputChars..];
    }

    private sealed record DockerCommandResult(int ExitCode, string Output);
}
