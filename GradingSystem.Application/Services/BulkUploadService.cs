using System.IO.Compression;
using System.Text.Json;
using GradingSystem.Application.Common;
using GradingSystem.Application.DTOs;
using GradingSystem.Application.Exceptions;
using GradingSystem.Application.Interfaces;
using GradingSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace GradingSystem.Application.Services;

public class BulkUploadService(
    IUnitOfWork uow,
    IConfiguration config,
    ILogger<BulkUploadService> logger) : IBulkUploadService
{
    private readonly string _basePath = string.IsNullOrEmpty(config["Storage:BasePath"]) ? "/storage" : config["Storage:BasePath"]!;

    public async Task<BulkUploadResultDto> ParseAndCreateAsync(
        Guid assignmentId,
        string gradingRound,
        Stream masterZipStream,
        CancellationToken ct = default)
    {
        _ = await uow.Assignments.GetByIdAsync(assignmentId)
            ?? throw new NotFoundException($"Assignment '{assignmentId}' not found.");

        var participants = (await uow.Participants.FindAsync(p => p.AssignmentId == assignmentId)).ToList();
        var participantByUsername = participants.ToDictionary(p => p.Username, StringComparer.OrdinalIgnoreCase);

        var result = new BulkUploadResultDto();
        var tempRoot = Path.Combine(Path.GetTempPath(), "bulk_upload", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var createdArtifactDirs = new List<string>();

        try
        {
            using var archive = new ZipArchive(masterZipStream, ZipArchiveMode.Read, leaveOpen: true);
            ExtractArchiveToDirectory(archive, tempRoot);

            // Top-level directories = student folders (e.g. "hoalvpse181951").
            // Some exports wrap all student folders in one extra directory (e.g. the exam
            // paper code "5/"), so descend through non-matching wrapper levels until we
            // reach the level whose folder names actually match participants.
            var studentDirs = FindStudentDirs(tempRoot, participantByUsername);
            result.Parsed = studentDirs.Length;

            var seenUsernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var studentDir in studentDirs)
            {
                var folderName = Path.GetFileName(studentDir).ToLowerInvariant();
                seenUsernames.Add(folderName);

                if (!participantByUsername.TryGetValue(folderName, out var participant))
                {
                    result.Errors.Add($"Folder '{folderName}': no matching participant found in session.");
                    continue;
                }

                // Load questions for participant's assignment
                var questions = (await uow.Questions.FindAsync(q => q.AssignmentId == participant.AssignmentId)).ToList();

                // Build per-question directories and repack artifact.zip
                var submissionId = Guid.NewGuid();
                var artifactDir = Path.Combine(_basePath, "submissions", submissionId.ToString());
                Directory.CreateDirectory(artifactDir);
                createdArtifactDirs.Add(artifactDir);
                var artifactZipPath = Path.Combine(artifactDir, "artifact.zip");

                using (var outZip = ZipFile.Open(artifactZipPath, ZipArchiveMode.Create))
                {
                    foreach (var question in questions)
                    {
                        // Look for subfolder matching ArtifactFolderName (exact, then case-insensitive)
                        var qFolder = FindQuestionFolder(studentDir, question.ArtifactFolderName);
                        if (qFolder is null)
                        {
                            var msg = $"Student '{folderName}': question folder '{question.ArtifactFolderName}' not found — this question will be graded as if not submitted. Check that Question.ArtifactFolderName matches the real submission folder name (e.g. \"1\"/\"2\", not \"Q1\"/\"Q2\").";
                            logger.LogWarning(msg);
                            result.Errors.Add(msg);
                            continue;
                        }

                        // Find solution.zip inside the question folder
                        var solutionZip = Directory.GetFiles(qFolder, "*.zip", SearchOption.TopDirectoryOnly).FirstOrDefault();
                        if (solutionZip is null)
                        {
                            var msg = $"Student '{folderName}': no zip found in '{qFolder}' — this question will be graded as if not submitted.";
                            logger.LogWarning(msg);
                            result.Errors.Add(msg);
                            continue;
                        }

                        // Extract solution.zip contents into a temp dir then add to output zip
                        var extractDir = Path.Combine(tempRoot, $"extract_{folderName}_{question.ArtifactFolderName}");
                        ZipFile.ExtractToDirectory(solutionZip, extractDir, overwriteFiles: true);

                        foreach (var file in Directory.GetFiles(extractDir, "*", SearchOption.AllDirectories))
                        {
                            var relativePath = Path.GetRelativePath(extractDir, file);
                            var entryName = $"{question.ArtifactFolderName}/{relativePath.Replace('\\', '/')}";
                            outZip.CreateEntryFromFile(file, entryName, CompressionLevel.NoCompression);
                        }
                    }
                }

                // Create or update Submission
                var existing = (await uow.Submissions.FindAsync(
                    s => s.ParticipantId == participant.Id && s.GradingRound == gradingRound)).FirstOrDefault();

                if (existing is not null)
                {
                    // Replace artifact
                    if (!string.IsNullOrEmpty(existing.ArtifactZipPath) && File.Exists(existing.ArtifactZipPath))
                        File.Delete(existing.ArtifactZipPath);

                    existing.ArtifactZipPath = artifactZipPath.Replace('\\', '/');
                    existing.HasArtifact     = true;
                    existing.Status          = SubmissionStatus.Pending;
                    uow.Submissions.Update(existing);
                }
                else
                {
                    var submission = new Submission
                    {
                        Id              = submissionId,
                        AssignmentId    = participant.AssignmentId,
                        ParticipantId   = participant.Id,
                        StudentCode     = participant.StudentCode,
                        GradingRound    = gradingRound,
                        ArtifactZipPath = artifactZipPath.Replace('\\', '/'),
                        HasArtifact     = true,
                        Status          = SubmissionStatus.Pending,
                    };
                    await uow.Submissions.AddAsync(submission);
                }

                result.Created++;
            }

            // Create zero-score placeholders for participants without a folder in the zip
            foreach (var participant in participants)
            {
                if (seenUsernames.Contains(participant.Username)) continue;

                var existing = await uow.Submissions.FindAsync(
                    s => s.ParticipantId == participant.Id && s.GradingRound == gradingRound);

                if (existing.Any()) continue;

                var missingSubmission = new Submission
                {
                    AssignmentId  = participant.AssignmentId,
                    ParticipantId = participant.Id,
                    StudentCode   = participant.StudentCode,
                    GradingRound  = gradingRound,
                    HasArtifact   = false,
                    ArtifactZipPath = string.Empty,
                    Status        = SubmissionStatus.Done,
                };
                await uow.Submissions.AddAsync(missingSubmission);

                // Insert 0-score QuestionResults immediately (no grading job)
                var questions = await uow.Questions.FindAsync(q => q.AssignmentId == participant.AssignmentId);
                foreach (var q in questions)
                {
                    await uow.QuestionResults.AddAsync(new QuestionResult
                    {
                        SubmissionId = missingSubmission.Id,
                        QuestionId   = q.Id,
                        Score        = 0,
                        MaxScore     = q.MaxScore,
                        Detail       = MakeNote("Sinh viên không nộp bài"),
                    });
                }

                result.Missing++;
            }

            await uow.SaveChangesAsync(ct);
        }
        catch
        {
            // Save failed (e.g. a unique-constraint collision that CreateNewRoundAsync will retry) —
            // the artifact zips already written to persistent storage for this attempt are now
            // orphaned (no Submission row references them), so clean them up before rethrowing.
            foreach (var dir in createdArtifactDirs)
            {
                try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
            }
            throw;
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { /* best effort */ }
        }

        return result;
    }

    public async Task<BulkUploadResultDto> ParseAndCreateForLatestRoundAsync(
        Guid assignmentId, Stream masterZipStream, CancellationToken ct = default)
    {
        var existingRounds = (await uow.Submissions.FindAsync(s => s.AssignmentId == assignmentId))
            .Select(s => s.GradingRound).Distinct().ToList();
        var targetRound = GradingRoundHelper.LatestRoundLabel(existingRounds);

        return await ParseAndCreateAsync(assignmentId, targetRound, masterZipStream, ct);
    }

    public async Task<BulkUploadResultDto> CreateNewRoundAsync(
        Guid assignmentId, Stream masterZipStream, CancellationToken ct = default)
    {
        _ = await uow.Assignments.GetByIdAsync(assignmentId)
            ?? throw new NotFoundException($"Assignment '{assignmentId}' not found.");

        // Buffer once so a unique-constraint retry can re-read the archive without
        // touching the caller's (possibly non-seekable) request stream.
        using var buffered = new MemoryStream();
        await masterZipStream.CopyToAsync(buffered, ct);

        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var existingRounds = (await uow.Submissions.FindAsync(s => s.AssignmentId == assignmentId))
                .Select(s => s.GradingRound).Distinct().ToList();
            var nextRound = GradingRoundHelper.NextRoundLabel(existingRounds);

            buffered.Position = 0;
            try
            {
                return await ParseAndCreateAsync(assignmentId, nextRound, buffered, ct);
            }
            catch (DbUpdateException ex) when (attempt < maxAttempts && IsUniqueViolation(ex))
            {
                logger.LogWarning(ex,
                    "Round '{Round}' collided with a concurrent creation on attempt {Attempt}, retrying.",
                    nextRound, attempt);
            }
        }

        throw new BadRequestException(
            "Could not create a new grading round after multiple attempts due to concurrent creation; please retry.");
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: "23505" };

    public static string MakeNote(string message) =>
        JsonSerializer.Serialize(new[]
        {
            new TestCaseResult
            {
                TestCaseId    = Guid.Empty,
                Pass          = false,
                AwardedScore  = 0,
                HttpMethod    = "-",
                Url           = "-",
                ActualStatus  = 0,
                FailReason    = message,
            }
        });

    /// Descends through wrapper directories (e.g. an exam-code folder wrapping every
    /// student folder) until it finds the level whose directory names match participant
    /// usernames. Picks the depth with the MOST matches (not the first non-zero match) so
    /// inconsistently-nested zips — where one folder coincidentally matches at a shallower
    /// depth than the rest of the class — don't cause the real cohort to be missed. Falls
    /// back to the immediate top-level directories if no level matches (preserves the
    /// original "no matching participant found" error reporting).
    private static string[] FindStudentDirs(
        string tempRoot, IReadOnlyDictionary<string, Participant> participantByUsername)
    {
        const int maxDepth = 5;
        var topLevelDirs = Directory.GetDirectories(tempRoot);

        var currentLevel = topLevelDirs;
        var bestLevel = topLevelDirs;
        var bestMatchCount = 0;

        for (var depth = 0; depth < maxDepth; depth++)
        {
            if (currentLevel.Length == 0) break;

            var matchCount = currentLevel.Count(d => participantByUsername.ContainsKey(Path.GetFileName(d)));
            if (matchCount > bestMatchCount)
            {
                bestMatchCount = matchCount;
                bestLevel = currentLevel;
            }

            if (matchCount == participantByUsername.Count) break;

            currentLevel = currentLevel
                .Where(d => !IsBuildArtifactDir(Path.GetFileName(d)))
                .SelectMany(Directory.GetDirectories)
                .ToArray();
        }

        return bestMatchCount > 0 ? bestLevel : topLevelDirs;
    }

    private static bool IsBuildArtifactDir(string name) =>
        name is "bin" or "obj" or "node_modules" or ".git" or ".vs";

    private static string? FindQuestionFolder(string studentDir, string artifactFolderName)
    {
        // Exact match first
        var exact = Path.Combine(studentDir, artifactFolderName);
        if (Directory.Exists(exact)) return exact;

        // Case-insensitive match
        foreach (var dir in Directory.GetDirectories(studentDir))
        {
            if (string.Equals(Path.GetFileName(dir), artifactFolderName, StringComparison.OrdinalIgnoreCase))
                return dir;
        }

        return null;
    }

    private static void ExtractArchiveToDirectory(ZipArchive archive, string destinationRoot)
    {
        var rootFullPath = Path.GetFullPath(destinationRoot);
        Directory.CreateDirectory(rootFullPath);

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.FullName))
                continue;

            // Normalize zip separators so extraction is consistent across OSes/zip tools.
            var normalized = entry.FullName.Replace('\\', '/').TrimStart('/');
            if (normalized.Length == 0 || normalized.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase))
                continue;

            var relativePath = normalized.Replace('/', Path.DirectorySeparatorChar);
            var destinationPath = Path.GetFullPath(Path.Combine(rootFullPath, relativePath));
            if (!destinationPath.StartsWith(rootFullPath, StringComparison.Ordinal))
                continue;

            if (normalized.EndsWith('/'))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDir))
                Directory.CreateDirectory(destinationDir);

            using var source = entry.Open();
            using var target = File.Create(destinationPath);
            source.CopyTo(target);
        }
    }
}
