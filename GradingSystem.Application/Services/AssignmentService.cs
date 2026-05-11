using GradingSystem.Application.Common;
using GradingSystem.Application.DTOs;
using GradingSystem.Application.Exceptions;
using GradingSystem.Application.Interfaces;
using GradingSystem.Application.Messaging;
using GradingSystem.Domain.Entities;
using MassTransit;
using Microsoft.Extensions.Configuration;

namespace GradingSystem.Application.Services;

public class AssignmentService(IUnitOfWork unitOfWork, IConfiguration configuration, IPublishEndpoint publishEndpoint) : IAssignmentService
{
    private readonly string _storageBasePath = configuration["Storage:BasePath"] ?? "/storage";

    public async Task<AssignmentDto> CreateAsync(CreateAssignmentRequest req, CancellationToken ct = default)
    {
        var code = req.Code.Trim().ToUpperInvariant();
        var existing = await unitOfWork.Assignments.FindAsync(a =>
            a.Code == code &&
            (req.ExamSessionId == null
                ? a.ExamSessionId == null
                : a.ExamSessionId == req.ExamSessionId));
        if (existing.Any())
            throw new BadRequestException($"Assignment code '{code}' is already in use within this exam session.");

        var entity = new Assignment
        {
            Code          = code,
            Title         = req.Title.Trim(),
            Description   = req.Description?.Trim(),
            ExamSessionId = req.ExamSessionId,
        };

        await unitOfWork.Assignments.AddAsync(entity);
        await unitOfWork.SaveChangesAsync(ct);

        return Map(entity);
    }

    public async Task<AssignmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await unitOfWork.Assignments.GetByIdAsync(id);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<AssignmentSummaryDto>> GetSummariesAsync(CancellationToken ct = default)
    {
        var entities = await unitOfWork.Assignments.GetAllAsync();
        return entities
            .Select(e => new AssignmentSummaryDto
            {
                Id          = e.Id,
                Code        = e.Code,
                Title       = e.Title,
                Description = e.Description,
                CreatedAt   = e.CreatedAt,
            })
            .ToList();
    }

    public async Task<AssignmentDto> UpsertResourcesAsync(
        Guid id,
        UpsertAssignmentResourcesRequest request,
        CancellationToken ct = default)
    {
        var hasSql = request.DatabaseSql.HasValue;
        var hasUrl = !string.IsNullOrWhiteSpace(request.GivenApiBaseUrl);
        var hasZip = request.GivenZip.HasValue;

        if (!hasSql && !hasUrl && !hasZip)
            throw new BadRequestException("Provide at least one of: databaseSql file, givenApiBaseUrl, or givenZip file.");

        var entity = await unitOfWork.Assignments.GetByIdAsync(id)
            ?? throw new NotFoundException($"Assignment '{id}' not found.");

        if (hasSql)
        {
            var (fileName, stream) = request.DatabaseSql!.Value;
            EnsureExtension(fileName, ".sql");
            entity.DatabaseSqlPath = await SaveAssignmentFileAsync(id, "database.sql", stream, ct);
        }

        if (hasUrl)
        {
            var url = request.GivenApiBaseUrl!.Trim();
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new BadRequestException("GivenApiBaseUrl must be a valid absolute HTTP/HTTPS URL.");

            entity.GivenApiBaseUrl = url;
        }

        if (hasZip)
        {
            var (fileName, stream) = request.GivenZip!.Value;
            EnsureExtension(fileName, ".zip");
            FileHelper.SafeDelete(entity.GivenZipPath);
            entity.GivenZipPath = await SaveAssignmentFileAsync(id, "given.zip", stream, ct);
        }

        unitOfWork.Assignments.Update(entity);
        await unitOfWork.SaveChangesAsync(ct);

        return Map(entity);
    }

    public async Task<AssignmentDto> DeleteAsync(Guid assignmentId, CancellationToken ct = default)
    {
        var entity = await unitOfWork.Assignments.GetByIdAsync(assignmentId)
            ?? throw new NotFoundException($"Assignment '{assignmentId}' not found.");

        FileHelper.SafeDelete(entity.DatabaseSqlPath);
        FileHelper.SafeDelete(entity.GivenZipPath);

        // Get all questions for this assignment
        var questions = await unitOfWork.Questions.FindAsync(q => q.AssignmentId == assignmentId);

        foreach (var question in questions)
        {
            // Delete test cases for this question
            var testCases = await unitOfWork.TestCases.FindAsync(t => t.QuestionId == question.Id);
            foreach (var testCase in testCases)
                unitOfWork.TestCases.Remove(testCase);

            // Delete the question
            unitOfWork.Questions.Remove(question);
        }

        // Get all submissions for this assignment
        var submissions = await unitOfWork.Submissions.FindAsync(s => s.AssignmentId == assignmentId);

        foreach (var submission in submissions)
        {
            // Delete grading jobs
            var jobs = await unitOfWork.GradingJobs.FindAsync(j => j.SubmissionId == submission.Id);
            foreach (var job in jobs)
                unitOfWork.GradingJobs.Remove(job);

            // Delete question results
            var results = await unitOfWork.QuestionResults.FindAsync(r => r.SubmissionId == submission.Id);
            foreach (var result in results)
                unitOfWork.QuestionResults.Remove(result);

            FileHelper.SafeDelete(submission.ArtifactZipPath);

            // Delete the submission
            unitOfWork.Submissions.Remove(submission);
        }

        // Delete the assignment
        unitOfWork.Assignments.Remove(entity);
        await unitOfWork.SaveChangesAsync(ct);

        return Map(entity);
    }

