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
///   file-not-contains:**/Controllers/*.cs:DbContext — no file contains text
///   compose-service-exists:api-gateway — docker-compose.yml defines this service
///   compose-service-count-at-least:7   — docker-compose.yml defines at least N services
/// </summary>
public class SourceAnalyzer
{
    public LabTestCaseResult Check(LabTestCase tc, string workDir, Guid jobId)
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
                "project-name"       => CheckProjectName(workDir, ruleArgs),
                "project-count"      => CheckProjectCount(workDir, ruleArgs),
                "project-count-at-least" => CheckProjectCountAtLeast(workDir, ruleArgs),
                "folder-exists"      => CheckFolderExists(workDir, ruleArgs),
                "file-exists"        => CheckFileExists(workDir, ruleArgs),
                "file-count-at-least" => CheckFileCountAtLeast(workDir, ruleArgs),
                "file-contains"      => CheckFilePattern(workDir, ruleArgs, mustContain: true),
                "file-contains-any"  => CheckFileContainsAny(workDir, ruleArgs),
                "file-not-contains"  => CheckFilePattern(workDir, ruleArgs, mustContain: false),
                "compose-service-exists" => CheckComposeServiceExists(workDir, ruleArgs),
                "compose-service-count-at-least" => CheckComposeServiceCountAtLeast(workDir, ruleArgs),
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
    private static (bool, string) CheckProjectName(string workDir, string pattern)
    {
        if (!pattern.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            pattern += ".csproj";
        var found = GlobFiles(workDir, $"**/{pattern}")
            .Select(Path.GetFileName).ToList();
        return found.Count > 0
            ? (true,  $"Found: {string.Join(", ", found)}")
            : (false, $"No .csproj matching '{pattern}' found in archive.");
    }

    // project-count:3  →  archive contains exactly N .csproj files (ignores macOS ._* metadata files)
    private static (bool, string) CheckProjectCount(string workDir, string args)
    {
        if (!int.TryParse(args.Trim(), out var expected))
            return (false, $"Invalid count '{args}' — must be an integer.");
        var files = GetProjectFileNames(workDir);
        return files.Count == expected
            ? (true,  $"Found {files.Count} project(s): {string.Join(", ", files)}")
            : (false, $"Expected {expected} project(s), found {files.Count}: {string.Join(", ", files)}");
    }

    private static (bool, string) CheckProjectCountAtLeast(string workDir, string args)
    {
        if (!int.TryParse(args.Trim(), out var minimum))
            return (false, $"Invalid count '{args}' — must be an integer.");

        var files = GetProjectFileNames(workDir);
        return files.Count >= minimum
            ? (true, $"Found {files.Count} project(s): {string.Join(", ", files)}")
            : (false, $"Expected at least {minimum} project(s), found {files.Count}: {string.Join(", ", files)}");
    }

    // folder-exists:**/Controllers  →  at least one matching directory exists
    private static (bool, string) CheckFolderExists(string workDir, string pattern)
    {
        var found = GlobDirs(workDir, pattern)
            .Select(d => Path.GetRelativePath(workDir, d)).ToList();
        return found.Count > 0
            ? (true,  $"Found: {string.Join(", ", found)}")
            : (false, $"No folder matching '{pattern}' found.");
    }

    // file-exists:**/docker-compose.yml  →  at least one matching file exists
    private static (bool, string) CheckFileExists(string workDir, string pattern)
    {
        var found = GlobFiles(workDir, pattern)
            .Select(f => Path.GetRelativePath(workDir, f)).ToList();
        return found.Count > 0
            ? (true,  $"Found: {string.Join(", ", found)}")
            : (false, $"No file matching '{pattern}' found.");
    }

    private static (bool, string) CheckFileCountAtLeast(string workDir, string args)
    {
        var sep = args.LastIndexOf(':');
        if (sep < 0)
            return (false, "file-count-at-least requires 'glob:count' format.");

        var fileGlob = args[..sep];
        var countText = args[(sep + 1)..];
        if (!int.TryParse(countText.Trim(), out var minimum))
            return (false, $"Invalid count '{countText}' — must be an integer.");

        var found = GlobFiles(workDir, fileGlob)
            .Select(f => Path.GetRelativePath(workDir, f))
            .ToList();

        return found.Count >= minimum
            ? (true, $"Found {found.Count} file(s): {string.Join(", ", found)}")
            : (false, $"Expected at least {minimum} file(s) matching '{fileGlob}', found {found.Count}: {string.Join(", ", found)}");
    }

    // file-contains:**/Services/*.cs:IRepository
    //   mustContain=true  → passes if AT LEAST ONE matching file contains the text
    //   mustContain=false → passes if NO matching file contains the text
    private static (bool, string) CheckFilePattern(string workDir, string args, bool mustContain)
    {
        var sep = args.IndexOf(':');
        if (sep < 0)
            return (false, $"{(mustContain ? "file-contains" : "file-not-contains")} requires 'glob:text' format.");

        var fileGlob  = args[..sep];
        var searchText = args[(sep + 1)..];

        var files = GlobFiles(workDir, fileGlob).ToList();
        if (files.Count == 0)
            return (false, $"No files matching '{fileGlob}' found in archive.");

        var matches = files
            .Where(f => File.ReadAllText(f).Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetRelativePath(workDir, f))
            .ToList();

        return mustContain
            ? matches.Count > 0
                ? (true,  $"{matches.Count}/{files.Count} file(s) contain '{searchText}'.")
                : (false, $"None of the {files.Count} file(s) matching '{fileGlob}' contain '{searchText}'.")
            : matches.Count == 0
                ? (true,  $"None of the {files.Count} file(s) contain '{searchText}'.")
                : (false, $"{matches.Count} file(s) contain '{searchText}': {string.Join(", ", matches)}");
    }

    private static (bool, string) CheckFileContainsAny(string workDir, string args)
    {
        var sep = args.IndexOf(':');
        if (sep < 0)
            return (false, "file-contains-any requires 'glob:text1|text2' format.");

        var fileGlob = args[..sep];
        var options = args[(sep + 1)..]
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (options.Length == 0)
            return (false, "file-contains-any requires at least one search option.");

        var files = GlobFiles(workDir, fileGlob).ToList();
        if (files.Count == 0)
            return (false, $"No files matching '{fileGlob}' found in archive.");

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            var matched = options.FirstOrDefault(option =>
                content.Contains(option, StringComparison.OrdinalIgnoreCase));
            if (matched is not null)
                return (true, $"{Path.GetRelativePath(workDir, file)} contains '{matched}'.");
        }

        return (false, $"None of the {files.Count} file(s) matching '{fileGlob}' contain any of: {string.Join(", ", options)}");
    }

