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
///   folder-exists:**/Controllers        — at least one folder matches glob
///   file-exists:**/docker-compose.yml  — at least one file matches glob
///   file-contains:**/Services/*.cs:IRepository     — at least one file contains text
///   file-not-contains:**/Controllers/*.cs:DbContext — no file contains text
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
                "folder-exists"      => CheckFolderExists(workDir, ruleArgs),
                "file-exists"        => CheckFileExists(workDir, ruleArgs),
                "file-contains"      => CheckFilePattern(workDir, ruleArgs, mustContain: true),
                "file-not-contains"  => CheckFilePattern(workDir, ruleArgs, mustContain: false),
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
        var files = Directory
            .EnumerateFiles(workDir, "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Where(f => !f!.StartsWith("._", StringComparison.Ordinal))
            .ToList();
        return files.Count == expected
            ? (true,  $"Found {files.Count} project(s): {string.Join(", ", files)}")
            : (false, $"Expected {expected} project(s), found {files.Count}: {string.Join(", ", files)}");
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
