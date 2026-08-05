using System.Text.RegularExpressions;
using GradingSystem.Domain.Entities;

namespace GradingSystem.Worker.Services.Lab;

/// <summary>
/// Runs SOURCE-type test cases against extracted student source code.
/// Rules are encoded in UrlTemplate as "rule-type:args".
///
/// Supported rules:
///   project-name:PRN232.*.API          — at least one .csproj matches glob
///   project-count:3                    — exactly N .csproj files exist
///   project-count-at-least:4           — at least N .csproj files exist
///   folder-exists:**/Controllers        — at least one folder matches glob
///   file-exists:**/docker-compose.yml  — at least one file matches glob
///   file-count-at-least:**/*.proto:1   — at least N files match glob
///   file-contains:**/Services/*.cs:IRepository     — at least one file contains text
///   file-contains-any:**/*.cs:AddReverseProxy|AddOcelot — at least one file contains any option
///   integration-signal:redis        — bonus integration signal from package references or code usage
///   file-not-contains:**/Controllers/*.cs:DbContext — no file contains text
///   compose-service-exists:api-gateway — docker-compose.yml defines this service
///   compose-service-count-at-least:7   — docker-compose.yml defines at least N services
/// </summary>
public class SourceAnalyzer
{
    private const string IntegrationSignalFileGlob = "**/*.csproj;**/*.props;**/packages.config;**/*.cs";
    private static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".vs",
        "bin",
        "obj",
        "node_modules",
        "packages",
        "TestResults",
    };

    private static readonly IReadOnlyDictionary<string, string[]> IntegrationSignals =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["rabbitmq"] = new[]
            {
                "RabbitMQ.Client",
                "MassTransit",
                "MassTransit.RabbitMQ",
                "EasyNetQ",
                "AddMassTransit",
                "UsingRabbitMq",
                "UseRabbitMq",
                "IRabbitMqBusFactoryConfigurator",
            },
            ["redis"] = new[]
            {
                "StackExchange.Redis",
                "Microsoft.Extensions.Caching.StackExchangeRedis",
                "NRedisStack",
                "CSRedisCore",
                "FreeRedis",
                "AddStackExchangeRedisCache",
                "IConnectionMultiplexer",
            },
            ["opentelemetry"] = new[]
            {
                "OpenTelemetry",
                "AddOpenTelemetry",
                "WithTracing",
                "UseOtlpExporter",
                "AddAspNetCoreInstrumentation",
                "AddHttpClientInstrumentation",
            },
            ["resilience"] = new[]
            {
                "Polly",
                "Microsoft.Extensions.Http.Polly",
                "Microsoft.Extensions.Http.Resilience",
                "Microsoft.Extensions.Resilience",
                "AddPolicyHandler",
                "AddTransientHttpErrorPolicy",
                "WaitAndRetry",
                "CircuitBreaker",
                "AddStandardResilienceHandler",
                "AddResilienceHandler",
            },
        };

    public SourceScanContext Scan(string workDir) => new(workDir);

    public LabTestCaseResult Check(LabTestCase tc, string workDir, Guid jobId)
    {
        var source = Scan(workDir);
        return Check(tc, source, jobId);
    }

    public LabTestCaseResult Check(LabTestCase tc, SourceScanContext source, Guid jobId)
    {
        var rule = tc.UrlTemplate?.Trim() ?? string.Empty;
        var colon = rule.IndexOf(':');
        if (colon < 0)
            return Fail(tc, jobId, $"SOURCE rule must be 'type:args'. Got: '{rule}'");

        var ruleType = rule[..colon].Trim().ToLowerInvariant();
        var ruleArgs = rule[(colon + 1)..].Trim();

        try
        {
            var (passed, detail) = ruleType switch
            {
                "project-name"       => CheckProjectName(source, ruleArgs),
                "project-count"      => CheckProjectCount(source, ruleArgs),
                "project-count-at-least" => CheckProjectCountAtLeast(source, ruleArgs),
                "folder-exists"      => CheckFolderExists(source, ruleArgs),
                "file-exists"        => CheckFileExists(source, ruleArgs),
                "file-count-at-least" => CheckFileCountAtLeast(source, ruleArgs),
                "file-contains"      => CheckFilePattern(source, ruleArgs, mustContain: true),
                "file-contains-any"  => CheckFileContainsAny(source, ruleArgs),
                "integration-signal" => CheckIntegrationSignal(source, ruleArgs),
                "file-not-contains"  => CheckFilePattern(source, ruleArgs, mustContain: false),
                "compose-service-exists" => CheckComposeServiceExists(source, ruleArgs),
                "compose-service-count-at-least" => CheckComposeServiceCountAtLeast(source, ruleArgs),
                _                    => (false, $"Unknown SOURCE rule type: '{ruleType}'")
            };

            return new LabTestCaseResult
            {
                LabGradingJobId  = jobId,
                LabTestCaseId    = tc.Id,
                Passed           = passed,
                AwardedScore     = passed ? tc.Score : 0,
                ActualStatusCode = passed ? 200 : 0,
                ActualResponse   = detail.Length > 1000 ? detail[..1000] : detail,
                ErrorMessage     = passed ? null : detail,
            };
        }
        catch (Exception ex)
        {
            return Fail(tc, jobId, $"SOURCE check error: {ex.Message}");
        }
    }

    // project-name:PRN232.*.API  →  at least one .csproj whose name matches the glob
    private static (bool, string) CheckProjectName(SourceScanContext source, string pattern)
    {
        if (!pattern.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            pattern += ".csproj";
        var found = source.GlobFilePaths($"**/{pattern}")
            .Select(Path.GetFileName).ToList();
        return found.Count > 0
            ? (true,  $"Found: {string.Join(", ", found)}")
            : (false, $"No .csproj matching '{pattern}' found in archive.");
    }

    // project-count:3  →  archive contains exactly N .csproj files (ignores macOS ._* metadata files)
    private static (bool, string) CheckProjectCount(SourceScanContext source, string args)
    {
        if (!int.TryParse(args.Trim(), out var expected))
            return (false, $"Invalid count '{args}' — must be an integer.");
        var files = source.ProjectFileNames;
        return files.Count == expected
            ? (true,  $"Found {files.Count} project(s): {string.Join(", ", files)}")
            : (false, $"Expected {expected} project(s), found {files.Count}: {string.Join(", ", files)}");
    }

    private static (bool, string) CheckProjectCountAtLeast(SourceScanContext source, string args)
    {
        if (!int.TryParse(args.Trim(), out var minimum))
            return (false, $"Invalid count '{args}' — must be an integer.");

        var files = source.ProjectFileNames;
        return files.Count >= minimum
            ? (true, $"Found {files.Count} project(s): {string.Join(", ", files)}")
            : (false, $"Expected at least {minimum} project(s), found {files.Count}: {string.Join(", ", files)}");
    }

    // folder-exists:**/Controllers  →  at least one matching directory exists
    private static (bool, string) CheckFolderExists(SourceScanContext source, string pattern)
    {
        var found = source.GlobDirectoryPaths(pattern)
            .Select(source.GetRelativePath).ToList();
        return found.Count > 0
            ? (true,  $"Found: {string.Join(", ", found)}")
            : (false, $"No folder matching '{pattern}' found.");
    }

    // file-exists:**/docker-compose.yml  →  at least one matching file exists
    private static (bool, string) CheckFileExists(SourceScanContext source, string pattern)
    {
        var found = source.GlobFilePaths(pattern)
            .Select(source.GetRelativePath).ToList();
        return found.Count > 0
            ? (true,  $"Found: {string.Join(", ", found)}")
            : (false, $"No file matching '{pattern}' found.");
    }

    private static (bool, string) CheckFileCountAtLeast(SourceScanContext source, string args)
    {
        var sep = args.LastIndexOf(':');
        if (sep < 0)
            return (false, "file-count-at-least requires 'glob:count' format.");

        var fileGlob = args[..sep];
        var countText = args[(sep + 1)..];
        if (!int.TryParse(countText.Trim(), out var minimum))
            return (false, $"Invalid count '{countText}' — must be an integer.");

        var found = source.GlobFilePaths(fileGlob)
            .Select(source.GetRelativePath)
            .ToList();

        return found.Count >= minimum
            ? (true, $"Found {found.Count} file(s): {string.Join(", ", found)}")
            : (false, $"Expected at least {minimum} file(s) matching '{fileGlob}', found {found.Count}: {string.Join(", ", found)}");
    }

    // file-contains:**/Services/*.cs:IRepository
    //   mustContain=true  → passes if AT LEAST ONE matching file contains the text
    //   mustContain=false → passes if NO matching file contains the text
    private static (bool, string) CheckFilePattern(SourceScanContext source, string args, bool mustContain)
    {
        var sep = args.IndexOf(':');
        if (sep < 0)
            return (false, $"{(mustContain ? "file-contains" : "file-not-contains")} requires 'glob:text' format.");

        var fileGlob  = args[..sep];
        var searchText = args[(sep + 1)..];

        var files = source.GlobFilePaths(fileGlob, includeCentralPackageFiles: true);
        if (files.Count == 0)
            return (false, $"No files matching '{fileGlob}' found in archive.");

        var matches = files
            .Where(f => source.ReadAllText(f).Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .Select(source.GetRelativePath)
            .ToList();

        return mustContain
            ? matches.Count > 0
                ? (true,  $"{matches.Count}/{files.Count} file(s) contain '{searchText}'.")
                : (false, $"None of the {files.Count} file(s) matching '{fileGlob}' contain '{searchText}'.")
            : matches.Count == 0
                ? (true,  $"None of the {files.Count} file(s) contain '{searchText}'.")
                : (false, $"{matches.Count} file(s) contain '{searchText}': {string.Join(", ", matches)}");
    }

    private static (bool, string) CheckFileContainsAny(SourceScanContext source, string args)
    {
        var sep = args.IndexOf(':');
        if (sep < 0)
            return (false, "file-contains-any requires 'glob:text1|text2' format.");

        var fileGlob = args[..sep];
        var options = args[(sep + 1)..]
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (options.Length == 0)
            return (false, "file-contains-any requires at least one search option.");

        var files = source.GlobFilePaths(fileGlob, includeCentralPackageFiles: true);
        if (files.Count == 0)
            return (false, $"No files matching '{fileGlob}' found in archive.");

        foreach (var file in files)
        {
            var content = source.ReadAllText(file);
            var matched = options.FirstOrDefault(option =>
                content.Contains(option, StringComparison.OrdinalIgnoreCase));
            if (matched is not null)
                return (true, $"{source.GetRelativePath(file)} contains '{matched}'.");
        }

        return (false, $"None of the {files.Count} file(s) matching '{fileGlob}' contain any of: {string.Join(", ", options)}");
    }

    private static (bool, string) CheckIntegrationSignal(SourceScanContext source, string args)
    {
        var integration = args.Trim();
        if (!IntegrationSignals.TryGetValue(integration, out var signals))
            return (false, $"Unknown integration signal '{integration}'. Supported: {string.Join(", ", IntegrationSignals.Keys)}");

        var files = source.GlobFilePaths(IntegrationSignalFileGlob);
        if (files.Count == 0)
            return (false, $"No source/package files found for integration signal '{integration}'.");

        foreach (var file in files)
        {
            var content = source.ReadAllText(file);
            var matched = signals.FirstOrDefault(signal =>
                content.Contains(signal, StringComparison.OrdinalIgnoreCase));
            if (matched is not null)
                return (true, $"{integration}: {source.GetRelativePath(file)} contains '{matched}'.");
        }

        return (false, $"{integration}: none of the {files.Count} source/package file(s) contain any strong signal: {string.Join(", ", signals)}");
    }

    private static (bool, string) CheckComposeServiceExists(SourceScanContext source, string serviceName)
    {
        serviceName = serviceName.Trim();
        if (string.IsNullOrWhiteSpace(serviceName))
            return (false, "compose-service-exists requires a service name.");

        var services = source.GetComposeServices();
        return services.Contains(serviceName, StringComparer.OrdinalIgnoreCase)
            ? (true, $"Found compose service '{serviceName}'.")
            : (false, $"Compose service '{serviceName}' not found. Services: {string.Join(", ", services)}");
    }

    private static (bool, string) CheckComposeServiceCountAtLeast(SourceScanContext source, string args)
    {
        if (!int.TryParse(args.Trim(), out var minimum))
            return (false, $"Invalid count '{args}' — must be an integer.");

        var services = source.GetComposeServices();
        return services.Count >= minimum
            ? (true, $"Found {services.Count} compose service(s): {string.Join(", ", services)}")
            : (false, $"Expected at least {minimum} compose service(s), found {services.Count}: {string.Join(", ", services)}");
    }

    // --- Glob helpers ---

    private static string[] SplitGlobPatterns(string pattern) =>
        pattern
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();

    private static bool IsProjectFileGlob(string pattern) =>
        pattern.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);

    private static bool GlobMatch(string path, string pattern)
    {
        path    = path.Replace('\\', '/');
        pattern = pattern.TrimStart('/').Replace('\\', '/');

        var regex = "^" + Regex.Escape(pattern)
            .Replace(@"\*\*/", "(.+/)?")
            .Replace(@"\*\*",   ".*")
            .Replace(@"\*",     "[^/]*")
            .Replace(@"\?",     "[^/]")
            + "$";
        return Regex.IsMatch(path, regex, RegexOptions.IgnoreCase);
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

    private static LabTestCaseResult Fail(LabTestCase tc, Guid jobId, string message) => new()
    {
        LabGradingJobId  = jobId,
        LabTestCaseId    = tc.Id,
        Passed           = false,
        AwardedScore     = 0,
        ActualStatusCode = 0,
        ErrorMessage     = message,
    };

    public sealed class SourceScanContext
    {
        private readonly record struct SourcePath(string AbsolutePath, string RelativePath);
        private readonly record struct GlobCacheKey(string Pattern, bool IncludeCentralPackageFiles);

        private readonly List<SourcePath> _files;
        private readonly List<SourcePath> _directories;
        private readonly Dictionary<GlobCacheKey, IReadOnlyList<string>> _fileGlobCache = new();
        private readonly Dictionary<string, IReadOnlyList<string>> _directoryGlobCache = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _contentCache = new(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyList<string>? _composeServices;

        public SourceScanContext(string root)
        {
            Root = Path.GetFullPath(root);
            (_files, _directories) = ScanTree(Root);
            ProjectFileNames = _files
                .Select(f => Path.GetFileName(f.AbsolutePath))
                .Where(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                .Where(f => !f.StartsWith("._", StringComparison.Ordinal))
                .ToList();
        }

        public string Root { get; }

        public IReadOnlyList<string> ProjectFileNames { get; }

        public IReadOnlyList<string> GlobFilePaths(
            string pattern,
            bool includeCentralPackageFiles = false)
        {
            var key = new GlobCacheKey(pattern, includeCentralPackageFiles);
            if (_fileGlobCache.TryGetValue(key, out var cached))
                return cached;

            var patterns = SplitGlobPatterns(pattern);
            if (includeCentralPackageFiles && patterns.Any(IsProjectFileGlob))
                patterns = [.. patterns, "**/*.props", "**/packages.config"];

            var matched = _files
                .Where(f => patterns.Any(p => GlobMatch(f.RelativePath, p)))
                .Select(f => f.AbsolutePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            _fileGlobCache[key] = matched;
            return matched;
        }

        public IReadOnlyList<string> GlobDirectoryPaths(string pattern)
        {
            if (_directoryGlobCache.TryGetValue(pattern, out var cached))
                return cached;

            var patterns = SplitGlobPatterns(pattern);
            var matched = _directories
                .Where(d => patterns.Any(p => GlobMatch(d.RelativePath, p)))
                .Select(d => d.AbsolutePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            _directoryGlobCache[pattern] = matched;
            return matched;
        }

        public string ReadAllText(string path)
        {
            if (_contentCache.TryGetValue(path, out var cached))
                return cached;

            var content = File.ReadAllText(path);
            _contentCache[path] = content;
            return content;
        }

        public string GetRelativePath(string path) => Path.GetRelativePath(Root, path);

        public IReadOnlyList<string> GetComposeServices()
        {
            if (_composeServices is not null)
                return _composeServices;

            var composePath = GlobFilePaths("**/docker-compose.yml").FirstOrDefault()
                ?? GlobFilePaths("**/compose.yml").FirstOrDefault();

            _composeServices = composePath is null ? [] : ParseComposeServices(ReadAllText(composePath));
            return _composeServices;
        }

        private static (List<SourcePath> Files, List<SourcePath> Directories) ScanTree(string root)
        {
            var files = new List<SourcePath>();
            var directories = new List<SourcePath>();
            var pending = new Stack<string>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                var current = pending.Pop();

                foreach (var directory in Directory.EnumerateDirectories(current))
                {
                    if (IgnoredDirectoryNames.Contains(Path.GetFileName(directory)))
                        continue;

                    var fullPath = Path.GetFullPath(directory);
                    directories.Add(new SourcePath(fullPath, NormalizeRelativePath(root, fullPath)));
                    pending.Push(fullPath);
                }

                foreach (var file in Directory.EnumerateFiles(current))
                {
                    var fullPath = Path.GetFullPath(file);
                    files.Add(new SourcePath(fullPath, NormalizeRelativePath(root, fullPath)));
                }
            }

            return (files, directories);
        }

        private static IReadOnlyList<string> ParseComposeServices(string composeContent)
        {
            var services = new List<string>();
            var inServices = false;
            var servicesIndent = -1;

            foreach (var rawLine in composeContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.TrimEnd();
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#')) continue;

                if (!inServices)
                {
                    if (RemoveInlineComment(line).Trim() == "services:")
                    {
                        inServices = true;
                        servicesIndent = CountLeadingWhitespace(rawLine);
                    }
                    continue;
                }

                if (CountLeadingWhitespace(rawLine) <= servicesIndent && !string.IsNullOrWhiteSpace(rawLine))
                    break;

                var match = Regex.Match(rawLine, @"^(?<indent>\s{2,}|\t+)(?<name>[a-zA-Z0-9][a-zA-Z0-9_.-]*):\s*(?:#.*)?$");
                if (!match.Success) continue;

                var indent = match.Groups["indent"].Value.Length;
                if (indent == servicesIndent + 2 || indent == servicesIndent + 1)
                    services.Add(match.Groups["name"].Value);
            }

            return services.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string NormalizeRelativePath(string root, string path) =>
            Path.GetRelativePath(root, path).Replace('\\', '/');
    }
}
