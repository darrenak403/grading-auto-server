using System.Diagnostics;
using System.IO.Compression;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using GradingSystem.Domain.Entities;
using GradingSystem.Worker.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace GradingSystem.Worker.Services;

public partial class ArtifactRunner(
    IOptions<WorkerOptions> opts,
    IConfiguration config,
    ILogger<ArtifactRunner> logger)
{
    public async Task<StudentContext> RunAsync(
        GradingJob job,
        IReadOnlyList<Question> questions,
        CancellationToken ct)
    {
        var submission = job.Submission;
        var assignment = submission.Assignment;
        var basePath = string.IsNullOrEmpty(config["Storage:BasePath"]) ? "/storage" : config["Storage:BasePath"]!;

        var sandboxPath = Path.Combine(basePath, "sandbox", job.Id.ToString());
        Directory.CreateDirectory(sandboxPath);
        var studentRoot = Path.Combine(sandboxPath, "student");

        ZipFile.ExtractToDirectory(submission.ArtifactZipPath, studentRoot);
        logger.LogInformation("Extracted artifact for job {JobId} → {Path}", job.Id, studentRoot);

        string? dbName = null;
        bool hasApiQuestion = questions.Any(q => q.Type == QuestionType.Api);

        if (hasApiQuestion && assignment.DatabaseSqlPath != null)
        {
            dbName = $"grading_{job.Id:N}";
            await SetupDatabaseAsync(dbName, assignment.DatabaseSqlPath, ct);
            logger.LogInformation("SQL Server sandbox ready: {DbName}", dbName);
        }
        else if (hasApiQuestion)
        {
            logger.LogWarning("Assignment has API question but DatabaseSqlPath is null — student app will use its own connection string and may return 500");
        }

        var ctx = new StudentContext
        {
            SandboxPath = sandboxPath,
            DatabaseName = dbName,
        };

        // Start given API from zip (takes priority over static GivenApiBaseUrl for Q2)
        string? effectiveGivenApiBaseUrl = assignment.GivenApiBaseUrl;
        bool hasRazorQuestion = questions.Any(q => q.Type == QuestionType.Razor);
        if (hasRazorQuestion && assignment.GivenZipPath != null)
        {
            var givenRoot = Path.Combine(sandboxPath, "given");
            ZipFile.ExtractToDirectory(assignment.GivenZipPath, givenRoot);
            logger.LogInformation("Extracted given API zip for job {JobId} → {Path}", job.Id, givenRoot);

            StripPublishingListenConfigFromAppSettings(givenRoot);

            var givenDll = FindEntryDll(givenRoot);
            var givenPort = PickPort();
            var givenProcess = StartDotnet(givenDll, givenPort);
            var bindHost = opts.Value.BindHost;
            await WaitForPortAsync($"http://{bindHost}:{givenPort}", givenProcess, ct);

            ctx.GivenApiProcess = givenProcess;
            ctx.GivenApiPort = givenPort;
            effectiveGivenApiBaseUrl = $"http://{bindHost}:{givenPort}";
            logger.LogInformation("Given API started on port {Port} for job {JobId}", givenPort, job.Id);
        }

        foreach (var question in questions)
        {
            var questionDir = Path.Combine(studentRoot, question.ArtifactFolderName);
            if (!Directory.Exists(questionDir))
            {
                logger.LogWarning("Folder '{Folder}' not found in artifact — searching root",
                    question.ArtifactFolderName);
                questionDir = studentRoot;
            }

            // Q2: validate student used the correct GivenApiBaseUrl in their appsettings
            // Skip check when using given.zip (URL is dynamic, assigned at runtime)
            if (question.Type == QuestionType.Razor && effectiveGivenApiBaseUrl != null && ctx.GivenApiProcess == null)
            {
                var urlMismatch = CheckGivenApiBaseUrl(questionDir, effectiveGivenApiBaseUrl);
                if (urlMismatch != null)
                {
                    logger.LogWarning("Q2 GivenApiBaseUrl mismatch for question {QId}: {Reason}", question.Id, urlMismatch);
                    ctx.QuestionApps[question.Id] = new QuestionApp
                    {
                        Process = null!,
                        Port = 0,
                        GivenUrlInvalid = true,
                        GivenUrlInvalidReason = urlMismatch,
                    };
                    continue;
                }
            }

            var dll = FindEntryDll(questionDir);
            var port = PickPort();
            var env = BuildEnv(question, dbName, effectiveGivenApiBaseUrl, questionDir);

            var process = StartDotnet(dll, port, env);
            await WaitForPortAsync($"http://{opts.Value.BindHost}:{port}", process, ct);

            ctx.QuestionApps[question.Id] = new QuestionApp { Process = process, Port = port };

            logger.LogInformation("Q{Type} app on port {Port} for question {QId}",
                question.Type, port, question.Id);
        }

        return ctx;
    }

    public async Task CleanupAsync(StudentContext ctx)
    {
        foreach (var (qId, app) in ctx.QuestionApps)
        {
            if (app.GivenUrlInvalid) continue;
            try
            {
                if (!app.Process.HasExited)
                    app.Process.Kill(entireProcessTree: true);
            }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to kill process for question {QId}", qId); }
        }

        if (ctx.GivenApiProcess != null)
        {
            try
            {
                if (!ctx.GivenApiProcess.HasExited)
                    ctx.GivenApiProcess.Kill(entireProcessTree: true);
            }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to kill given API process"); }
        }

        try
        {
            if (Directory.Exists(ctx.SandboxPath))
                Directory.Delete(ctx.SandboxPath, recursive: true);
        }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to delete sandbox {Path}", ctx.SandboxPath); }

        if (ctx.DatabaseName != null)
        {
            try { await DropDatabaseAsync(ctx.DatabaseName); }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to drop database {Db}", ctx.DatabaseName); }
        }
    }

    private async Task SetupDatabaseAsync(string dbName, string sqlScriptPath, CancellationToken ct)
    {
        var masterConn = config.GetConnectionString("SqlServer")!;

        await using (var conn = new SqlConnection(masterConn))
        {
            await conn.OpenAsync(ct);
            await ExecuteNonQueryAsync(conn, $"IF DB_ID(N'{dbName}') IS NOT NULL DROP DATABASE [{dbName}]");
            await ExecuteNonQueryAsync(conn, $"CREATE DATABASE [{dbName}]");
        }

        var builder = new SqlConnectionStringBuilder(masterConn) { InitialCatalog = dbName };
        await using var dbConn = new SqlConnection(builder.ConnectionString);
        await dbConn.OpenAsync(ct);

        var script = await File.ReadAllTextAsync(sqlScriptPath, ct);
        foreach (var batch in GoBatchRegex().Split(script))
        {
            var trimmed = batch.Trim();
            if (trimmed.Length == 0) continue;
            if (IsSetupOnlyBatch(trimmed)) continue;

            await ExecuteNonQueryAsync(dbConn, trimmed);
        }
    }

    private async Task DropDatabaseAsync(string dbName)
    {
        var masterConn = config.GetConnectionString("SqlServer")!;
        await using var conn = new SqlConnection(masterConn);
        await conn.OpenAsync();

        await ExecuteNonQueryAsync(conn,
            $"IF DB_ID(N'{dbName}') IS NOT NULL " +
            $"BEGIN ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            $"DROP DATABASE [{dbName}] END");
    }

    private static async Task ExecuteNonQueryAsync(SqlConnection conn, string sql)
    {
        await using var cmd = new SqlCommand(sql, conn);
        cmd.CommandTimeout = 30;
        await cmd.ExecuteNonQueryAsync();
    }

    private Dictionary<string, string> BuildEnv(
        Question question, string? dbName, string? givenApiBaseUrl, string? questionDir = null)
    {
        var env = new Dictionary<string, string>();

        if (question.Type == QuestionType.Api && dbName != null)
        {
            var masterConn = config.GetConnectionString("SqlServer")!;
            var builder = new SqlConnectionStringBuilder(masterConn) { InitialCatalog = dbName };
            var connStr = builder.ConnectionString;

            // Always set DefaultConnection as the baseline
            env["ConnectionStrings__DefaultConnection"] = connStr;

            // Also override every connection string key found in the student's appsettings
            // so that students using non-standard names (SchoolDB, AppDb, etc.) also work
            if (questionDir != null)
            {
                foreach (var key in FindStudentConnectionStringKeys(questionDir))
                {
                    var envKey = $"ConnectionStrings__{key}";
                    if (!env.ContainsKey(envKey))
                    {
                        env[envKey] = connStr;
                        logger.LogInformation("Injecting sandbox DB into connection string '{Key}'", key);
                    }
                }
            }
        }

        if (question.Type == QuestionType.Razor && givenApiBaseUrl != null)
            env["GivenAPIBaseUrl"] = givenApiBaseUrl;

        return env;
    }

    private static List<string> FindStudentConnectionStringKeys(string dir)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var path in Directory.GetFiles(dir, "appsettings*.json", SearchOption.AllDirectories))
        {
            if (path.Contains("Development", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                var root = JsonNode.Parse(File.ReadAllText(path))?.AsObject();
                if (root?["ConnectionStrings"] is not JsonObject csObj) continue;
                foreach (var kv in csObj)
                    if (seen.Add(kv.Key)) result.Add(kv.Key);
            }
            catch { /* unreadable — skip */ }
        }
        return result;
    }

    private static string FindEntryDll(string dir)
    {
        var runtimeConfig = Directory.GetFiles(dir, "*.runtimeconfig.json", SearchOption.AllDirectories)
                                     .FirstOrDefault();
        if (runtimeConfig != null)
        {
            var dll = runtimeConfig.Replace(".runtimeconfig.json", ".dll");
            if (File.Exists(dll)) return dll;
        }

        var candidateDll = Directory.GetFiles(dir, "*.dll", SearchOption.AllDirectories)
            .FirstOrDefault(f => !f.Contains(".Views.") && !f.EndsWith(".runtimeconfig.dll"));

        if (candidateDll == null)
            throw new InvalidOperationException($"No suitable DLL found in {dir}");

        return candidateDll;
    }

    private static void StripPublishingListenConfigFromAppSettings(string rootDir)
    {
        foreach (var path in Directory.GetFiles(rootDir, "appsettings*.json", SearchOption.AllDirectories))
        {
            try
            {
                var text = File.ReadAllText(path);
                if (JsonNode.Parse(text) is not JsonObject root)
                    continue;

                root.Remove("Urls");
                root.Remove("urls");
                root.Remove("Kestrel");
                root.Remove("kestrel");

                File.WriteAllText(
                    path,
                    root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                /* unreadable or non-object JSON — leave file as-is */
            }
        }
    }

    private Process StartDotnet(string dll, int port, Dictionary<string, string>? env = null)
    {
        // --urls wins over appsettings / hardcoded UseUrls (e.g. http://127.0.0.1:5100 in publish)
        var bindUrl = $"http://{opts.Value.BindHost}:{port}";
        var psi = new ProcessStartInfo("dotnet", $"\"{dll}\" --urls={bindUrl}")
        {
            WorkingDirectory = Path.GetDirectoryName(dll),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["ASPNETCORE_URLS"] = bindUrl;
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development"; // ensures Swagger is enabled in student apps
        if (env != null)
            foreach (var (k, v) in env) psi.Environment[k] = v;

        var process = Process.Start(psi)!;

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                logger.LogDebug("[student-stdout] {Line}", e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                logger.LogWarning("[student-stderr] {Line}", e.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return process;
    }

    private int PickPort()
    {
        var rng = new Random();
        for (int i = 0; i < 100; i++)
        {
            var port = rng.Next(opts.Value.ArtifactPortRangeStart, opts.Value.ArtifactPortRangeEnd + 1);
            if (IsPortFree(port)) return port;
        }
        throw new InvalidOperationException("No free port in configured range.");
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

    private async Task WaitForPortAsync(string baseUrl, Process process, CancellationToken ct)
    {
        using var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var deadline = DateTime.UtcNow.AddSeconds(opts.Value.ArtifactHealthCheckTimeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            if (process.HasExited)
            {
                // Wait briefly for async stderr/stdout readers to flush buffered output
                await Task.Delay(300, CancellationToken.None);
                throw new InvalidOperationException(
                    $"Student app exited with code {process.ExitCode} (0x{process.ExitCode:X8}) before becoming ready — see [student-stderr] lines above.");
            }

            try
            {
                await probe.GetAsync(baseUrl, ct);
                return;
            }
            catch (Exception ex) when (ex is HttpRequestException
                                    || (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                await Task.Delay(500, ct);
            }
        }

        throw new TimeoutException(
            $"App did not start within {opts.Value.ArtifactHealthCheckTimeoutSeconds}s: {baseUrl}");
    }

    private static bool IsSetupOnlyBatch(string batch)
    {
        var stripped = LeadingBlockCommentsRegex().Replace(batch, string.Empty);
        stripped = LeadingLineCommentsRegex().Replace(stripped, string.Empty).TrimStart();

        return stripped.StartsWith("CREATE DATABASE", StringComparison.OrdinalIgnoreCase)
            || stripped.StartsWith("USE ", StringComparison.OrdinalIgnoreCase)
            || stripped.StartsWith("USE[", StringComparison.OrdinalIgnoreCase);
    }

    // Returns null if OK, or an error string if the student's appsettings does not contain the givenApiBaseUrl.
    private static string? CheckGivenApiBaseUrl(string questionDir, string givenApiBaseUrl)
    {
        var appsettingsFiles = Directory.GetFiles(questionDir, "appsettings*.json", SearchOption.AllDirectories)
            .Where(f => !f.Contains("appsettings.Development", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (appsettingsFiles.Count == 0)
            return "appsettings.json not found in student artifact";

        foreach (var path in appsettingsFiles)
        {
            try
            {
                var content = File.ReadAllText(path);
                if (content.Contains(givenApiBaseUrl, StringComparison.OrdinalIgnoreCase))
                    return null; // found — OK
            }
            catch { /* skip unreadable file */ }
        }

        return $"Student appsettings does not contain the required GivenApiBaseUrl '{givenApiBaseUrl}'";
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^(\s*/\*.*?\*/\s*)+", System.Text.RegularExpressions.RegexOptions.Singleline)]
    private static partial System.Text.RegularExpressions.Regex LeadingBlockCommentsRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"^(\s*--[^\r\n]*[\r\n]+)*")]
    private static partial System.Text.RegularExpressions.Regex LeadingLineCommentsRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"^\s*GO\s*$", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex GoBatchRegex();
}
