using GradingSystem.Application.DTOs;
using GradingSystem.Application.Exceptions;
using GradingSystem.Application.Interfaces;
using GradingSystem.Domain.Entities;

namespace GradingSystem.Application.Services;

public class ExportService(IUnitOfWork uow) : IExportService
{
    public async Task<ExportJobDto> CreateAsync(CreateExportRequest req, CancellationToken ct = default)
    {
        var assignment = await uow.Assignments.GetByIdAsync(req.AssignmentId)
            ?? throw new NotFoundException($"Assignment '{req.AssignmentId}' not found.");

        var job = new ExportJob
        {
            AssignmentId = assignment.Id,
            GradingRound = req.GradingRound?.Trim(),
            Status       = ExportStatus.Pending,
        };

        await uow.ExportJobs.AddAsync(job);
        await uow.SaveChangesAsync(ct);

        return Map(job, assignment.Code, null, null);
    }

    public async Task<ExportJobDto> CreateSessionExportAsync(
        Guid sessionId, string? gradingRound, CancellationToken ct = default)
    {
        var session = await uow.ExamSessions.GetByIdAsync(sessionId)
            ?? throw new NotFoundException($"ExamSession '{sessionId}' not found.");

        var job = new ExportJob
        {
            ExamSessionId = sessionId,
            GradingRound  = gradingRound?.Trim(),
            Status        = ExportStatus.Pending,
        };

        await uow.ExportJobs.AddAsync(job);
        await uow.SaveChangesAsync(ct);

        return Map(job, null, sessionId, session.Title);
    }

    public async Task<ExportJobDto?> GetByIdAsync(Guid exportJobId, CancellationToken ct = default)
    {
        var job = await uow.ExportJobs.GetByIdAsync(exportJobId);
        if (job is null) return null;

        string? assignmentCode = null;
        string? sessionTitle = null;

        if (job.AssignmentId.HasValue)
        {
            var a = await uow.Assignments.GetByIdAsync(job.AssignmentId.Value);
            assignmentCode = a?.Code;
        }
        else if (job.ExamSessionId.HasValue)
        {
            var s = await uow.ExamSessions.GetByIdAsync(job.ExamSessionId.Value);
            sessionTitle = s?.Title;
        }

        return Map(job, assignmentCode, job.ExamSessionId, sessionTitle);
    }

    public async Task<string?> GetFilePathAsync(Guid exportJobId, CancellationToken ct = default)
    {
        var job = await uow.ExportJobs.GetByIdAsync(exportJobId);
        return job?.Status == ExportStatus.Done ? job.FilePath : null;
    }

    private static ExportJobDto Map(ExportJob e, string? assignmentCode, Guid? examSessionId, string? sessionTitle) => new()
    {
        Id               = e.Id,
        AssignmentId     = e.AssignmentId,
        AssignmentCode   = assignmentCode,
        ExamSessionId    = examSessionId,
        ExamSessionTitle = sessionTitle,
        Status           = e.Status,
        GradingRound     = e.GradingRound,
        FilePath         = e.FilePath,
        ErrorMessage     = e.ErrorMessage,
    };
}