    public async Task<ImportParticipantsResultDto> ImportParticipantsAsync(
        Guid assignmentId, Stream csvStream, CancellationToken ct = default)
    {
        var assignment = await unitOfWork.Assignments.GetByIdAsync(assignmentId)
            ?? throw new NotFoundException($"Assignment '{assignmentId}' not found.");

        if (assignment.ExamSessionId is null)
            throw new BadRequestException($"Assignment '{assignmentId}' is not linked to an exam session.");

        var sessionId = assignment.ExamSessionId.Value;
        var result = new ImportParticipantsResultDto();
        using var reader = new StreamReader(csvStream);

        string? line;
        int lineNumber = 0;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (lineNumber == 1 && line.TrimStart().StartsWith("username", StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = line.Split(',');
            if (parts.Length < 2)
            {
                result.Errors.Add($"Line {lineNumber}: expected 'username,studentCode'.");
                continue;
            }

            var username    = parts[0].Trim().ToLowerInvariant();
            var studentCode = parts[1].Trim();

            var existing = await unitOfWork.Participants.FindAsync(
                p => p.ExamSessionId == sessionId && p.Username == username);

            if (existing.Any())
            {
                result.Skipped++;
                continue;
            }

            await unitOfWork.Participants.AddAsync(new Participant
            {
                ExamSessionId = sessionId,
                Username      = username,
                StudentCode   = studentCode,
                AssignmentId  = assignmentId,
            });
            result.Created++;
        }

        if (result.Created > 0)
            await unitOfWork.SaveChangesAsync(ct);

        return result;
    }

    public async Task<IReadOnlyList<ParticipantDto>> GetParticipantsAsync(
        Guid assignmentId, CancellationToken ct = default)
    {
        _ = await unitOfWork.Assignments.GetByIdAsync(assignmentId)
            ?? throw new NotFoundException($"Assignment '{assignmentId}' not found.");

        var participants = await unitOfWork.Participants.FindAsync(p => p.AssignmentId == assignmentId);
        var assignment = await unitOfWork.Assignments.GetByIdAsync(assignmentId);

        return participants.Select(p => new ParticipantDto
        {
            Id              = p.Id,
            ExamSessionId   = p.ExamSessionId,
            Username        = p.Username,
            StudentCode     = p.StudentCode,
            AssignmentId    = p.AssignmentId,
            AssignmentCode  = assignment?.Code ?? "",
            AssignmentTitle = assignment?.Title ?? "",
            CreatedAt       = p.CreatedAt,
        }).ToList();
    }

    public async Task<int> TriggerGradeAsync(Guid assignmentId, string gradingRound, CancellationToken ct = default)
    {
        _ = await unitOfWork.Assignments.GetByIdAsync(assignmentId)
            ?? throw new NotFoundException($"Assignment '{assignmentId}' not found.");

        var submissions = (await unitOfWork.Submissions.FindAsync(
            s => s.AssignmentId == assignmentId
              && s.GradingRound == gradingRound
              && s.HasArtifact
              && s.Status == SubmissionStatus.Pending)).ToList();

        int enqueued = 0;
        foreach (var submission in submissions)
        {
            var job = new GradingJob
            {
                SubmissionId = submission.Id,
                GradingRound = submission.GradingRound,
                Status       = JobStatus.Pending,
            };
            submission.Status = SubmissionStatus.Grading;
            unitOfWork.Submissions.Update(submission);
            await unitOfWork.GradingJobs.AddAsync(job);
            await unitOfWork.SaveChangesAsync(ct);
            await publishEndpoint.Publish(new GradeJobMessage(job.Id), ct);
            enqueued++;
        }

        return enqueued;
    }

    private async Task<string> SaveAssignmentFileAsync(Guid assignmentId, string targetFileName, Stream content, CancellationToken ct)
    {
        var directory = Path.Combine(_storageBasePath, "assignments", assignmentId.ToString());
        Directory.CreateDirectory(directory);

        var fullPath = Path.Combine(directory, targetFileName);
        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, ct);

        return fullPath.Replace('\\', '/');
    }

    private static void EnsureExtension(string fileName, string expectedExtension)
    {
        if (!string.Equals(Path.GetExtension(fileName), expectedExtension, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException($"Invalid file type. Expected '{expectedExtension}'.");
    }

    private static AssignmentDto Map(Assignment entity) => new()
    {
        Id              = entity.Id,
        Code            = entity.Code,
        Title           = entity.Title,
        Description     = entity.Description,
        ExamSessionId   = entity.ExamSessionId,
        DatabaseSqlPath = entity.DatabaseSqlPath,
        GivenApiBaseUrl = entity.GivenApiBaseUrl,
        HasGivenZip     = entity.GivenZipPath != null,
        CreatedAt       = entity.CreatedAt,
    };
}