    private static (bool, string) CheckComposeServiceExists(string workDir, string serviceName)
    {
        serviceName = serviceName.Trim();
        if (string.IsNullOrWhiteSpace(serviceName))
            return (false, "compose-service-exists requires a service name.");

        var services = GetComposeServices(workDir);
        return services.Contains(serviceName, StringComparer.OrdinalIgnoreCase)
            ? (true, $"Found compose service '{serviceName}'.")
            : (false, $"Compose service '{serviceName}' not found. Services: {string.Join(", ", services)}");
    }

    private static (bool, string) CheckComposeServiceCountAtLeast(string workDir, string args)
    {
        if (!int.TryParse(args.Trim(), out var minimum))
            return (false, $"Invalid count '{args}' — must be an integer.");

        var services = GetComposeServices(workDir);
        return services.Count >= minimum
            ? (true, $"Found {services.Count} compose service(s): {string.Join(", ", services)}")
            : (false, $"Expected at least {minimum} compose service(s), found {services.Count}: {string.Join(", ", services)}");
    }

    private static List<string> GetProjectFileNames(string workDir) =>
        Directory
            .EnumerateFiles(workDir, "*.csproj", SearchOption.AllDirectories)
            .Select(f => Path.GetFileName(f)!)
            .Where(f => !f.StartsWith("._", StringComparison.Ordinal))
            .ToList();

    private static List<string> GetComposeServices(string workDir)
    {
        var composePath = GlobFiles(workDir, "**/docker-compose.yml").FirstOrDefault()
            ?? GlobFiles(workDir, "**/compose.yml").FirstOrDefault();

        if (composePath is null) return [];

        var services = new List<string>();
        var inServices = false;
        var servicesIndent = -1;

        foreach (var rawLine in File.ReadLines(composePath))
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

    // --- Glob helpers ---

    private static IEnumerable<string> GlobFiles(string root, string pattern) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                 .Where(f => GlobMatch(Path.GetRelativePath(root, f), pattern));

    private static IEnumerable<string> GlobDirs(string root, string pattern) =>
        Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                 .Where(d => GlobMatch(Path.GetRelativePath(root, d), pattern));

    private static bool GlobMatch(string path, string pattern)
    {
        path    = path.Replace('\\', '/');
        pattern = pattern.TrimStart('/').Replace('\\', '/');

        var regex = "^" + Regex.Escape(pattern)
            .Replace(@"\*\*\/", "(.+/)?")
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
}
