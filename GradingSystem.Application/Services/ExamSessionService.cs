using System.Text.Json;
using System.Text.RegularExpressions;
using GradingSystem.Application.Common;
using GradingSystem.Application.DTOs;
using GradingSystem.Application.Exceptions;
using GradingSystem.Application.Interfaces;
using GradingSystem.Application.Messaging;
using GradingSystem.Domain.Entities;
using MassTransit;

namespace GradingSystem.Application.Services;

public partial class ExamSessionService(IUnitOfWork unitOfWork, IPublishEndpoint publishEndpoint) : IExamSessionService
{
    public async Task<ExamSessionDto> CreateAsync(CreateExamSessionRequest req, CancellationToken ct = default)
    {
        var entity = new ExamSession
        {
            Title       = req.Title.Trim(),
            Description = req.Description?.Trim(),
        };

        await unitOfWork.ExamSessions.AddAsync(entity);
        await unitOfWork.SaveChangesAsync(ct);

        return MapSummary(entity);
    }

    public async Task<ExamSessionDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await unitOfWork.ExamSessions.GetByIdAsync(id);
        if (entity is null) return null;

        var assignments = await unitOfWork.Assignments.FindAsync(a => a.ExamSessionId == id);

        return new ExamSessionDto
        {
            Id          = entity.Id,
            Title       = entity.Title,
            Description = entity.Description,
            CreatedAt   = entity.CreatedAt,
            Assignments = assignments.Select(a => new AssignmentSummaryDto
            {
                Id          = a.Id,
                Code        = a.Code,
                Title       = a.Title,
                Description = a.Description,
                CreatedAt   = a.CreatedAt,
            }).ToList(),
        };
    }

    public async Task<IReadOnlyList<ExamSessionSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await unitOfWork.ExamSessions.GetAllAsync();
        return entities.OrderByDescending(e => e.CreatedAt)
            .Select(e => new ExamSessionSummaryDto
            {
                Id          = e.Id,
                Title       = e.Title,
                Description = e.Description,
                CreatedAt   = e.CreatedAt,
            }).ToList();
    }

    public async Task<ExamSessionDto> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await unitOfWork.ExamSessions.GetByIdAsync(id)
            ?? throw new NotFoundException($"ExamSession '{id}' not found.");

        var participants = await unitOfWork.Participants.FindAsync(p => p.ExamSessionId == id);
        foreach (var p in participants)
            unitOfWork.Participants.Remove(p);

        var assignments = await unitOfWork.Assignments.FindAsync(a => a.ExamSessionId == id);
        foreach (var a in assignments)
        {
            a.ExamSessionId = null;
            unitOfWork.Assignments.Update(a);
        }

        unitOfWork.ExamSessions.Remove(entity);
        await unitOfWork.SaveChangesAsync(ct);

        return MapSummary(entity);
    }

    public async Task<IReadOnlyList<ParticipantDto>> GetParticipantsAsync(Guid sessionId, CancellationToken ct = default)
    {
        _ = await unitOfWork.ExamSessions.GetByIdAsync(sessionId)
            ?? throw new NotFoundException($"ExamSession '{sessionId}' not found.");

        var participants = await unitOfWork.Participants.FindAsync(p => p.ExamSessionId == sessionId);
        var assignmentIds = participants.Select(p => p.AssignmentId).Distinct().ToList();
        var assignmentTitles = new Dictionary<Guid, string>();
        var assignmentCodes  = new Dictionary<Guid, string>();
        foreach (var aId in assignmentIds)
        {
            var a = await unitOfWork.Assignments.GetByIdAsync(aId);
            if (a is not null)
            {
                assignmentTitles[aId] = a.Title;
                assignmentCodes[aId]  = a.Code;
            }
        }

        return participants.Select(p => new ParticipantDto
        {
            Id              = p.Id,
            ExamSessionId   = p.ExamSessionId,
            Username        = p.Username,
            StudentCode     = p.StudentCode,
            AssignmentId    = p.AssignmentId,
            AssignmentCode  = assignmentCodes.GetValueOrDefault(p.AssignmentId, ""),
            AssignmentTitle = assignmentTitles.GetValueOrDefault(p.AssignmentId, ""),
            CreatedAt       = p.CreatedAt,
        }).ToList();
    }

    public async Task<IReadOnlyList<SessionSubmissionResultDto>> GetSessionResultsAsync(
        Guid sessionId, string? gradingRound, CancellationToken ct = default)
    {
        _ = await unitOfWork.ExamSessions.GetByIdAsync(sessionId)
            ?? throw new NotFoundException($"ExamSession '{sessionId}' not found.");

        var assignments = (await unitOfWork.Assignments.FindAsync(a => a.ExamSessionId == sessionId)).ToList();
        var assignmentIds = assignments.Select(a => a.Id).ToHashSet();
        var assignmentCodeMap = assignments.ToDictionary(a => a.Id, a => a.Code);

        var participants = (await unitOfWork.Participants.FindAsync(p => p.ExamSessionId == sessionId)).ToList();
        var participantByStudentCode = participants.ToDictionary(p => p.StudentCode, StringComparer.OrdinalIgnoreCase);
        var usernameByStudentCode    = participants.ToDictionary(p => p.StudentCode, p => p.Username, StringComparer.OrdinalIgnoreCase);

        var submissionsQuery = await unitOfWork.Submissions.FindAsync(s => assignmentIds.Contains(s.AssignmentId));
        var submissions = (gradingRound != null
            ? submissionsQuery.Where(s => s.GradingRound == gradingRound)
            : submissionsQuery).ToList();

        var submissionIds = submissions.Select(s => s.Id).ToHashSet();

        // Latest Done grading job per submission
        var allJobs = (await unitOfWork.GradingJobs.FindAsync(
            j => submissionIds.Contains(j.SubmissionId) && j.Status == JobStatus.Done)).ToList();
        var latestJobBySubmission = allJobs
            .GroupBy(j => j.SubmissionId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(j => j.FinishedAt).First().Id);
        var latestJobIds = latestJobBySubmission.Values.ToHashSet();

        var allResults = (await unitOfWork.QuestionResults.FindAsync(r =>
            submissionIds.Contains(r.SubmissionId)
            && (r.GradingJobId == null || latestJobIds.Contains(r.GradingJobId.Value)))).ToList();

        var allNotes = (await unitOfWork.ReviewNotes.FindAsync(n =>
            submissionIds.Contains(n.SubmissionId))).ToList();

        // Questions per assignment (cached)
        var questionsByAssignment = new Dictionary<Guid, List<Question>>();
        foreach (var aId in assignmentIds)
            questionsByAssignment[aId] = (await unitOfWork.Questions.FindAsync(q => q.AssignmentId == aId))
                                          .OrderBy(q => q.CreatedAt).ToList();

        var dtos = new List<SessionSubmissionResultDto>(submissions.Count);

        foreach (var sub in submissions.OrderBy(s => s.StudentCode))
        {
            latestJobBySubmission.TryGetValue(sub.Id, out var latestJobId);
            var subResults = allResults
                .Where(r => r.SubmissionId == sub.Id
                    && (latestJobId == Guid.Empty ? r.GradingJobId == null : r.GradingJobId == latestJobId))
                .ToList();

            var questions = questionsByAssignment.GetValueOrDefault(sub.AssignmentId, []);

            var qDtos = questions.Select(q =>
            {
                var qr = subResults.FirstOrDefault(r => r.QuestionId == q.Id);
                return new QuestionSummaryResult
                {
                    QuestionId    = q.Id,
                    QuestionTitle = q.Title,
                    Score         = qr?.Score ?? 0,
                    FinalScore    = qr?.FinalScore ?? 0,
                    MaxScore      = q.MaxScore,
                    AdjustedScore = qr?.AdjustedScore,
                    AdjustReason  = qr?.AdjustReason,
                    TestCaseResults = qr?.Detail is { Length: > 0 }
                        ? JsonSerializer.Deserialize<List<TestCaseResult>>(qr.Detail, _jsonOpts)
                        : null,
                };
            }).ToList();

            dtos.Add(new SessionSubmissionResultDto
            {
                SubmissionId   = sub.Id,
                Username       = usernameByStudentCode.GetValueOrDefault(sub.StudentCode, ""),
                StudentCode    = sub.StudentCode,
                AssignmentCode = assignmentCodeMap.GetValueOrDefault(sub.AssignmentId, ""),
                GradingRound   = sub.GradingRound,
                Status         = sub.Status,
                HasArtifact    = sub.HasArtifact,
                TotalScore     = qDtos.Sum(q => q.FinalScore),
                MaxScore       = qDtos.Sum(q => q.MaxScore),
                Questions      = qDtos,
                Notes          = allNotes.FirstOrDefault(n => n.SubmissionId == sub.Id)?.Content,
            });
        }

        return dtos;
    }

    private static readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web);

    private static ExamSessionDto MapSummary(ExamSession e) => new()
    {
        Id          = e.Id,
        Title       = e.Title,
        Description = e.Description,
        CreatedAt   = e.CreatedAt,
    };

    [GeneratedRegex(@"[a-z]{2,3}\d{6}$", RegexOptions.IgnoreCase)]
    public static partial Regex StudentCodeSuffixRegex();

    /// <summary>Parses "hoalvpse181951" → studentCode="pse181951", username=fullName.</summary>
    public static (string username, string studentCode) ParseFolderName(string folderName)
    {
        var lower = folderName.ToLowerInvariant();
        var m = StudentCodeSuffixRegex().Match(lower);
        return m.Success
            ? (lower, m.Value)
            : (lower, lower);
    }
}
