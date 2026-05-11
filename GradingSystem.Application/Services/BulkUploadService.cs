using System.IO.Compression;
using System.Text.Json;
using GradingSystem.Application.Common;
using GradingSystem.Application.DTOs;
using GradingSystem.Application.Exceptions;
using GradingSystem.Application.Interfaces;
using GradingSystem.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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

        try
        {
            using var archive = new ZipArchive(masterZipStream, ZipArchiveMode.Read, leaveOpen: true);
            ExtractArchiveToDirectory(archive, tempRoot);

            // Top-level directories = student folders (e.g. "hoalvpse181951")
            var studentDirs = Directory.GetDirectories(tempRoot);
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
                var artifactZipPath = Path.Combine(artifactDir, "artifact.zip");

                using (var outZip = ZipFile.Open(artifactZipPath, ZipArchiveMode.Create))
                {
                    foreach (var question in questions)
                    {
                        // Look for subfolder matching ArtifactFolderName or numeric index
                        var qFolder = FindQuestionFolder(studentDir, question.ArtifactFolderName);
                        if (qFolder is null)
                        {
                            logger.LogWarning("Student '{Folder}': question folder '{Q}' not found", folderName, question.ArtifactFolderName);
                            continue;
                        }

                        // Find solution.zip inside the question folder
                        var solutionZip = Directory.GetFiles(qFolder, "*.zip", SearchOption.TopDirectoryOnly).FirstOrDefault();
                        if (solutionZip is null)
                        {
                            logger.LogWarning("Student '{Folder}': no zip found in '{Q}'", folderName, qFolder);
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
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { /* best effort */ }
        }

        return result;
    }

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
