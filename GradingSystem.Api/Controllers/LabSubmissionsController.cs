using GradingSystem.Application.DTOs;
using GradingSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GradingSystem.Api.Controllers;

public class LabSubmissionsController(
    ILabSubmissionService submissionService,
    ILabGradingResultService resultService) : BaseApiController
{
    [HttpGet("lab-submissions")]
    public async Task<IActionResult> ListAsync([FromQuery] Guid? assignmentId, CancellationToken ct)
    {
        var result = await submissionService.ListAsync(assignmentId, ct);
        return Ok(result);
    }

    [HttpPost("lab-submissions")]
    public async Task<IActionResult> BatchUploadAsync([FromQuery] Guid assignmentId, [FromForm] IFormFileCollection files, CancellationToken ct)
    {
        if (files is null || files.Count == 0)
            return BadRequest("No files provided.");
        var uploads = files.Select(f => new LabUploadFile(f.FileName, f.OpenReadStream()));
        var result = await submissionService.BatchUploadAsync(assignmentId, uploads, ct);
        return Ok(result, $"{result.Created.Count} submission(s) uploaded.");
    }

    [HttpGet("lab-submissions/{id:guid}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var result = await submissionService.GetByIdAsync(id, ct);
        return result is null ? NotFound($"LabSubmission '{id}' not found.") : Ok(result);
    }

    [HttpGet("lab-submissions/{id:guid}/results")]
    public async Task<IActionResult> GetResultsAsync(Guid id, CancellationToken ct)
    {
        var result = await resultService.GetResultsBySubmissionAsync(id, ct);
        return result is null ? NotFound($"LabSubmission '{id}' not found.") : Ok(result);
    }

    [HttpPut("lab-submissions/{id:guid}/adjust")]
    public async Task<IActionResult> AdjustScoreAsync(Guid id, [FromBody] AdjustLabScoreRequest req, CancellationToken ct)
    {
        var result = await resultService.AdjustScoreAsync(id, req.ResultId, req.Score, req.Reason, ct);
        return Ok(result, "Score adjusted.");
    }
}
