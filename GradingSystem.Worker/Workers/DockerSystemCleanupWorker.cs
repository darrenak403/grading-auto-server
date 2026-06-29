using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using GradingSystem.Worker.Options;
using Microsoft.Extensions.Options;

namespace GradingSystem.Worker.Workers;

public sealed class DockerSystemCleanupWorker(
    ILogger<DockerSystemCleanupWorker> logger,
    IOptions<WorkerOptions> options) : BackgroundService
{
    private const int MaxOutputChars = 12_000;
    private readonly WorkerOptions _opts = options.Value;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var intervalHours = _opts.LabDockerSystemPruneIntervalHours;
        var thresholdGb = _opts.LabDockerSystemPruneReclaimableThresholdGb;
        if (intervalHours <= 0 || thresholdGb <= 0)
        {
            logger.LogInformation("Docker system prune worker disabled.");
            return;
        }

        var interval = TimeSpan.FromHours(intervalHours);
        logger.LogInformation(
            "Docker system prune scheduled every {IntervalHours}h when image reclaimable size is >= {ThresholdGb:0.##}GB.",
            intervalHours,
            thresholdGb);

        await TryPruneIfThresholdExceededAsync(ct);

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
                await TryPruneIfThresholdExceededAsync(ct);
        }
    }

    private async Task TryPruneIfThresholdExceededAsync(CancellationToken ct)
    {
        try
        {
            await PruneIfThresholdExceededAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Docker system prune check failed.");
        }
    }

    private async Task PruneIfThresholdExceededAsync(CancellationToken ct)
    {
        var df = await RunDockerAsync(["system", "df", "--format", "json"], TimeSpan.FromSeconds(30), ct);
        if (df.ExitCode != 0)
        {
            logger.LogWarning("Docker system df failed (exit {ExitCode}): {Output}", df.ExitCode, df.Output);
            return;
        }

        var rows = ParseSystemDf(df.Output).ToList();
        var imageReclaimableGb = rows
            .Where(r => r.Type.Equals("Images", StringComparison.OrdinalIgnoreCase))
            .Select(r => r.ReclaimableGb)
            .FirstOrDefault();

        if (imageReclaimableGb < _opts.LabDockerSystemPruneReclaimableThresholdGb)
        {
            logger.LogInformation(
                "Docker image prune skipped: reclaimable={ReclaimableGb:0.##}GB below threshold={ThresholdGb:0.##}GB.",
                imageReclaimableGb,
                _opts.LabDockerSystemPruneReclaimableThresholdGb);
            return;
        }

        logger.LogInformation(
            "Docker image reclaimable size {ReclaimableGb:0.##}GB reached threshold {ThresholdGb:0.##}GB. Starting prune.",
            imageReclaimableGb,
            _opts.LabDockerSystemPruneReclaimableThresholdGb);

        await RunAndLogPruneAsync(["image", "prune", "-a", "-f"], "Docker image prune", ct);

        if (_opts.LabDockerSystemPruneVolumes)
            await RunAndLogPruneAsync(["volume", "prune", "-f"], "Docker volume prune", ct);
    }

    private async Task RunAndLogPruneAsync(string[] args, string name, CancellationToken ct)
    {
        var result = await RunDockerAsync(args, TimeSpan.FromMinutes(10), ct);
        if (result.ExitCode == 0)
        {
            logger.LogInformation("{Name} finished: {Output}", name,
                string.IsNullOrWhiteSpace(result.Output) ? "(no output)" : result.Output.Trim());
            return;
        }

        logger.LogWarning("{Name} failed (exit {ExitCode}): {Output}", name, result.ExitCode, result.Output);
    }

    private static IEnumerable<DockerDfRow> ParseSystemDf(string output)
    {
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var type = root.TryGetProperty("Type", out var typeEl) ? typeEl.GetString() ?? string.Empty : string.Empty;
            var reclaimable = root.TryGetProperty("Reclaimable", out var reclaimableEl)
                ? reclaimableEl.GetString() ?? string.Empty
                : string.Empty;

            yield return new DockerDfRow(type, ParseReclaimableGb(reclaimable));
        }
    }

    private static double ParseReclaimableGb(string value)
    {
        var size = value.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? value;
        size = size.Trim();
        if (size.Length == 0) return 0;

        var unitStart = size.TakeWhile(ch => char.IsDigit(ch) || ch == '.').Count();
        if (unitStart == 0) return 0;

        var numberText = size[..unitStart];
        var unit = size[unitStart..].Trim().ToUpperInvariant();
        if (!double.TryParse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount))
            return 0;

        return unit switch
        {
            "TB" => amount * 1024,
            "GB" => amount,
            "MB" => amount / 1024,
            "KB" => amount / 1024 / 1024,
            "B" => amount / 1024 / 1024 / 1024,
            _ => amount
        };
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

    private sealed record DockerDfRow(string Type, double ReclaimableGb);
    private sealed record DockerCommandResult(int ExitCode, string Output);
}
