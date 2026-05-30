using GradingSystem.Application.DTOs;
using GradingSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using SharpCompress.Archives;

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

    [HttpPost("lab-assignments/{assignmentId:guid}/bulk-upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(500L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 500L * 1024 * 1024)]
    public async Task<IActionResult> BulkUploadZipAsync(Guid assignmentId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Master zip/rar file is required.");

        var uploads = new List<LabUploadFile>();
        using var archive = ArchiveFactory.OpenArchive(file.OpenReadStream());
        
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory && e.Key != null && (e.Key.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || e.Key.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))))
        {
            var ms = new MemoryStream();
            using (var entryStream = entry.OpenEntryStream())
            {
                await entryStream.CopyToAsync(ms, ct);
            }
            ms.Position = 0;
            // The file name might contain paths if it's nested; take just the name
            var safeName = Path.GetFileName(entry.Key) ?? "unknown.zip";
            uploads.Add(new LabUploadFile(safeName, ms));
        }

        var result = await submissionService.BatchUploadAsync(assignmentId, uploads, ct);
        return Ok(result, $"Bulk upload complete: {result.Created.Count} submission(s) extracted and uploaded.");
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

    [HttpPost("lab-submissions/{id:guid}/regrade")]
    public async Task<IActionResult> RegradeAsync(Guid id, CancellationToken ct)
    {
        var queued = await submissionService.RegradeAsync(id, ct);
        return Ok(new
        {
            Queued = queued,
            Message = queued
                ? "Regrade job queued. Worker will process it sequentially."
                : "Submission already has a pending regrade job."
        });
    }

    [HttpPost("lab-submissions/regrade-all")]
    public async Task<IActionResult> RegradeAllAsync([FromQuery] Guid assignmentId, CancellationToken ct)
    {
        var count = await submissionService.RegradeAllAsync(assignmentId, ct);
        return Ok(new { Queued = count }, $"{count} submission(s) queued for regrading.");
    }

    [HttpPut("lab-submissions/{id:guid}/adjust")]
    public async Task<IActionResult> AdjustScoreAsync(Guid id, [FromBody] AdjustLabScoreRequest req, CancellationToken ct)
    {
        var result = await resultService.AdjustScoreAsync(id, req.ResultId, req.Score, req.Reason, ct);
        return Ok(result, "Score adjusted.");
    }

    [HttpDelete("lab-submissions/{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        await submissionService.DeleteAsync(id, ct);
        return Ok(new { }, "Submission deleted.");
    }

    [HttpDelete("lab-submissions")]
    public async Task<IActionResult> DeleteAllAsync([FromQuery] Guid assignmentId, CancellationToken ct)
    {
        var count = await submissionService.DeleteAllByAssignmentAsync(assignmentId, ct);
        return Ok(new { Deleted = count }, $"{count} submission(s) deleted.");
    }
}
