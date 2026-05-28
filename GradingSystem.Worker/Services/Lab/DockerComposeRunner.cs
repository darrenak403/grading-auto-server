using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Globalization;
using System.Net.Sockets;
using System.Security;
using System.Text.RegularExpressions;
using GradingSystem.Worker.Options;
using Microsoft.Extensions.Options;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace GradingSystem.Worker.Services.Lab;

public class DockerComposeException(string message, Exception? inner = null) : Exception(message, inner);
public class DockerComposeTimeoutException(string message) : Exception(message);

public partial class DockerComposeRunner(
    IOptions<WorkerOptions> opts,
    ILogger<DockerComposeRunner> logger)
{
    private static readonly string WorkRoot = Path.Combine(Path.GetTempPath(), "lab-grading");
    private const int MaxDockerOutputChars = 12_000;
    private readonly ConcurrentDictionary<Guid, string> _composeDirs = new();

    [GeneratedRegex(@"^( {2}|\t)([a-zA-Z0-9][a-zA-Z0-9_.-]*):\s*(?:#.*)?$")]
    private static partial Regex ServiceIndentRegex();

    [GeneratedRegex(@"^\s*target:\s*['""]?(\d+)['""]?\s*(?:#.*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex LongPortTargetRegex();

    /// <summary>Extracts the submission archive to a temp workdir. Returns the workdir path.</summary>
    public static string Extract(string archivePath, Guid jobId)
    {
        var workDir = Path.Combine(WorkRoot, jobId.ToString());
        Directory.CreateDirectory(workDir);
        ExtractArchive(archivePath, workDir);
        return workDir;
    }

    /// <summary>Starts Docker containers from an already-extracted workdir. Returns the assigned API port.</summary>
    public async Task<int> StartContainersAsync(string workDir, Guid jobId, CancellationToken ct)
    {
        var composePath = Directory.GetFiles(workDir, "docker-compose.yml", SearchOption.AllDirectories)
            .FirstOrDefault() ?? throw new DockerComposeException("docker-compose.yml not found in archive.");

        var composeDir = Path.GetFullPath(Path.GetDirectoryName(composePath)!);
        var safeRoot = Path.GetFullPath(workDir) + Path.DirectorySeparatorChar;
        if (!composeDir.StartsWith(safeRoot, StringComparison.Ordinal) && composeDir + Path.DirectorySeparatorChar != safeRoot)
            throw new SecurityException($"docker-compose.yml found outside workDir: {composeDir}");

        var (serviceName, containerPort) = DetectApiService(composePath);
        var apiPort = PickPort(opts.Value.LabApiPortRangeStart, opts.Value.LabApiPortRangeEnd);

        _composeDirs[jobId] = composeDir;
        await CleanupDockerResourcesAsync(jobId, composeDir, removeWorkDir: false);

        StripHostPortsAndContainerNames(composeDir);
        WriteOverride(composeDir, apiPort, serviceName, containerPort,
            opts.Value.LabContainerMemoryLimit, opts.Value.LabContainerCpuLimit);

        await RunDockerComposeAsync(composeDir, jobId, ct, "up", "-d", "--build");

        logger.LogInformation("Docker compose up for job {JobId} on API port {Port}", jobId, apiPort);

        await WaitForApiAsync(apiPort, jobId, ct);

        return apiPort;
    }

    public async Task<int> StartAsync(string archivePath, Guid jobId, CancellationToken ct)
    {
        var workDir = Extract(archivePath, jobId);
        return await StartContainersAsync(workDir, jobId, ct);
    }

    public string GetApiBaseUrl(int port) => $"http://{opts.Value.LabApiHost}:{port}";

    public async Task StopAsync(Guid jobId)
    {
        _composeDirs.TryRemove(jobId, out var composeDir);
        var downDir = !string.IsNullOrEmpty(composeDir) && Directory.Exists(composeDir) ? composeDir : "/tmp";

        await CleanupDockerResourcesAsync(jobId, downDir, removeWorkDir: true);

        logger.LogInformation("Docker compose down complete for job {JobId}", jobId);
    }

    public async Task<string> GetLogsAsync(Guid jobId)
    {
        _composeDirs.TryGetValue(jobId, out var composeDir);
        var logsDir = !string.IsNullOrEmpty(composeDir) && Directory.Exists(composeDir) ? composeDir : "/tmp";

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await RunDockerAsync(logsDir, cts.Token,
            ["compose", "-p", $"lab-{jobId}", "logs", "--no-color"]);
        return result.Output;
    }

    private static void ExtractArchive(string archivePath, string workDir)
    {
        var ext = Path.GetExtension(archivePath).ToLowerInvariant();
        if (ext == ".zip")
        {
            ZipFile.ExtractToDirectory(archivePath, workDir);
            return;
        }

        // .rar or other SharpCompress-supported formats
        var safeWorkDir = Path.GetFullPath(workDir) + Path.DirectorySeparatorChar;
        using var archive = ArchiveFactory.OpenArchive(new FileInfo(archivePath));
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            var destPath = Path.GetFullPath(Path.Combine(workDir, entry.Key ?? string.Empty));
            if (!destPath.StartsWith(safeWorkDir, StringComparison.Ordinal))
                throw new SecurityException($"Path traversal in archive entry: {entry.Key}");
            entry.WriteToDirectory(workDir, new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
        }
    }

    // Removes host ports and fixed container names from every docker-compose*.yml in composeDir.
    // Our override adds back only the API port; DB stays on the internal compose network.
    // Fixed container_name values are not project-scoped and commonly collide across submissions.
    private static void StripHostPortsAndContainerNames(string composeDir)
    {
        foreach (var file in Directory.EnumerateFiles(composeDir, "docker-compose*.yml"))
        {
            var lines = File.ReadAllLines(file);
            var stripped = new List<string>(lines.Length);
            var changed = false;

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var removeKey = IsYamlKey(line, "ports", out var keyIndent, allowInlineValue: true);
                if (!removeKey)
                    removeKey = IsYamlKey(line, "container_name", out keyIndent, allowInlineValue: true);

                if (!removeKey)
                {
                    stripped.Add(line);
                    continue;
                }

                changed = true;
                i++;
                while (i < lines.Length && IsYamlChildOrBlank(lines[i], keyIndent))
                    i++;
                i--;
            }

            if (changed)
                File.WriteAllLines(file, stripped);
        }
    }

    // Parses the student's docker-compose.yml to find which service exposes a known HTTP port.
    // Returns (serviceName, containerPort). Falls back to the best-looking service on port 8080.
    private (string ServiceName, int ContainerPort) DetectApiService(string composeFile)
    {
        // Known HTTP ports students typically expose
        var httpPorts = new[] { 8080, 5000, 80, 5001, 3000 };

        var lines = File.ReadAllLines(composeFile);

        string? currentService = null;
        var serviceNames = new List<string>();
        var buildServices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool inServices = false;
        bool inPortsBlock = false;
        int portsIndent = -1;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Detect "services:" top-level key
            if (line.TrimStart() == "services:" && !rawLine.StartsWith(' '))
            {
                inServices = true;
                inPortsBlock = false;
                currentService = null;
                continue;
            }

            if (!inServices) continue;

            // Detect service-level key (single indent level, e.g. "  api:")
            var svcMatch = ServiceIndentRegex().Match(rawLine);
            if (svcMatch.Success)
            {
                currentService = svcMatch.Groups[2].Value;
                serviceNames.Add(currentService);
                inPortsBlock = false;
                continue;
            }

            // Detect "    ports:" under current service
            if (currentService is not null && IsYamlKey(rawLine, "build", out _, allowInlineValue: true))
            {
                buildServices.Add(currentService);
                continue;
            }

            if (currentService is not null && IsYamlKey(rawLine, "ports", out var detectedPortsIndent, allowInlineValue: true))
            {
                inPortsBlock = true;
                portsIndent = detectedPortsIndent;

                var inlineContainerPort = TryParseInlinePorts(rawLine);
                if (inlineContainerPort.HasValue && httpPorts.Contains(inlineContainerPort.Value))
                {
                    logger.LogInformation(
                        "Detected API service '{Service}' on container port {Port} from docker-compose.yml",
                        currentService, inlineContainerPort.Value);
                    return (currentService, inlineContainerPort.Value);
                }
                continue;
            }

            // Once inside ports block, scan port entries
            if (inPortsBlock && currentService is not null)
            {
                if (!IsYamlChildOrBlank(rawLine, portsIndent))
                {
                    inPortsBlock = false;
                    portsIndent = -1;
                    continue;
                }

                var containerPort = TryParsePortEntry(rawLine);
                if (containerPort.HasValue && httpPorts.Contains(containerPort.Value))
                {
                    logger.LogInformation(
                        "Detected API service '{Service}' on container port {Port} from docker-compose.yml",
                        currentService, containerPort.Value);
                    return (currentService, containerPort.Value);
                }
            }
        }

        var fallbackService = PickFallbackService(serviceNames, buildServices);
        logger.LogWarning(
            "Could not detect API service/port from docker-compose.yml — falling back to service='{Service}', port=8080. " +
            "Ensure your service exposes one of: 80, 8080, 5000, 5001, 3000.",
            fallbackService);
        return (fallbackService, 8080);
    }

    // Writes our override: exposes the detected API service on a dynamic host port + enforces resource limits.
    private static void WriteOverride(string composeDir, int apiPort, string serviceName, int containerPort,
        string memoryLimit, double cpuLimit)
    {
        var cpuLimitText = cpuLimit.ToString("0.###", CultureInfo.InvariantCulture);
        var content =
            "services:\n" +
            $"  {serviceName}:\n" +
            "    ports:\n" +
            $"      - \"{apiPort}:{containerPort}\"\n" +
            $"    mem_limit: {memoryLimit}\n" +
            $"    cpus: '{cpuLimitText}'\n" +
            "    deploy:\n" +
            "      resources:\n" +
            "        limits:\n" +
            $"          memory: {memoryLimit}\n" +
            $"          cpus: '{cpuLimitText}'\n";

        try
        {
            File.WriteAllText(Path.Combine(composeDir, "docker-compose.override.yml"), content);
        }
        catch (Exception ex)
        {
            throw new DockerComposeException($"Failed to write override.yml: {ex.Message}", ex);
        }
    }

    private async Task RunDockerComposeAsync(string composeDir, Guid jobId, CancellationToken ct, params string[] args)
    {
        using var buildTimeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(opts.Value.LabDockerBuildTimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, buildTimeout.Token);
        var dockerArgs = new List<string> { "compose", "-p", $"lab-{jobId}" };
        dockerArgs.AddRange(args);

        DockerCommandResult result;
        try
        {
            result = await RunDockerAsync(composeDir, linked.Token, dockerArgs);
        }
        catch (OperationCanceledException) when (buildTimeout.IsCancellationRequested)
        {
            throw new DockerComposeTimeoutException(
                $"docker compose {string.Join(' ', args)} timed out after {opts.Value.LabDockerBuildTimeoutSeconds}s.");
        }

        if (result.ExitCode != 0)
            throw new DockerComposeException(
                $"docker compose {string.Join(' ', args)} failed (exit {result.ExitCode}): {result.Output}");
    }

    private async Task CleanupDockerResourcesAsync(Guid jobId, string workingDirectory, bool removeWorkDir)
    {
        var timeout = TimeSpan.FromSeconds(opts.Value.LabDockerDownTimeoutSeconds);
        var project = $"lab-{jobId}";

        await RunCleanupCommandAsync(
            jobId,
            workingDirectory,
            timeout,
            "compose", "-p", project, "down", "--remove-orphans", "--volumes", "--rmi", "local");

        await RemoveResourcesByLabelAsync(jobId, "container", "ps", ["-aq", "--filter", $"label=com.docker.compose.project={project}"], ["rm", "-f"]);
        await RemoveResourcesByLabelAsync(jobId, "volume", "volume", ["ls", "-q", "--filter", $"label=com.docker.compose.project={project}"], ["volume", "rm", "-f"]);
        await RemoveResourcesByLabelAsync(jobId, "network", "network", ["ls", "-q", "--filter", $"label=com.docker.compose.project={project}"], ["network", "rm"]);
        await RemoveResourcesByLabelAsync(jobId, "image", "image", ["ls", "-q", "--filter", $"label=com.docker.compose.project={project}"], ["image", "rm", "-f"]);

        if (!removeWorkDir) return;

        var workDir = Path.Combine(WorkRoot, jobId.ToString());
        try
        {
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete workdir for job {JobId}", jobId);
        }
    }

    private async Task RemoveResourcesByLabelAsync(
        Guid jobId,
        string resourceName,
        string listCommand,
        string[] listArgs,
        string[] removeArgs)
    {
        var listResult = await RunCleanupCommandAsync(jobId, "/tmp", TimeSpan.FromSeconds(15),
            [listCommand, .. listArgs]);
        if (listResult.ExitCode != 0) return;

        var ids = listResult.Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (ids.Length == 0) return;

        var args = removeArgs.Concat(ids).ToArray();
        var removeResult = await RunCleanupCommandAsync(jobId, "/tmp", TimeSpan.FromSeconds(30), args);
        if (removeResult.ExitCode == 0)
            logger.LogInformation("Removed {Count} Docker {ResourceName}(s) for job {JobId}",
                ids.Length, resourceName, jobId);
    }

    private async Task<DockerCommandResult> RunCleanupCommandAsync(
        Guid jobId,
        string workingDirectory,
        TimeSpan timeout,
        params string[] args)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            var result = await RunDockerAsync(workingDirectory, cts.Token, args);
            if (result.ExitCode != 0)
            {
                logger.LogWarning(
                    "Docker cleanup command failed for job {JobId} (exit {ExitCode}): docker {Args}. {Output}",
                    jobId, result.ExitCode, string.Join(' ', args), result.Output);
            }
            return result;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Docker cleanup command timed out for job {JobId}: docker {Args}",
                jobId, string.Join(' ', args));
            return new DockerCommandResult(-1, string.Empty);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Docker cleanup command failed for job {JobId}: docker {Args}",
                jobId, string.Join(' ', args));
            return new DockerCommandResult(-1, ex.Message);
        }
    }

    private static async Task<DockerCommandResult> RunDockerAsync(
        string workingDirectory,
        CancellationToken ct,
        IReadOnlyList<string> args)
    {
        var psi = new ProcessStartInfo("docker")
        {
            WorkingDirectory = Directory.Exists(workingDirectory) ? workingDirectory : "/tmp",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        var proc = Process.Start(psi) ?? throw new DockerComposeException("Failed to start docker process.");
        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);

        try
        {
            await proc.WaitForExitAsync(ct);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            return new DockerCommandResult(proc.ExitCode, CombineOutput(stdout, stderr));
        }
        catch
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* best effort */ }
            throw;
        }
    }

    private static string CombineOutput(string stdout, string stderr)
    {
        var output = string.Join('\n',
            new[] { stdout, stderr }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return output.Length <= MaxDockerOutputChars
            ? output
            : output[^MaxDockerOutputChars..];
    }

    private async Task WaitForApiAsync(int port, Guid jobId, CancellationToken ct)
    {
        using var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var baseUrl = GetApiBaseUrl(port);
        var deadline = DateTime.UtcNow.AddSeconds(opts.Value.LabDockerHealthCheckTimeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await probe.GetAsync(baseUrl, ct);
                logger.LogInformation("Student API on port {Port} is ready (job {JobId})", port, jobId);
                return;
            }
            catch (Exception ex) when (ex is HttpRequestException
                                    || (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                await Task.Delay(3000, ct);
            }
        }

        throw new DockerComposeTimeoutException(
            $"Student API on port {port} did not respond within {opts.Value.LabDockerHealthCheckTimeoutSeconds}s.");
    }

    private static int PickPort(int start, int end)
    {
        var rng = Random.Shared;
        for (int i = 0; i < 100; i++)
        {
            var port = rng.Next(start, end + 1);
            if (IsPortFree(port)) return port;
        }
        throw new InvalidOperationException($"No free port in range {start}–{end}.");
    }

    private static bool IsPortFree(int port)
    {
        try
        {
            var listener = new TcpListener(System.Net.IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch { return false; }
    }

    private static bool IsYamlKey(string line, string key, out int indent, bool allowInlineValue = false)
    {
        indent = CountLeadingWhitespace(line);
        var trimmed = RemoveInlineComment(line).Trim();
        return allowInlineValue
            ? trimmed.Equals($"{key}:", StringComparison.OrdinalIgnoreCase)
              || trimmed.StartsWith($"{key}:", StringComparison.OrdinalIgnoreCase)
            : trimmed.Equals($"{key}:", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsYamlChildOrBlank(string line, int parentIndent)
    {
        if (string.IsNullOrWhiteSpace(line)) return true;
        return CountLeadingWhitespace(line) > parentIndent;
    }

    private static int CountLeadingWhitespace(string line)
    {
        var count = 0;
        foreach (var ch in line)
        {
            if (ch is not (' ' or '\t')) break;
            count++;
        }
        return count;
    }

    private static int? TryParsePortEntry(string rawLine)
    {
        var targetMatch = LongPortTargetRegex().Match(rawLine);
        if (targetMatch.Success && int.TryParse(targetMatch.Groups[1].Value, out var targetPort))
            return targetPort;

        var trimmed = RemoveInlineComment(rawLine).Trim();
        if (!trimmed.StartsWith('-')) return null;

        var value = trimmed[1..].Trim().Trim('"', '\'');
        if (value.Length == 0) return null;

        var slashIndex = value.IndexOf('/');
        if (slashIndex >= 0)
            value = value[..slashIndex];

        var candidate = value.Split(':', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return int.TryParse(candidate, out var port) ? port : null;
    }

    private static int? TryParseInlinePorts(string rawLine)
    {
        var value = RemoveInlineComment(rawLine).Trim();
        var colon = value.IndexOf(':');
        if (colon < 0 || colon == value.Length - 1) return null;

        value = value[(colon + 1)..].Trim();
        if (!value.StartsWith('[') || !value.EndsWith(']')) return null;

        foreach (var entry in value.Trim('[', ']').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var port = TryParsePortEntry("- " + entry.Trim());
            if (port.HasValue) return port;
        }

        return null;
    }

    private static string RemoveInlineComment(string line)
    {
        var inSingleQuote = false;
        var inDoubleQuote = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '\'' && !inDoubleQuote)
                inSingleQuote = !inSingleQuote;
            else if (ch == '"' && !inSingleQuote)
                inDoubleQuote = !inDoubleQuote;
            else if (ch == '#' && !inSingleQuote && !inDoubleQuote)
                return line[..i];
        }

        return line;
    }

    private static string PickFallbackService(List<string> serviceNames, HashSet<string> buildServices)
    {
        if (serviceNames.Count == 0) return "api";

        return serviceNames.FirstOrDefault(s => s.Equals("api", StringComparison.OrdinalIgnoreCase))
               ?? serviceNames.FirstOrDefault(s => s.Contains("api", StringComparison.OrdinalIgnoreCase))
               ?? serviceNames.FirstOrDefault(s => s.Contains("web", StringComparison.OrdinalIgnoreCase))
               ?? serviceNames.FirstOrDefault(buildServices.Contains)
               ?? serviceNames[0];
    }

    private sealed record DockerCommandResult(int ExitCode, string Output);
}
