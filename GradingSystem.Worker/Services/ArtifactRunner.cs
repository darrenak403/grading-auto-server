using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
    // Process-local reservation registry closing the PickPort/IsPortFree TOCTOU race:
    // IsPortFree's TcpListener probe alone isn't enough once same-assignment submissions
    // can run concurrently, since the real bind happens later in StartDotnet.
    private readonly HashSet<int> _reservedPorts = [];
    private readonly object _portLock = new();

    /// <summary>
    /// Computes the sandbox path for a job without doing any I/O. Callers create the
    /// <see cref="StudentContext"/> via this method *before* calling <see cref="RunAsync"/>
    /// so that partial state (already-started processes, reserved ports) set on the context
    /// by <see cref="RunAsync"/> mid-run remains reachable for <see cref="CleanupAsync"/> even
    /// if <see cref="RunAsync"/> is cancelled (e.g. a per-submission timeout) before returning.
    /// </summary>
    public StudentContext CreateContext(GradingJob job)
    {
        var basePath = string.IsNullOrEmpty(config["Storage:BasePath"]) ? "/storage" : config["Storage:BasePath"]!;
        var sandboxPath = Path.Combine(basePath, "sandbox", job.Id.ToString());
        return new StudentContext { SandboxPath = sandboxPath };
    }

    public virtual async Task RunAsync(
        GradingJob job,
        IReadOnlyList<Question> questions,
        StudentContext ctx,
        CancellationToken ct)
    {
        var submission = job.Submission;
        var assignment = submission.Assignment;
        var sandboxPath = ctx.SandboxPath;

        Directory.CreateDirectory(sandboxPath);
        var studentRoot = Path.Combine(sandboxPath, "student");

        ZipFile.ExtractToDirectory(submission.ArtifactZipPath, studentRoot);
        logger.LogInformation("Extracted artifact for job {JobId} → {Path}", job.Id, studentRoot);

        bool hasApiQuestion = questions.Any(q => q.Type == QuestionType.Api);

        if (hasApiQuestion && assignment.DatabaseSqlPath != null)
        {
            var dbName = $"grading_{job.Id:N}";
            ctx.DatabaseName = dbName;
            await SetupDatabaseAsync(dbName, assignment.DatabaseSqlPath, ct);
            logger.LogInformation("SQL Server sandbox ready: {DbName}", dbName);
        }
        else if (hasApiQuestion)
        {
            logger.LogWarning("Assignment has API question but DatabaseSqlPath is null — student app will use its own connection string and may return 500");
        }

        // Start given API from zip (takes priority over static GivenApiBaseUrl for Q2)
        string? effectiveGivenApiBaseUrl = assignment.GivenApiBaseUrl;
        bool hasRazorQuestion = questions.Any(q => q.Type == QuestionType.Razor);
        if (hasRazorQuestion && assignment.GivenZipPath != null)
        {
            var givenRoot = Path.Combine(sandboxPath, "given");
            ZipFile.ExtractToDirectory(assignment.GivenZipPath, givenRoot);
            logger.LogInformation("Extracted given API zip for job {JobId} → {Path}", job.Id, givenRoot);

            StripPublishingListenConfigFromAppSettings(givenRoot);

            var givenTarget = FindExecutableTarget(givenRoot);
            RepairMissingRuntimeAssets(givenTarget);
            var givenPort = PickPort();
            var givenProcess = StartDotnet(givenTarget, givenPort);

            // Attach to ctx before awaiting the health check so CleanupAsync can still find and
            // kill this process / release this port if WaitForPortAsync throws (timeout, crash,
            // or shutdown cancellation) — otherwise a startup-time failure leaks both forever.
            ctx.GivenApiProcess = givenProcess;
            ctx.GivenApiPort = givenPort;
            ctx.GivenApiReservedPort = givenPort;

            ctx.GivenApiPort = await WaitForPortAsync(givenPort, givenProcess, ct);
            ForgetDetectedPort(givenProcess.Id);

            effectiveGivenApiBaseUrl = BindUrl(ctx.GivenApiPort);
            logger.LogInformation("Given API started on port {Port} for job {JobId}", ctx.GivenApiPort, job.Id);
        }

        foreach (var question in questions)
        {
            var questionDir = Path.Combine(studentRoot, question.ArtifactFolderName);
            if (!Directory.Exists(questionDir))
            {
                // Do NOT fall back to studentRoot: every artifact.zip nests each question strictly
                // under its own ArtifactFolderName, so a missing folder means this question truly
                // wasn't submitted (or ArtifactFolderName is misconfigured) — searching the shared
                // root here would risk picking up and grading a sibling question's app instead.
                var reason = $"Question folder '{question.ArtifactFolderName}' not found in submission — treated as not submitted.";
                logger.LogWarning("Student folder missing for question {QId}: {Reason}", question.Id, reason);
                ctx.QuestionApps[question.Id] = new QuestionApp
                {
                    Process = null!,
                    Port = 0,
                    NotSubmitted = true,
                    NotSubmittedReason = reason,
                };
                continue;
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

            // Some students hardcode a local SQL connection string in OnConfiguring without an
            // IsConfigured guard, which silently overrides whatever DI/env config we inject —
            // the app then tries to reach a server/database that only exists on the student's own
            // machine and 500s on every DB-touching request. Rather than fail those questions
            // outright, detect the hardcoded string and provision a matching "shadow" database so
            // the app's own (broken) configuration happens to reach real, seeded data.
            if (question.Type == QuestionType.Api && ctx.DatabaseName != null && assignment.DatabaseSqlPath != null)
            {
                await ProvisionHardcodedShadowDatabaseAsync(question, questionDir, assignment.DatabaseSqlPath, ctx, ct);
            }

            var target = FindExecutableTarget(questionDir);
            RepairMissingRuntimeAssets(target);
            var port = PickPort();
            var env = BuildEnv(question, ctx.DatabaseName, effectiveGivenApiBaseUrl, questionDir);

            var process = StartDotnet(target, port, env);

            // Attach to ctx before awaiting the health check — see the matching comment above
            // for the given-API process — so a startup-time failure still leaves the process
            // and port reachable for CleanupAsync instead of leaking them.
            var questionApp = new QuestionApp { Process = process, Port = port, ReservedPort = port };
            ctx.QuestionApps[question.Id] = questionApp;

            var actualPort = await WaitForPortAsync(port, process, ct);
            ForgetDetectedPort(process.Id);

            if (actualPort != port)
            {
                // Student's own hardcoded URL (Program.cs or launchSettings.json) won over the
                // --urls/ASPNETCORE_URLS we passed — grade against wherever it actually came up.
                logger.LogWarning(
                    "Question {QId}: app bound to port {ActualPort} instead of assigned {AssignedPort} — " +
                    "likely a hardcoded URL in the student's submission; grading against the actual port",
                    question.Id, actualPort, port);
                questionApp.Port = actualPort;
            }

            logger.LogInformation("Q{Type} app on port {Port} for question {QId}",
                question.Type, questionApp.Port, question.Id);
        }
    }

    public virtual async Task CleanupAsync(StudentContext ctx)
    {
        foreach (var (qId, app) in ctx.QuestionApps)
        {
            if (app.GivenUrlInvalid || app.NotSubmitted) continue;
            KillAndRelease(app.Process, app.ReservedPort, qId);
        }

        if (ctx.GivenApiProcess != null)
        {
            KillAndRelease(ctx.GivenApiProcess, ctx.GivenApiReservedPort, null);
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

        if (ctx.HardcodedShadowDb is { } shadowDb)
        {
            try { await DropDatabaseAsync(shadowDb.Name); }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to drop shadow database {Db}", shadowDb.Name); }
            finally { shadowDb.Lock.Release(); }
        }
    }

    private void KillAndRelease(Process process, int reservedPort, Guid? questionId)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
        }
        catch (Exception ex)
        {
            if (questionId is { } qId)
                logger.LogWarning(ex, "Failed to kill process for question {QId}", qId);
            else
                logger.LogWarning(ex, "Failed to kill given API process");
        }
        finally
        {
            ReleasePort(reservedPort);
            ForgetDetectedPort(process.Id);
            _stderrTail.TryRemove(process.Id, out _);
        }
    }

    private async Task SetupDatabaseAsync(string dbName, string sqlScriptPath, CancellationToken ct)
    {
        RequireValidDatabaseName(dbName);

        // Force out any stale/pooled connections left over from a previous job that used this
        // same database name (e.g. two students hardcoding the same local DB name) before
        // (re)creating it — a plain DROP here would fail with "database is currently in use"
        // the same way an un-forced drop does in DropDatabaseAsync.
        await DropDatabaseAsync(dbName);

        var masterConn = config.GetConnectionString("SqlServer")!;

        await using (var conn = new SqlConnection(masterConn))
        {
            await conn.OpenAsync(ct);
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
        RequireValidDatabaseName(dbName);
        var masterConn = config.GetConnectionString("SqlServer")!;
        await using var conn = new SqlConnection(masterConn);
        await conn.OpenAsync();

        await ExecuteNonQueryAsync(conn,
            $"IF DB_ID(N'{dbName}') IS NOT NULL " +
            $"BEGIN ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            $"DROP DATABASE [{dbName}] END");
    }

    // Every dbName here either comes from our own "grading_{jobId:N}" format or is a catalog
    // name pulled out of a student's own connection-string literal (ProvisionHardcodedShadowDatabaseAsync)
    // before being interpolated straight into T-SQL DDL — reject anything that isn't a plain SQL
    // identifier so a crafted "malicious'; DROP DATABASE x; --"-style literal can't reach the server.
    [GeneratedRegex(@"^[A-Za-z0-9_]{1,64}$")]
    private static partial Regex ValidDatabaseNameRegex();

    private static void RequireValidDatabaseName(string dbName)
    {
        if (!ValidDatabaseNameRegex().IsMatch(dbName))
            throw new InvalidOperationException($"Refusing to use unsafe database name: '{dbName}'");
    }

    private static async Task ExecuteNonQueryAsync(SqlConnection conn, string sql)
    {
        await using var cmd = new SqlCommand(sql, conn);
        cmd.CommandTimeout = 30;
        await cmd.ExecuteNonQueryAsync();
    }

    private SqlConnectionStringBuilder GetMasterConnectionStringBuilder() =>
        new(config.GetConnectionString("SqlServer")!);

    private Dictionary<string, string> BuildEnv(
        Question question, string? dbName, string? givenApiBaseUrl, string? questionDir = null)
    {
        var env = new Dictionary<string, string>();

        if (question.Type == QuestionType.Api && dbName != null)
        {
            var builder = GetMasterConnectionStringBuilder();
            builder.InitialCatalog = dbName;
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

    // Cross-job registry of locks, one per distinct "server|database" a hardcoded connection
    // string points at. Static/process-wide because different grading jobs (different students,
    // possibly different assignments) can independently hardcode the exact same well-known
    // server+database name (e.g. copied from the same in-class demo), and must not run
    // concurrently against the shared shadow database that name maps to.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _hardcodedDbLocks = new(StringComparer.OrdinalIgnoreCase);

    private static SemaphoreSlim GetHardcodedDbLock(string canonicalKey) =>
        _hardcodedDbLocks.GetOrAdd(canonicalKey, _ => new SemaphoreSlim(1, 1));

    private async Task ProvisionHardcodedShadowDatabaseAsync(
        Question question, string questionDir, string databaseSqlPath, StudentContext ctx, CancellationToken ct)
    {
        var hardcoded = DetectHardcodedSqlConnectionString(questionDir);
        if (hardcoded == null) return;

        var canonicalKey = $"{hardcoded.DataSource}|{hardcoded.InitialCatalog}".ToLowerInvariant();
        var sem = GetHardcodedDbLock(canonicalKey);
        await sem.WaitAsync(ct);
        try
        {
            await SetupDatabaseAsync(hardcoded.InitialCatalog, databaseSqlPath, ct);
            ctx.HardcodedShadowDb = (hardcoded.InitialCatalog, sem);
            logger.LogWarning(
                "Question {QId}: student code hardcodes a local SQL connection string (ignoring " +
                "injected config) — provisioned shadow database '{Db}' matching it so grading can proceed.",
                question.Id, hardcoded.InitialCatalog);
        }
        catch
        {
            sem.Release();
            throw;
        }
    }

    [GeneratedRegex(
        @"(?:Server|Data Source)\s*=\s*[^;""']+;\s*(?:(?:Database|Initial Catalog|UID|User Id|PWD|Password|TrustServerCertificate|Encrypt|MultipleActiveResultSets|Integrated Security)\s*=\s*[^;""']+;?\s*)+",
        RegexOptions.IgnoreCase)]
    private static partial Regex HardcodedConnStringRegex();

    /// <summary>
    /// Scans the student's Q1 artifact (source .cs and/or published .dll) for an embedded SQL
    /// connection string literal pointing at a "local" server — the telltale sign of a scaffolded
    /// OnConfiguring override that hardcodes its own DB instead of reading DI config. .NET string
    /// literals survive compilation as plain UTF-16 text in the assembly, so scanning .dll bytes
    /// decoded as Unicode finds them even when only a published build was submitted (no source).
    /// </summary>
    // Third-party framework/NuGet assemblies that ship alongside a published app can never
    // contain the student's own OnConfiguring literal — skipping them by filename prefix avoids
    // reading and regex-scanning dozens of unrelated multi-MB DLLs per question.
    private static readonly string[] FrameworkDllPrefixes =
        ["Microsoft.", "System.", "netstandard", "Newtonsoft.", "Swashbuckle.", "Azure."];

    private SqlConnectionStringBuilder? DetectHardcodedSqlConnectionString(string questionDir)
    {
        foreach (var path in Directory.GetFiles(questionDir, "*", SearchOption.AllDirectories))
        {
            bool isCs = path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
            bool isDll = !isCs && path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
            if (!isCs && !isDll) continue;

            var fileName = Path.GetFileName(path);
            if (isDll && FrameworkDllPrefixes.Any(p => fileName.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                continue;

            long length;
            try { length = new FileInfo(path).Length; } catch { continue; }
            if (length > 20_000_000) continue;

            byte[] bytes;
            try { bytes = File.ReadAllBytes(path); } catch { continue; }

            // .cs source is UTF-8 text; compiled string literals in a .dll survive as UTF-16LE
            // in the metadata blob — decoding the other encoding for a given file type is wasted
            // work that can essentially never match.
            var text = isCs ? Encoding.UTF8.GetString(bytes) : Encoding.Unicode.GetString(bytes);

            var match = HardcodedConnStringRegex().Match(text);
            if (!match.Success) continue;

            SqlConnectionStringBuilder builder;
            try { builder = new SqlConnectionStringBuilder(match.Value); }
            catch { continue; }

            if (!string.IsNullOrWhiteSpace(builder.InitialCatalog) && IsLocalServerReference(builder.DataSource))
                return builder;
        }
        return null;
    }

    /// <summary>
    /// True if <paramref name="server"/> refers to "wherever this grading worker process is
    /// running" — the common set of local aliases ("(local)", ".", "localhost", "127.0.0.1") or
    /// the exact host the worker's own configured SQL Server connection string uses. A connection
    /// string pointing anywhere else is genuinely unreachable from the sandbox and is left to fail
    /// as-is — there's no safe way to intercept traffic to an arbitrary remote server.
    /// </summary>
    private static readonly string[] LocalServerAliases = ["local", ".", "localhost", "127.0.0.1"];

    private bool IsLocalServerReference(string server)
    {
        var bare = server.Split(',', '\\')[0].Trim().Trim('(', ')');
        if (LocalServerAliases.Contains(bare, StringComparer.OrdinalIgnoreCase)) return true;

        var masterHost = GetMasterConnectionStringBuilder().DataSource.Split(',', '\\')[0].Trim();
        return bare.Equals(masterHost, StringComparison.OrdinalIgnoreCase);
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

    private record ExecutableTarget(bool IsProject, string Path);

    private static bool IsUnderPublishDir(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(seg => seg.Equals("publish", StringComparison.OrdinalIgnoreCase));

    private static ExecutableTarget FindExecutableTarget(string dir)
    {
        // 1. Look for published app (has runtimeconfig.json). A submission can contain more than one
        // build byproduct at once — e.g. a leftover bin/Debug build from the student's last local run
        // alongside an actual `dotnet publish` output — and Directory.GetFiles' enumeration order is
        // filesystem-dependent, not "most intentional first". Prefer whichever pair sits under a
        // `publish` directory (the one output `dotnet publish` produces, and nothing else does); among
        // ties or when no publish folder exists, prefer the most recently built one. Otherwise an old
        // Debug build can silently shadow the version the student actually meant to submit.
        var published = Directory.GetFiles(dir, "*.runtimeconfig.json", SearchOption.AllDirectories)
            .Select(rc => rc.Replace(".runtimeconfig.json", ".dll"))
            .Where(File.Exists)
            .OrderByDescending(IsUnderPublishDir)
            .ThenByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        if (published != null) return new ExecutableTarget(false, published);

        // 2. Look for raw source code (has .csproj)
        var csproj = Directory.GetFiles(dir, "*.csproj", SearchOption.AllDirectories).FirstOrDefault();
        if (csproj != null)
        {
            return new ExecutableTarget(true, csproj);
        }

        // 3. Fallback to any DLL
        var candidateDll = Directory.GetFiles(dir, "*.dll", SearchOption.AllDirectories)
            .FirstOrDefault(f => !f.Contains(".Views.") && !f.EndsWith(".runtimeconfig.dll"));

        if (candidateDll != null)
            return new ExecutableTarget(false, candidateDll);

        throw new InvalidOperationException($"No suitable .csproj or published DLL found in {dir}");
    }

    private static readonly string NuGetPackagesRoot =
        Environment.GetEnvironmentVariable("NUGET_PACKAGES")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

    private static string CurrentWinRid => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X64 => "win-x64",
        Architecture.X86 => "win-x86",
        Architecture.Arm64 => "win-arm64",
        Architecture.Arm => "win-arm",
        _ => "win-x64"
    };

    /// <summary>
    /// Some student publish outputs are missing the "runtimes/{rid}/{native|lib\{tfm}}/" assets
    /// that their own {App}.deps.json declares for RID-specific dependencies — e.g. a partial or
    /// mixed dotnet publish that flattens the generic managed asset into the app root but drops
    /// the win/unix-specific copies, or drops a native asset (like Microsoft.Data.SqlClient's
    /// SNI.dll) entirely. The .NET host does not fall back to a same-name flat copy once
    /// deps.json advertises a RID-specific path for the current platform: it commits to the
    /// declared path and throws FileNotFoundException (or, for a missing native SNI dependency,
    /// PlatformNotSupportedException) if nothing is there.
    /// Repair tries two sources, in order: (1) a version-matched flat copy already sitting in the
    /// app root, (2) the exact same package/version the student's own project restored, pulled
    /// from this machine's local NuGet cache (the same nupkg dotnet publish itself would have
    /// copied the asset out of). Both are publish-output completeness fixes — neither changes
    /// anything the student wrote.
    /// </summary>
    private void RepairMissingRuntimeAssets(ExecutableTarget target)
    {
        if (target.IsProject) return; // "dotnet run" from source has no deps.json to mis-resolve

        var depsPath = Path.ChangeExtension(target.Path, ".deps.json");
        if (!File.Exists(depsPath)) return;

        var depsText = File.ReadAllText(depsPath);
        // Most student publishes are plain framework-dependent with no RID-specific assets at
        // all — skip the full JSON parse and object walk below for the common case.
        if (!depsText.Contains("\"runtimeTargets\"", StringComparison.Ordinal)) return;

        JsonNode? root;
        try { root = JsonNode.Parse(depsText); }
        catch { return; }

        if (root?["targets"] is not JsonObject targets) return;
        var appDir = Path.GetDirectoryName(target.Path)!;
        var winRid = CurrentWinRid;

        // Managed assemblies are declared under the generic "win" RID; native libraries (e.g. the
        // SqlClient SNI dependency) are declared under this machine's specific win-{arch} RID —
        // both are RID buckets the .NET host resolves against on this platform.
        var winAssets = targets
            .Select(kv => kv.Value)
            .OfType<JsonObject>()
            .SelectMany(libs => libs) // library entries keyed "PackageId/Version"
            .Select(lib => (LibraryKey: lib.Key, RuntimeTargets: lib.Value?["runtimeTargets"] as JsonObject))
            .Where(l => l.RuntimeTargets != null)
            .SelectMany(l => l.RuntimeTargets!.Select(rt => (l.LibraryKey, RelPath: rt.Key, Meta: rt.Value as JsonObject)))
            .Where(a => a.Meta != null && a.Meta["rid"]?.GetValue<string>() is { } rid && (rid == "win" || rid == winRid))
            .Select(a => (a.LibraryKey, a.RelPath, Meta: a.Meta!));

        foreach (var (libraryKey, relPath, assetMeta) in winAssets) // relPath e.g. "runtimes/win-x64/native/Microsoft.Data.SqlClient.SNI.dll"
        {
            var expectedPath = Path.Combine(appDir, relPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(expectedPath)) continue; // deps.json already satisfied — nothing to do

            var source = ResolveFromNuGetCache(libraryKey, relPath);
            if (source == null)
            {
                // Last resort only: some packages ship a generic/portable build alongside the
                // RID-specific one under the same version number (e.g. Microsoft.Data.SqlClient's
                // netstandard2.0 build, which throws PlatformNotSupportedException by design). A
                // flat copy at the app root cannot be trusted to be the RID-specific asset just
                // because its reported version matches — only use it when nothing from NuGet
                // (exact or nearby version) is available to salvage instead.
                var flatCandidate = Path.Combine(appDir, Path.GetFileName(relPath));
                if (File.Exists(flatCandidate) && MatchesDeclaredVersion(flatCandidate, assetMeta))
                    source = flatCandidate;
            }
            if (source == null) continue; // no salvageable copy present anywhere

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(expectedPath)!);
                File.Copy(source, expectedPath, overwrite: false);
                logger.LogWarning(
                    "Repaired incomplete publish output: copied {Source} into missing {RelPath} " +
                    "(declared in {Deps} but absent from the submitted artifact).",
                    source, relPath, Path.GetFileName(depsPath));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to repair missing runtime asset {RelPath}", relPath);
            }
        }
    }

    private static bool MatchesDeclaredVersion(string candidatePath, JsonObject assetMeta)
    {
        if (assetMeta["assemblyVersion"]?.GetValue<string>() is { } asmVersion)
        {
            try { return System.Reflection.AssemblyName.GetAssemblyName(candidatePath).Version?.ToString() == asmVersion; }
            catch { return false; }
        }
        if (assetMeta["fileVersion"]?.GetValue<string>() is { } fileVersion)
        {
            try { return FileVersionInfo.GetVersionInfo(candidatePath).FileVersion == fileVersion; }
            catch { return false; }
        }
        return true; // no version metadata declared to check against — accept on name match alone
    }

    private static string? ResolveFromNuGetCache(string libraryKey, string relPath)
    {
        var slash = libraryKey.LastIndexOf('/');
        if (slash < 0) return null;

        var packageId = libraryKey[..slash].ToLowerInvariant();
        var declaredVersion = libraryKey[(slash + 1)..];
        var relFsPath = relPath.Replace('/', Path.DirectorySeparatorChar);

        var exactPath = Path.Combine(NuGetPackagesRoot, packageId, declaredVersion, relFsPath);
        if (File.Exists(exactPath)) return exactPath;

        // Exact version isn't restored on this machine (e.g. the grading host's cache lags the
        // student project's pinned version). A nearby cached version of the same package's
        // RID-specific asset is a much safer substitute than falling through to a flat copy at
        // the app root — same actual implementation, just a different patch release.
        var packageDir = Path.Combine(NuGetPackagesRoot, packageId);
        if (!Directory.Exists(packageDir)) return null;

        var declared = Version.TryParse(declaredVersion, out var dv) ? dv : null;
        return Directory.GetDirectories(packageDir)
            .Select(d => (Path: Path.Combine(d, relFsPath), Version: Version.TryParse(Path.GetFileName(d), out var v) ? v : null))
            .Where(x => x.Version != null && File.Exists(x.Path))
            .OrderBy(x => declared == null ? 0 : Math.Abs(VersionDistance(x.Version!, declared)))
            .Select(x => x.Path)
            .FirstOrDefault();
    }

    private static long VersionDistance(Version a, Version b) => VersionWeight(a) - VersionWeight(b);

    private static long VersionWeight(Version v) =>
        Math.Max(v.Major, 0) * 1_000_000_000L +
        Math.Max(v.Minor, 0) * 1_000_000L +
        Math.Max(v.Build, 0) * 1_000L +
        Math.Max(v.Revision, 0);

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

    // Some student submissions hardcode their own port/URL (Program.cs UseUrls/Run, or
    // launchSettings.json applicationUrl), which silently overrides the --urls/ASPNETCORE_URLS
    // we pass to `dotnet run` — the app then binds to a different port than the one we assigned
    // and reserved. Grading doesn't need to fight that: we parse Kestrel's own "Now listening on"
    // startup log to learn whichever port the app actually bound to, keyed by process id. Only the
    // port number is trusted — the logged host can be a wildcard (0.0.0.0, +, *, [::]) that isn't
    // a connectable destination, so we always reconnect via our own configured BindHost.
    private readonly ConcurrentDictionary<int, int> _detectedListenPorts = new();

    private void ForgetDetectedPort(int processId) => _detectedListenPorts.TryRemove(processId, out _);

    private string BindUrl(int port) => $"http://{opts.Value.BindHost}:{port}";

    // Last few stderr lines per process, kept only so a "never became ready" failure can quote
    // the student's actual runtime error (unhandled exception, missing config, etc.) in
    // job.ErrorMessage instead of just saying it timed out — graders otherwise have no way to
    // see what actually went wrong short of digging through worker log files.
    private const int StderrTailMaxLines = 20;
    private readonly ConcurrentDictionary<int, ConcurrentQueue<string>> _stderrTail = new();

    private string? GetStderrTail(int processId) =>
        _stderrTail.TryGetValue(processId, out var tail) && !tail.IsEmpty
            ? string.Join(Environment.NewLine, tail)
            : null;

    private Process StartDotnet(ExecutableTarget target, int port, Dictionary<string, string>? env = null)
    {
        var bindUrl = BindUrl(port);

        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(target.Path),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        var fileName = Path.GetFileName(target.Path);

        if (target.IsProject)
        {
            psi.ArgumentList.Add("run");
            psi.ArgumentList.Add("--project");
            psi.ArgumentList.Add(fileName);
            psi.ArgumentList.Add("--");
            psi.ArgumentList.Add($"--urls={bindUrl}");
        }
        else
        {
            psi.ArgumentList.Add(fileName);
            psi.ArgumentList.Add($"--urls={bindUrl}");
        }

        logger.LogInformation("StartDotnet -> WorkingDir: {Wd}, Args: {Args}", psi.WorkingDirectory, string.Join(" ", psi.ArgumentList));

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
            if (string.IsNullOrWhiteSpace(e.Data)) return;
            logger.LogWarning("[student-stderr] {Line}", e.Data);

            var tail = _stderrTail.GetOrAdd(process.Id, static _ => new ConcurrentQueue<string>());
            tail.Enqueue(e.Data);
            while (tail.Count > StderrTailMaxLines) tail.TryDequeue(out string _);
        };
        process.OutputDataReceived += (_, e) =>
        {
            // A self-signed dev cert on an https endpoint won't validate against HttpClient's
            // default handler, so only http binds are worth detecting — once one is recorded for
            // this process there's nothing left to look for in later lines.
            if (_detectedListenPorts.ContainsKey(process.Id)) return;

            var match = NowListeningOnRegex().Match(e.Data ?? "");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var listenPort))
                _detectedListenPorts.TryAdd(process.Id, listenPort);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return process;
    }

    // Only the trailing port is extracted, and only for a plain http endpoint — the host portion
    // of a hardcoded bind can be a wildcard (0.0.0.0, +, *, [::]) that HttpClient can't connect
    // to directly, and an https-only endpoint isn't gradeable against a self-signed dev cert.
    [GeneratedRegex(@"Now listening on:\s*http://.*:(\d+)/?\s*$")]
    private static partial Regex NowListeningOnRegex();

    internal int PickPort()
    {
        var rng = new Random();
        lock (_portLock)
        {
            for (int i = 0; i < 100; i++)
            {
                var port = rng.Next(opts.Value.ArtifactPortRangeStart, opts.Value.ArtifactPortRangeEnd + 1);
                if (_reservedPorts.Contains(port)) continue;
                if (!IsPortFree(port)) continue;

                // Claim atomically in the same critical section as the IsPortFree probe —
                // the OS-level check alone is a check-then-act race once two callers can
                // run concurrently (the real bind happens later, in StartDotnet).
                _reservedPorts.Add(port);
                return port;
            }
        }
        throw new InvalidOperationException("No free port in configured range.");
    }

    internal void ReleasePort(int port)
    {
        lock (_portLock)
        {
            _reservedPorts.Remove(port);
        }
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

    /// <summary>
    /// Waits for the app to accept HTTP requests and returns the port it actually answered on.
    /// Grading doesn't care which port that is — a student's own hardcoded URL config can make
    /// the app bind somewhere other than <paramref name="assignedPort"/>, so once Kestrel reports
    /// the real bound port via <see cref="_detectedListenPorts"/> we probe that instead.
    /// </summary>
    private async Task<int> WaitForPortAsync(int assignedPort, Process process, CancellationToken ct)
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
                var exitStderrTail = GetStderrTail(process.Id);
                throw new InvalidOperationException(
                    $"Student app exited with code {process.ExitCode} (0x{process.ExitCode:X8}) before becoming ready." +
                    (exitStderrTail is null ? " (no stderr output captured)" : $" [student-stderr]: {exitStderrTail}"));
            }

            // Probe by our own BindHost:port, never the raw address Kestrel logged — a hardcoded
            // wildcard bind (0.0.0.0, +, *, [::]) isn't a connectable destination on every
            // platform/runtime, and TestRunner reconstructs request URLs the same way later.
            var probePort = _detectedListenPorts.TryGetValue(process.Id, out var detectedPort) ? detectedPort : assignedPort;

            try
            {
                await probe.GetAsync(BindUrl(probePort), ct);
                return probePort;
            }
            catch (Exception ex) when (ex is HttpRequestException
                                    || (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                await Task.Delay(500, ct);
            }
        }

        var timeoutStderrTail = GetStderrTail(process.Id);
        throw new TimeoutException(
            $"App did not start within {opts.Value.ArtifactHealthCheckTimeoutSeconds}s: {BindUrl(assignedPort)}" +
            (timeoutStderrTail is null ? "" : $" [student-stderr]: {timeoutStderrTail}"));
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
