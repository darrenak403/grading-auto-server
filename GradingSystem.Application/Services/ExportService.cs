using GradingSystem.Application.DTOs;
using GradingSystem.Application.Exceptions;
using GradingSystem.Application.Interfaces;
using GradingSystem.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace GradingSystem.Application.Services;

public class ExportService(IUnitOfWork uow, IConfiguration config) : IExportService
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

        return Map(job, assignmentCode: assignment.Code, examSessionTitle: null, labAssignmentTitle: null);
    }

    public async Task<ExportJobDto> CreateSessionExportAsync(
        Guid sessionId, string? gradingRound, Guid? assignmentId = null, CancellationToken ct = default)
    {
        var session = await uow.ExamSessions.GetByIdAsync(sessionId)
            ?? throw new NotFoundException($"ExamSession '{sessionId}' not found.");

        string? assignmentCode = null;
        if (assignmentId.HasValue)
        {
            var assignment = await uow.Assignments.GetByIdAsync(assignmentId.Value);
            if (assignment is null || assignment.ExamSessionId != sessionId)
                throw new NotFoundException($"Assignment '{assignmentId}' not found in session '{sessionId}'.");
            assignmentCode = assignment.Code;
        }

        var job = new ExportJob
        {
            ExamSessionId = sessionId,
            AssignmentId  = assignmentId,
            GradingRound  = gradingRound?.Trim(),
            Status        = ExportStatus.Pending,
        };

        await uow.ExportJobs.AddAsync(job);
        await uow.SaveChangesAsync(ct);

        return Map(job, assignmentCode, examSessionTitle: session.Title, labAssignmentTitle: null);
    }

    public async Task<ExportJobDto> CreateLabExportAsync(Guid labAssignmentId, CancellationToken ct = default)
    {
        var assignment = await uow.LabAssignments.GetByIdAsync(labAssignmentId)
            ?? throw new NotFoundException($"LabAssignment '{labAssignmentId}' not found.");

        var job = new ExportJob
        {
            LabAssignmentId = labAssignmentId,
            Status          = ExportStatus.Pending,
        };

        await uow.ExportJobs.AddAsync(job);
        await uow.SaveChangesAsync(ct);

        return Map(job, assignmentCode: null, examSessionTitle: null, labAssignmentTitle: assignment.Title);
    }

    public async Task<ExportJobDto?> GetByIdAsync(Guid exportJobId, CancellationToken ct = default)
    {
        var job = await uow.ExportJobs.GetByIdAsync(exportJobId);
        if (job is null) return null;

        string? assignmentCode = null;
        string? sessionTitle = null;
        string? labAssignmentTitle = null;

        if (job.ExamSessionId.HasValue)
        {
            var s = await uow.ExamSessions.GetByIdAsync(job.ExamSessionId.Value);
            sessionTitle = s?.Title;
            if (job.AssignmentId.HasValue)
            {
                var a = await uow.Assignments.GetByIdAsync(job.AssignmentId.Value);
                assignmentCode = a?.Code;
            }
        }
        else if (job.AssignmentId.HasValue)
        {
            var a = await uow.Assignments.GetByIdAsync(job.AssignmentId.Value);
            assignmentCode = a?.Code;
        }
        else if (job.LabAssignmentId.HasValue)
        {
            var la = await uow.LabAssignments.GetByIdAsync(job.LabAssignmentId.Value);
            labAssignmentTitle = la?.Title;
        }

        return Map(job, assignmentCode, sessionTitle, labAssignmentTitle);
    }

    public async Task<string?> GetFilePathAsync(Guid exportJobId, CancellationToken ct = default)
    {
        var job = await uow.ExportJobs.GetByIdAsync(exportJobId);
        if (job?.Status != ExportStatus.Done || string.IsNullOrWhiteSpace(job.FilePath))
            return null;

        var resolved = ResolveExportFilePath(job.FilePath);
        return File.Exists(resolved) ? resolved : null;
    }

    private string ResolveExportFilePath(string storedPath)
    {
        if (Path.IsPathRooted(storedPath) && File.Exists(storedPath))
            return storedPath;

        var basePath = config["Storage:BasePath"];
        if (string.IsNullOrWhiteSpace(basePath))
            return Path.GetFullPath(storedPath);

        if (!Path.IsPathRooted(basePath))
            basePath = Path.GetFullPath(basePath);

        return Path.Combine(basePath, "exports", Path.GetFileName(storedPath));
    }

    private static ExportJobDto Map(
        ExportJob e,
        string? assignmentCode,
        string? examSessionTitle,
        string? labAssignmentTitle) => new()
    {
        Id                 = e.Id,
        AssignmentId       = e.AssignmentId,
        AssignmentCode     = assignmentCode,
        ExamSessionId      = e.ExamSessionId,
        ExamSessionTitle   = examSessionTitle,
        LabAssignmentId    = e.LabAssignmentId,
        LabAssignmentTitle = labAssignmentTitle,
        Status             = e.Status,
        GradingRound       = e.GradingRound,
        FilePath           = e.FilePath,
        ErrorMessage       = e.ErrorMessage,
    };
}
