using GradingSystem.Application.DTOs;
using GradingSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GradingSystem.Api.Controllers;

public class ExamSessionsController(
    IExamSessionService examSessionService,
    IExportService exportService) : BaseApiController
{
    [HttpPost("exam-sessions")]
    public async Task<IActionResult> CreateAsync([FromBody] CreateExamSessionRequest req, CancellationToken ct)
    {
        var created = await examSessionService.CreateAsync(req, ct);
        return Ok(created, "Exam session created.");
    }

    [HttpGet("exam-sessions")]
    public async Task<IActionResult> GetAllAsync(CancellationToken ct)
    {
        var sessions = await examSessionService.GetAllAsync(ct);
        return Ok(sessions);
    }

    [HttpGet("exam-sessions/{id:guid}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var session = await examSessionService.GetByIdAsync(id, ct);
        return session is null ? NotFound($"ExamSession '{id}' not found.") : Ok(session);
    }

    [HttpDelete("exam-sessions/{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var deleted = await examSessionService.DeleteAsync(id, ct);
        return Ok(deleted, "Exam session deleted.");
    }

    [HttpGet("exam-sessions/{id:guid}/participants")]
    public async Task<IActionResult> GetParticipantsAsync(Guid id, CancellationToken ct)
    {
        var participants = await examSessionService.GetParticipantsAsync(id, ct);
        return Ok(participants);
    }

    [HttpGet("exam-sessions/{id:guid}/results")]
    public async Task<IActionResult> GetResultsAsync(
        Guid id,
        [FromQuery] string? gradingRound,
        CancellationToken ct)
    {
        var results = await examSessionService.GetSessionResultsAsync(id, gradingRound, ct);
        return Ok(results);
    }

    [HttpPost("exam-sessions/{id:guid}/exports")]
    public async Task<IActionResult> CreateExportAsync(
        Guid id,
        [FromBody] CreateSessionExportRequest req,
        CancellationToken ct)
    {
        var job = await exportService.CreateSessionExportAsync(id, req.GradingRound, ct);
        return Ok(job, "Session export job created.");
    }

}
