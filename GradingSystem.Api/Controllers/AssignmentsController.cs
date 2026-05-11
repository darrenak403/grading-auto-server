using GradingSystem.Application.DTOs;
using GradingSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GradingSystem.Api.Controllers;

public class AssignmentsController(IAssignmentService assignmentService, IBulkUploadService bulkUploadService) : BaseApiController
{
    [HttpPost("assignments")]
    public async Task<IActionResult> CreateAsync([FromBody] CreateAssignmentRequest req, CancellationToken ct)
    {
        var created = await assignmentService.CreateAsync(req, ct);
        return Ok(created, "Assignment created.");
    }

    [HttpGet("assignments/{id:guid}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var assignment = await assignmentService.GetByIdAsync(id, ct);
        return assignment is null
            ? NotFound($"Assignment '{id}' not found.")
            : Ok(assignment);
    }

    [HttpGet("assignments")]
    public async Task<IActionResult> GetSummariesAsync(CancellationToken ct)
    {
        var assignments = await assignmentService.GetSummariesAsync(ct);
        return Ok(assignments);
    }

    [HttpPut("assignments/{id:guid}/resources")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpsertResourcesAsync(
        Guid id,
        IFormFile? databaseSql,
        IFormFile? givenZip,
        [FromForm] string? givenApiBaseUrl,
        CancellationToken ct)
    {
        Stream? databaseSqlStream = null;
        Stream? givenZipStream = null;
        try
        {
            (string FileName, Stream Content)? databaseSqlPart = null;
            if (databaseSql is { Length: > 0 })
            {
                databaseSqlStream = databaseSql.OpenReadStream();
                databaseSqlPart = (databaseSql.FileName, databaseSqlStream);
            }

            (string FileName, Stream Content)? givenZipPart = null;
            if (givenZip is { Length: > 0 })
            {
                givenZipStream = givenZip.OpenReadStream();
                givenZipPart = (givenZip.FileName, givenZipStream);
            }

            var updated = await assignmentService.UpsertResourcesAsync(
                id,
                new UpsertAssignmentResourcesRequest
                {
                    DatabaseSql     = databaseSqlPart,
                    GivenApiBaseUrl = givenApiBaseUrl,
                    GivenZip        = givenZipPart,
                },
                ct);

            return Ok(updated, "Assignment resources updated.");
        }
        finally
        {
            databaseSqlStream?.Dispose();
            givenZipStream?.Dispose();
        }
    }

    [HttpDelete("assignments/{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var deleted = await assignmentService.DeleteAsync(id, ct);
        return Ok(deleted, "Assignment deleted.");
    }

    [HttpPost("assignments/{id:guid}/participants/import")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportParticipantsAsync(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("CSV file is required.");

        await using var stream = file.OpenReadStream();
        var result = await assignmentService.ImportParticipantsAsync(id, stream, ct);
        return Ok(result, $"Imported {result.Created} participant(s).");
    }

    [HttpGet("assignments/{id:guid}/participants")]
    public async Task<IActionResult> GetParticipantsAsync(Guid id, CancellationToken ct)
    {
        var participants = await assignmentService.GetParticipantsAsync(id, ct);
        return Ok(participants);
    }

    [HttpPost("assignments/{id:guid}/bulk-upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(200 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 200 * 1024 * 1024)]
    public async Task<IActionResult> BulkUploadAsync(
        Guid id,
        IFormFile file,
        [FromForm] string gradingRound = "Lần 1",
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Master zip file is required.");

        await using var stream = file.OpenReadStream();
        var result = await bulkUploadService.ParseAndCreateAsync(id, gradingRound, stream, ct);
        return Ok(result, $"Bulk upload complete: {result.Created} created, {result.Missing} missing.");
    }

    [HttpPost("assignments/{id:guid}/grade")]
    public async Task<IActionResult> TriggerGradeAsync(
        Guid id,
        [FromQuery] string gradingRound = "Lần 1",
        CancellationToken ct = default)
    {
        var count = await assignmentService.TriggerGradeAsync(id, gradingRound, ct);
        return Ok(count, $"Enqueued {count} grading job(s) for round '{gradingRound}'.");
    }
}
