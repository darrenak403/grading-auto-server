using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Globalization;
using System.Net.Sockets;
using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;
using GradingSystem.Domain.Entities;
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
    private static readonly SemaphoreSlim _pruneLock = new(1, 1);
    private static readonly string WorkRoot = Path.Combine(Path.GetTempPath(), "lab-grading");
    private static readonly string DockerFallbackWorkingDirectory = Path.GetTempPath();
    private const int MaxDockerOutputChars = 12_000;
    private static int _completedLabJobs;
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
    public async Task<int> StartContainersAsync(
        string workDir,
        Guid jobId,
        IReadOnlyCollection<LabTestCase>? readinessTestCases,
        CancellationToken ct)
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
        await CleanupDockerResourcesAsync(jobId, composeDir, removeWorkDir: false, ct);

        StripHostPortsAndContainerNames(composeDir);
        WriteOverride(composeDir, apiPort, serviceName, containerPort,
            opts.Value.LabContainerMemoryLimit, opts.Value.LabContainerCpuLimit);

        await RunDockerComposeAsync(composeDir, jobId, ct, "up", "-d", "--build");

        logger.LogInformation("Docker compose up for job {JobId} on API port {Port}", jobId, apiPort);

        await WaitForApiAsync(apiPort, jobId, composeDir, serviceName, readinessTestCases, ct);

        return apiPort;
    }

    public async Task<int> StartAsync(string archivePath, Guid jobId, CancellationToken ct)
    {
        var workDir = Extract(archivePath, jobId);
        return await StartContainersAsync(workDir, jobId, null, ct);
    }

    public string GetApiBaseUrl(int port) => $"http://{opts.Value.LabApiHost}:{port}";

    public async Task StopAsync(Guid jobId, CancellationToken ct = default)
    {
        _composeDirs.TryRemove(jobId, out var composeDir);
        var downDir = !string.IsNullOrEmpty(composeDir) && Directory.Exists(composeDir)
            ? composeDir
            : DockerFallbackWorkingDirectory;

        await CleanupDockerResourcesAsync(jobId, downDir, removeWorkDir: true, ct);

        logger.LogInformation("Docker compose down complete for job {JobId}", jobId);
    }

    public async Task<string> GetLogsAsync(Guid jobId)
    {
        _composeDirs.TryGetValue(jobId, out var composeDir);
        var logsDir = !string.IsNullOrEmpty(composeDir) && Directory.Exists(composeDir)
            ? composeDir
            : DockerFallbackWorkingDirectory;

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

    // Removes host ports and fixed container names from compose files in composeDir.
    // Our override adds back only the API port; DB stays on the internal compose network.
    // Fixed container_name values are not project-scoped and commonly collide across submissions.
    private static void StripHostPortsAndContainerNames(string composeDir)
    {
        foreach (var file in EnumerateComposeFiles(composeDir))
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

    private static IEnumerable<string> EnumerateComposeFiles(string composeDir)
    {
        string[] patterns =
        [
            "docker-compose*.yml",
            "docker-compose*.yaml",
            "compose*.yml",
            "compose*.yaml",
        ];

        return patterns
            .SelectMany(pattern => Directory.EnumerateFiles(composeDir, pattern))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    // Parses the student's docker-compose.yml to find which service should be exposed to the grader.
    // For microservice labs we must prefer the API Gateway over downstream services; otherwise
    // login may succeed on one service while the rest of the gateway routes return 404.
    private (string ServiceName, int ContainerPort) DetectApiService(string composeFile)
    {
        var httpPorts = new[] { 8080, 5000, 80, 5001, 3000 };
        var lines = File.ReadAllLines(composeFile);

        string? currentService = null;
        var serviceNames = new List<string>();
        var buildServices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var servicePorts = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
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
                servicePorts.TryAdd(currentService, []);
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
                    servicePorts[currentService].Add(inlineContainerPort.Value);
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
                    servicePorts[currentService].Add(containerPort.Value);
                }
            }
        }

        var preferredService = PickPreferredApiService(serviceNames, buildServices, servicePorts);
        if (!string.IsNullOrWhiteSpace(preferredService)
            && servicePorts.TryGetValue(preferredService, out var preferredPorts)
            && preferredPorts.Count > 0)
        {
            var preferredPort = httpPorts.First(preferredPorts.Contains);
            logger.LogInformation(
                "Detected preferred API service '{Service}' on container port {Port} from docker-compose.yml",
                preferredService, preferredPort);
            return (preferredService, preferredPort);
        }

        var fallbackService = preferredService;
        logger.LogWarning(
            "Could not detect API service/port from docker-compose.yml — falling back to preferred service='{Service}', port=8080. " +
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
        var dockerArgs = new List<string> { "compose", "-p", $"lab-{jobId}" };
        dockerArgs.AddRange(args);
        var maxAttempts = Math.Max(1, opts.Value.LabDockerBuildRetryAttempts + 1);
        var retryDelay = TimeSpan.FromSeconds(Math.Max(1, opts.Value.LabDockerBuildRetryDelaySeconds));

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var buildTimeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(opts.Value.LabDockerBuildTimeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, buildTimeout.Token);

            DockerCommandResult result;
            try
            {
                result = await RunDockerAsync(composeDir, linked.Token, dockerArgs, opts.Value.LabDockerComposeParallelLimit);
            }
            catch (OperationCanceledException) when (buildTimeout.IsCancellationRequested)
            {
                throw new DockerComposeTimeoutException(
                    $"docker compose {string.Join(' ', args)} timed out after {opts.Value.LabDockerBuildTimeoutSeconds}s.");
            }

            if (result.ExitCode == 0)
                return;

            if (attempt < maxAttempts && IsTransientDockerBuildFailure(result.Output))
            {
                logger.LogWarning(
                    "Docker compose build failed with transient registry/network error for job {JobId}; retrying attempt {NextAttempt}/{MaxAttempts} after {DelaySeconds}s. Output: {Output}",
                    jobId,
                    attempt + 1,
                    maxAttempts,
                    retryDelay.TotalSeconds,
                    result.Output);
                await Task.Delay(retryDelay, ct);
                continue;
            }

            throw new DockerComposeException(
                $"docker compose {string.Join(' ', args)} failed (exit {result.ExitCode}): {result.Output}");
        }
    }

    private async Task CleanupDockerResourcesAsync(
        Guid jobId,
        string workingDirectory,
        bool removeWorkDir,
        CancellationToken ct = default)
    {
        var timeout = TimeSpan.FromSeconds(opts.Value.LabDockerDownTimeoutSeconds);
        var project = $"lab-{jobId}";

        string[] downArgs = opts.Value.LabDockerRemoveImagesOnCleanup
            ? ["compose", "-p", project, "down", "--remove-orphans", "--volumes", "--rmi", "local"]
            : ["compose", "-p", project, "down", "--remove-orphans", "--volumes"];

        await RunCleanupCommandAsync(jobId, workingDirectory, timeout, downArgs);

        await RemoveResourcesByLabelAsync(jobId, "container", "ps", ["-aq", "--filter", $"label=com.docker.compose.project={project}"], ["rm", "-f"]);
        await RemoveResourcesByLabelAsync(jobId, "volume", "volume", ["ls", "-q", "--filter", $"label=com.docker.compose.project={project}"], ["volume", "rm", "-f"]);
        await RemoveResourcesByLabelAsync(jobId, "network", "network", ["ls", "-q", "--filter", $"label=com.docker.compose.project={project}"], ["network", "rm"]);
        if (opts.Value.LabDockerRemoveImagesOnCleanup)
            await RemoveResourcesByLabelAsync(jobId, "image", "image", ["ls", "-q", "--filter", $"label=com.docker.compose.project={project}"], ["image", "rm", "-f"]);

        if (removeWorkDir)
            await PruneBuildCacheIfDueAsync(jobId, ct);

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
        var listResult = await RunCleanupCommandAsync(jobId, DockerFallbackWorkingDirectory, TimeSpan.FromSeconds(15),
            [listCommand, .. listArgs]);
        if (listResult.ExitCode != 0) return;

        var ids = listResult.Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (ids.Length == 0) return;

        var args = removeArgs.Concat(ids).ToArray();
        var removeResult = await RunCleanupCommandAsync(jobId, DockerFallbackWorkingDirectory, TimeSpan.FromSeconds(30), args);
        if (removeResult.ExitCode == 0)
            logger.LogInformation("Removed {Count} Docker {ResourceName}(s) for job {JobId}",
                ids.Length, resourceName, jobId);
    }

    private async Task PruneBuildCacheIfDueAsync(Guid jobId, CancellationToken ct)
    {
        var interval = opts.Value.LabDockerBuildCachePruneIntervalJobs;
        if (interval <= 0) return;

        var completed = Interlocked.Increment(ref _completedLabJobs);
        if (completed % interval != 0) return;

        await _pruneLock.WaitAsync(ct);
        try
        {
            var keepStorage = string.IsNullOrWhiteSpace(opts.Value.LabDockerBuildCacheKeepStorage)
                ? "3GB"
                : opts.Value.LabDockerBuildCacheKeepStorage;

            var timeout = TimeSpan.FromMinutes(2);
            var pruneResult = await RunCleanupCommandAsync(
                jobId,
                DockerFallbackWorkingDirectory,
                timeout,
                "builder", "prune", "-f", "--keep-storage", keepStorage);

            if (pruneResult.ExitCode == 0)
            {
                logger.LogInformation(
                    "Docker build cache pruned for job {JobId} with keep-storage={KeepStorage}: {Output}",
                    jobId,
                    keepStorage,
                    string.IsNullOrWhiteSpace(pruneResult.Output) ? "(no output)" : pruneResult.Output.Trim());
            }
        }
        finally
        {
            _pruneLock.Release();
        }
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
            var result = await RunDockerAsync(workingDirectory, cts.Token, args, opts.Value.LabDockerComposeParallelLimit);
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
        IReadOnlyList<string> args,
        int? composeParallelLimit = null)
    {
        var psi = new ProcessStartInfo("docker")
        {
            WorkingDirectory = Directory.Exists(workingDirectory)
                ? workingDirectory
                : DockerFallbackWorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (composeParallelLimit is > 0)
            psi.Environment["COMPOSE_PARALLEL_LIMIT"] = composeParallelLimit.Value.ToString(CultureInfo.InvariantCulture);

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

    private static bool IsTransientDockerBuildFailure(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return false;

        string[] transientMarkers =
        [
            "failed to do request",
            "failed to resolve source metadata",
            "failed to fetch",
            "unexpected eof",
            " eof",
            "connection reset",
            "connection refused",
            "i/o timeout",
            "tls handshake timeout",
            "net/http",
            "server misbehaving",
            "temporary failure",
            "context deadline exceeded",
            "429 too many requests",
            "503 service unavailable",
            "502 bad gateway",
            "504 gateway timeout",
        ];

        return transientMarkers.Any(marker => output.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private async Task WaitForApiAsync(
        int port,
        Guid jobId,
        string composeDir,
        string serviceName,
        IReadOnlyCollection<LabTestCase>? readinessTestCases,
        CancellationToken ct)
    {
        using var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var baseUrl = GetApiBaseUrl(port);
        var deadline = DateTime.UtcNow.AddSeconds(opts.Value.LabDockerHealthCheckTimeoutSeconds);
        var intervalSeconds = Math.Max(1, opts.Value.LabDockerHealthCheckIntervalSeconds);
        var requiredSuccesses = Math.Max(1, opts.Value.LabDockerHealthCheckRequiredConsecutiveSuccesses);
        var successfulProbes = 0;
        var readinessProbes = GetReadinessProbes(readinessTestCases);

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var serviceStatus = await GetComposeServiceStatusAsync(composeDir, jobId, serviceName, ct);
            if (serviceStatus.IsExited)
            {
                var logs = await GetLogsAsync(jobId);
                throw new DockerComposeException(
                    $"Student API service '{serviceName}' exited before becoming ready. " +
                    $"docker compose ps: {serviceStatus.Output}. Logs: {logs}");
            }

            try
            {
                var probeResult = await ProbeReadinessAsync(probe, baseUrl, readinessProbes, ct);
                successfulProbes++;
                if (successfulProbes >= requiredSuccesses)
                {
                    logger.LogInformation(
                        "Student API on port {Port} is ready after {Successes} successful probe(s), last probe {Method} {Path} returned {Status} (job {JobId})",
                        port, successfulProbes, probeResult.Method, probeResult.Path, probeResult.StatusCode, jobId);
                    return;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException
                                    || (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                successfulProbes = 0;
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), ct);
            }
        }

        throw new DockerComposeTimeoutException(
            $"Student API on port {port} did not respond within {opts.Value.LabDockerHealthCheckTimeoutSeconds}s.");
    }

    private IReadOnlyList<ReadinessProbe> GetReadinessProbes(IReadOnlyCollection<LabTestCase>? testCases)
    {
        var testcaseProbes = testCases?
            .Where(tc => !string.IsNullOrWhiteSpace(tc.UrlTemplate)
                         && !tc.HttpMethod.Equals("SOURCE", StringComparison.OrdinalIgnoreCase))
            .OrderBy(tc => tc.Order)
            .ThenBy(tc => tc.CreatedAt)
            .Select(tc => new ReadinessProbe(
                tc.HttpMethod.ToUpperInvariant(),
                StripQueryString(ResolveProbeTemplate(tc.UrlTemplate)),
                ResolveProbeTemplate(tc.InputJson)))
            .Where(probe => Uri.IsWellFormedUriString(probe.Path, UriKind.Relative))
            .DistinctBy(probe => (probe.Method, probe.Path))
            .ToList();

        if (testcaseProbes is { Count: > 0 })
            return testcaseProbes;

        return GetHealthCheckPaths()
            .Select(path => new ReadinessProbe("GET", path, null))
            .ToList();
    }

    private IReadOnlyList<string> GetHealthCheckPaths()
    {
        var paths = opts.Value.LabDockerHealthCheckPaths
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.StartsWith('/') ? p : "/" + p)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return paths.Count > 0 ? paths : ["/"];
    }

    private static async Task<HealthProbeResult> ProbeReadinessAsync(
        HttpClient probe,
        string baseUrl,
        IReadOnlyList<ReadinessProbe> probes,
        CancellationToken ct)
    {
        Exception? lastError = null;
        HealthProbeResult? lastSuccess = null;
        foreach (var readinessProbe in probes)
        {
            try
            {
                using var request = BuildReadinessRequest(baseUrl, readinessProbe);
                using var response = await probe.SendAsync(request, ct);
                var statusCode = (int)response.StatusCode;
                if (IsReadyProbeStatusCode(statusCode))
                {
                    lastSuccess = new HealthProbeResult(readinessProbe.Method, readinessProbe.Path, statusCode);
                    continue;
                }

                lastError = new HttpRequestException(
                    $"Readiness probe '{readinessProbe.Method} {readinessProbe.Path}' returned non-ready status {statusCode}.");
                break;
            }
            catch (Exception ex) when (ex is HttpRequestException
                                    || (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                lastError = ex;
                break;
            }
        }

        return lastSuccess ?? throw lastError ?? new HttpRequestException("No health check paths configured.");
    }

    private static bool IsReadyProbeStatusCode(int statusCode) =>
        statusCode is >= 200 and < 500;

    private static HttpRequestMessage BuildReadinessRequest(string baseUrl, ReadinessProbe probe)
    {
        var request = new HttpRequestMessage(new HttpMethod(probe.Method), baseUrl.TrimEnd('/') + probe.Path);
        if (probe.Method is "POST" or "PUT" or "PATCH")
            request.Content = new StringContent(
                string.IsNullOrWhiteSpace(probe.InputJson) ? "{}" : probe.InputJson,
                System.Text.Encoding.UTF8,
                "application/json");

        return request;
    }

    private static string ResolveProbeTemplate(string? template)
    {
        if (string.IsNullOrWhiteSpace(template)) return "/";
        return Regex.Replace(template, @"\{\{[^{}]+\}\}", "probe");
    }

    private static string StripQueryString(string path)
    {
        var queryIndex = path.IndexOf('?');
        return queryIndex >= 0 ? path[..queryIndex] : path;
    }

    private async Task<ServiceStatus> GetComposeServiceStatusAsync(
        string composeDir,
        Guid jobId,
        string serviceName,
        CancellationToken ct)
    {
        try
        {
            var result = await RunDockerAsync(composeDir, ct,
                ["compose", "-p", $"lab-{jobId}", "ps", serviceName, "--format", "json"]);
            if (result.ExitCode != 0)
                return new ServiceStatus(false, result.Output);

            var output = result.Output.Trim();
            if (string.IsNullOrWhiteSpace(output))
                return new ServiceStatus(false, "(no service status output)");

            return new ServiceStatus(IsComposeServiceExited(output), output);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not inspect Docker service status for job {JobId}", jobId);
            return new ServiceStatus(false, ex.Message);
        }
    }

    private static bool IsComposeServiceExited(string output)
    {
        try
        {
            using var doc = JsonDocument.Parse(output);
            foreach (var service in EnumerateServiceStatusElements(doc.RootElement))
            {
                var state = GetJsonString(service, "State");
                if (state.Equals("running", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (state.Equals("exited", StringComparison.OrdinalIgnoreCase)
                    || state.Equals("dead", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (TryGetJsonInt(service, "ExitCode", out var exitCode) && exitCode != 0)
                    return true;
            }

            return false;
        }
        catch (JsonException)
        {
            return output.Contains("\"State\":\"exited\"", StringComparison.OrdinalIgnoreCase)
                || output.Contains("\"State\":\"dead\"", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static IEnumerable<JsonElement> EnumerateServiceStatusElements(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                yield return item;
        }
        else
        {
            yield return root;
        }
    }

    private static string GetJsonString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static bool TryGetJsonInt(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value);
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

    private static string PickPreferredApiService(
        List<string> serviceNames,
        HashSet<string> buildServices,
        Dictionary<string, HashSet<int>> servicePorts)
    {
        if (serviceNames.Count == 0) return "api";

        return serviceNames.FirstOrDefault(IsGatewayServiceName)
               ?? serviceNames
                   .Where(servicePorts.ContainsKey)
                   .FirstOrDefault(s => servicePorts[s].Count > 0)
               ?? serviceNames.FirstOrDefault(s => s.Equals("api", StringComparison.OrdinalIgnoreCase))
               ?? serviceNames.FirstOrDefault(s => s.Contains("gateway", StringComparison.OrdinalIgnoreCase))
               ?? serviceNames.FirstOrDefault(s => s.Contains("api", StringComparison.OrdinalIgnoreCase))
               ?? serviceNames.FirstOrDefault(s => s.Contains("web", StringComparison.OrdinalIgnoreCase))
               ?? serviceNames.FirstOrDefault(buildServices.Contains)
               ?? serviceNames[0];
    }

    private static bool IsGatewayServiceName(string serviceName) =>
        serviceName.Equals("api-gateway", StringComparison.OrdinalIgnoreCase)
        || serviceName.Equals("gateway", StringComparison.OrdinalIgnoreCase)
        || serviceName.Contains("gateway", StringComparison.OrdinalIgnoreCase)
        || serviceName.Contains("ocelot", StringComparison.OrdinalIgnoreCase)
        || serviceName.Contains("yarp", StringComparison.OrdinalIgnoreCase);

    private sealed record DockerCommandResult(int ExitCode, string Output);
    private sealed record ServiceStatus(bool IsExited, string Output);
    private sealed record ReadinessProbe(string Method, string Path, string? InputJson);
    private sealed record HealthProbeResult(string Method, string Path, int StatusCode);
}
