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
    public async Task<IActionResult> GetParticipantsAsync(
        Guid id, [FromQuery] Guid? assignmentId, CancellationToken ct)
    {
        var participants = await examSessionService.GetParticipantsAsync(id, assignmentId, ct);
        return Ok(participants);
    }

    [HttpPost("exam-sessions/{id:guid}/participants/import")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportParticipantsByCodeAsync(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("CSV file is required.");

        await using var stream = file.OpenReadStream();
        var result = await examSessionService.ImportParticipantsByCodeAsync(id, stream, ct);
        return Ok(result, $"Imported {result.Created} created, {result.Updated} updated, {result.Skipped} skipped.");
    }

    [HttpGet("exam-sessions/{id:guid}/rounds")]
    public async Task<IActionResult> GetRoundsAsync(
        Guid id, [FromQuery] Guid? assignmentId, CancellationToken ct)
    {
        var rounds = await examSessionService.GetRoundsAsync(id, assignmentId, ct);
        return Ok(rounds);
    }

    [HttpGet("exam-sessions/{id:guid}/results")]
    public async Task<IActionResult> GetResultsAsync(
        Guid id,
        [FromQuery] string? gradingRound,
        [FromQuery] Guid? assignmentId,
        CancellationToken ct)
    {
        var results = await examSessionService.GetSessionResultsAsync(id, gradingRound, assignmentId, ct);
        return Ok(results);
    }

    [HttpPost("exam-sessions/{id:guid}/exports")]
    public async Task<IActionResult> CreateExportAsync(
        Guid id,
        [FromBody] CreateSessionExportRequest req,
        CancellationToken ct)
    {
        var job = await exportService.CreateSessionExportAsync(id, req.GradingRound, req.AssignmentId, ct);
        return Ok(job, "Session export job created.");
    }

}
