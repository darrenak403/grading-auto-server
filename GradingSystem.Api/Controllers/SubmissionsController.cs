using GradingSystem.Application.DTOs;
using GradingSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GradingSystem.Api.Controllers;

public class SubmissionsController(
    ISubmissionService submissionService,
    IReviewNoteService reviewNoteService) : BaseApiController
{
    [HttpGet("assignments/{assignmentId:guid}/submissions")]
    public async Task<IActionResult> GetByAssignmentAsync(
        Guid assignmentId,
        [FromQuery] string? studentCode,
        [FromQuery] string? gradingRound,
        CancellationToken ct)
    {
        var list = await submissionService.GetByAssignmentIdAsync(assignmentId, studentCode, gradingRound, ct);
        return Ok(list);
    }

    [HttpGet("assignments/{assignmentId:guid}/rounds")]
    public async Task<IActionResult> GetRoundsAsync(Guid assignmentId, CancellationToken ct)
    {
        var rounds = await submissionService.GetRoundsAsync(assignmentId, ct);
        return Ok(rounds);
    }

    [HttpGet("submissions/{id:guid}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var submission = await submissionService.GetByIdAsync(id, ct);
        return submission is null ? NotFound($"Submission '{id}' not found.") : Ok(submission);
    }

    [HttpGet("submissions/{id:guid}/results")]
    public async Task<IActionResult> GetResultsAsync(Guid id, CancellationToken ct)
    {
        var results = await submissionService.GetResultsAsync(id, ct);
        return Ok(results);
    }

    [HttpPut("submissions/{id:guid}/custom-result")]
    public async Task<IActionResult> ImportCustomResultAsync(
        Guid id,
        [FromBody] ImportCustomResultRequest req,
        CancellationToken ct)
    {
        var results = await submissionService.ImportCustomResultAsync(id, req, ct);
        return Ok(results, "Custom result imported.");
    }

    [HttpPut("submissions/{id:guid}/notes")]
    public async Task<IActionResult> UpsertNoteAsync(Guid id, [FromBody] UpdateReviewNoteRequest req, CancellationToken ct)
    {
        var note = await reviewNoteService.UpsertAsync(id, req, ct);
        return Ok(note, "Review note upserted.");
    }

    [HttpDelete("submissions/{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var deleted = await submissionService.DeleteAsync(id, ct);
        return Ok(deleted, "Submission deleted.");
    }

    [HttpPost("submissions/{id:guid}/grade")]
    public async Task<IActionResult> RegradeAsync(Guid id, CancellationToken ct)
    {
        var job = await submissionService.RegradeAsync(id, ct);
        return Ok(job, "Regrade job queued.");
    }
}
