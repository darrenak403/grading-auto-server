using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Sockets;
using System.Security;
using GradingSystem.Worker.Options;
using Microsoft.Extensions.Options;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace GradingSystem.Worker.Services.Lab;

public class DockerComposeException(string message, Exception? inner = null) : Exception(message, inner);
public class DockerComposeTimeoutException(string message) : Exception(message);

public class DockerComposeRunner(
    IOptions<WorkerOptions> opts,
    ILogger<DockerComposeRunner> logger)
{
    private static readonly string WorkRoot = Path.Combine(Path.GetTempPath(), "lab-grading");
    private readonly ConcurrentDictionary<Guid, string> _composeDirs = new();

    /// <summary>Extracts the submission archive to a temp workdir. Returns the workdir path.</summary>
    public string Extract(string archivePath, Guid jobId)
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

        var apiPort = PickPort(opts.Value.LabApiPortRangeStart, opts.Value.LabApiPortRangeEnd);

        StripHostPorts(composeDir);
        var (serviceName, containerPort) = DetectApiService(composePath);
        WriteOverride(composeDir, apiPort, serviceName, containerPort);
        _composeDirs[jobId] = composeDir;

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

    public async Task StopAsync(Guid jobId)
    {
        _composeDirs.TryRemove(jobId, out var composeDir);
        var downDir = !string.IsNullOrEmpty(composeDir) && Directory.Exists(composeDir) ? composeDir : "/tmp";

        var psi = new ProcessStartInfo("docker")
        {
            WorkingDirectory = downDir,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("compose");
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add($"lab-{jobId}");
        psi.ArgumentList.Add("down");
        psi.ArgumentList.Add("--remove-orphans");
        psi.ArgumentList.Add("--volumes");

        var proc = Process.Start(psi);
        if (proc is not null)
            await proc.WaitForExitAsync(CancellationToken.None);

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

        logger.LogInformation("Docker compose down complete for job {JobId}", jobId);
    }

    public async Task<string> GetLogsAsync(Guid jobId)
    {
        _composeDirs.TryGetValue(jobId, out var composeDir);
        var logsDir = !string.IsNullOrEmpty(composeDir) && Directory.Exists(composeDir) ? composeDir : "/tmp";

        var psi = new ProcessStartInfo("docker")
        {
            WorkingDirectory = logsDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("compose");
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add($"lab-{jobId}");
        psi.ArgumentList.Add("logs");
        psi.ArgumentList.Add("--no-color");

        var proc = Process.Start(psi);
        if (proc is null) return string.Empty;
        var output = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync(CancellationToken.None);
        return output;
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

    // Removes entire ports: blocks from every docker-compose*.yml in composeDir.
    // Our override adds back only the API port; DB stays on the internal compose network.
    // Strips the whole block (including the "ports:" key) to avoid orphaned keys that
    // Docker Compose rejects with "must be a array".
    private static void StripHostPorts(string composeDir)
    {
        // Matches: indented "ports:" line followed by zero-or-more "  - ..." child lines
        var portsBlock = new System.Text.RegularExpressions.Regex(
            @"^([ \t]+)ports:[ \t]*\r?\n(?:[ \t]+-[^\r\n]*\r?\n)*",
            System.Text.RegularExpressions.RegexOptions.Multiline);

        foreach (var file in Directory.EnumerateFiles(composeDir, "docker-compose*.yml"))
        {
            var content = File.ReadAllText(file);
            var stripped = portsBlock.Replace(content, string.Empty);
            if (!ReferenceEquals(stripped, content))
                File.WriteAllText(file, stripped);
        }
    }

    // Parses the student's docker-compose.yml to find which service exposes a known HTTP port.
    // Returns (serviceName, containerPort). Falls back to ("api", 8080) if nothing detected.
    private (string ServiceName, int ContainerPort) DetectApiService(string composeFile)
    {
        // Known HTTP ports students typically expose
        var httpPorts = new[] { 8080, 5000, 80, 5001, 3000 };

        var lines = File.ReadAllLines(composeFile);

        // Regex: top-level service name (no leading spaces, ends with colon, not a compose key)
        var serviceNameRx = new System.Text.RegularExpressions.Regex(@"^([a-zA-Z][a-zA-Z0-9_\-]*):\s*$");
        // Regex: port entry like "- 8080:8080", "- '5000:5000'", "- 5000" (bare container port), "- HOST:CONTAINER"
        var portEntryRx   = new System.Text.RegularExpressions.Regex(@"^\s*-\s*['""]?(?:\d+:)?(\d+)['""]?\s*$");

        // Top-level compose keys that are NOT service names
        var composeTopKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "version", "services", "networks", "volumes", "configs", "secrets", "x-" };

        string? currentService = null;
        bool inServices = false;
        bool inPortsBlock = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Detect "services:" top-level key
            if (line.TrimStart() == "services:" && !rawLine.StartsWith(" "))
            {
                inServices = true;
                inPortsBlock = false;
                currentService = null;
                continue;
            }

            if (!inServices) continue;

            // Detect service-level key (single indent level, e.g. "  api:")
            var svcMatch = System.Text.RegularExpressions.Regex.Match(rawLine, @"^( {2}|\t)([a-zA-Z][a-zA-Z0-9_\-]*):\s*$");
            if (svcMatch.Success)
            {
                currentService = svcMatch.Groups[2].Value;
                inPortsBlock = false;
                continue;
            }

            // Detect "    ports:" under current service
            if (currentService is not null && System.Text.RegularExpressions.Regex.IsMatch(rawLine, @"^ {4}ports:\s*$"))
            {
                inPortsBlock = true;
                continue;
            }

            // Once inside ports block, scan port entries
            if (inPortsBlock && currentService is not null)
            {
                var portMatch = portEntryRx.Match(rawLine);
                if (portMatch.Success && int.TryParse(portMatch.Groups[1].Value, out var containerPort))
                {
                    if (httpPorts.Contains(containerPort))
                    {
                        logger.LogInformation(
                            "Detected API service '{Service}' on container port {Port} from docker-compose.yml",
                            currentService, containerPort);
                        return (currentService, containerPort);
                    }
                }
                // Stop scanning ports block when indentation drops back
                else if (!rawLine.StartsWith("      ") && !rawLine.StartsWith("\t\t\t"))
                {
                    inPortsBlock = false;
                }
            }
        }

        logger.LogWarning(
            "Could not detect API service/port from docker-compose.yml — falling back to service='api', port=8080. " +
            "Ensure your service exposes one of: 80, 8080, 5000, 5001, 3000.");
        return ("api", 8080);
    }

    // Writes our override: exposes the detected API service on a dynamic host port.
    private static void WriteOverride(string composeDir, int apiPort, string serviceName, int containerPort)
    {
        var content =
            "services:\n" +
            $"  {serviceName}:\n" +
            "    ports:\n" +
            $"      - \"{apiPort}:{containerPort}\"\n";

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
        var psi = new ProcessStartInfo("docker")
        {
            WorkingDirectory = composeDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("compose");
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add($"lab-{jobId}");
        foreach (var a in args) psi.ArgumentList.Add(a);

        var proc = Process.Start(psi) ?? throw new DockerComposeException("Failed to start docker compose process.");
        var stderr = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0)
            throw new DockerComposeException($"docker compose {string.Join(' ', args)} failed (exit {proc.ExitCode}): {stderr}");
    }

    private async Task WaitForApiAsync(int port, Guid jobId, CancellationToken ct)
    {
        using var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var baseUrl = $"http://localhost:{port}";
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
}
