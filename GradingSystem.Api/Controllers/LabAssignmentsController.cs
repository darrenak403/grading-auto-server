using GradingSystem.Application.DTOs;
using GradingSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GradingSystem.Api.Controllers;

public class LabAssignmentsController(
    ILabAssignmentService service,
    ILabTestCaseService testCaseService,
    IExportService exportService) : BaseApiController
{
    [HttpGet("lab-assignments")]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var result = await service.ListAsync(ct);
        return Ok(result);
    }

    [HttpGet("lab-assignments/{id:guid}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var result = await service.GetByIdAsync(id, ct);
        return result is null ? NotFound($"LabAssignment '{id}' not found.") : Ok(result);
    }

    [HttpPost("lab-assignments")]
    public async Task<IActionResult> CreateAsync([FromBody] CreateLabAssignmentRequest req, CancellationToken ct)
    {
        var result = await service.CreateAsync(req, ct);
        return Ok(result, "Lab assignment created.");
    }

    [HttpPut("lab-assignments/{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateLabAssignmentRequest req, CancellationToken ct)
    {
        var result = await service.UpdateAsync(id, req, ct);
        return Ok(result, "Lab assignment updated.");
    }

    [HttpDelete("lab-assignments/{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("lab-assignments/{id:guid}/testcases")]
    public async Task<IActionResult> GetTestCasesAsync(Guid id, CancellationToken ct)
    {
        var result = await service.GetTestCasesAsync(id, ct);
        return Ok(result);
    }

    [HttpGet("lab-assignments/{id:guid}/roster")]
    public async Task<IActionResult> GetRosterAsync(Guid id, CancellationToken ct)
    {
        var result = await service.GetRosterAsync(id, ct);
        return Ok(result);
    }

    [HttpGet("lab-assignments/{id:guid}/grading-progress")]
    public async Task<IActionResult> GetGradingProgressAsync(Guid id, CancellationToken ct)
    {
        var result = await service.GetGradingProgressAsync(id, ct);
        return Ok(result);
    }

    [HttpPost("lab-assignments/{id:guid}/testcases")]
    public async Task<IActionResult> CreateTestCaseAsync(Guid id, [FromBody] CreateLabTestCaseRequest req, CancellationToken ct)
    {
        var result = await testCaseService.CreateAsync(id, req, ct);
        return Ok(result, "Test case created.");
    }

    [HttpPatch("lab-assignments/{id:guid}/testcases/approve-all")]
    public async Task<IActionResult> ApproveAllTestCasesAsync(Guid id, CancellationToken ct)
    {
        var count = await testCaseService.ApproveAllAsync(id, ct);
        return Ok(new { Approved = count, Message = count == 0
            ? "No draft test cases to approve."
            : $"{count} test case(s) approved." });
    }

    [HttpDelete("lab-assignments/{id:guid}/testcases")]
    public async Task<IActionResult> DeleteAllTestCasesAsync(Guid id, CancellationToken ct)
    {
        var count = await testCaseService.DeleteAllByAssignmentAsync(id, ct);
        return Ok(new { Deleted = count }, $"{count} test case(s) deleted.");
    }

    [HttpPost("lab-assignments/{id:guid}/exports")]
    public async Task<IActionResult> CreateExportAsync(Guid id, CancellationToken ct)
    {
        var job = await exportService.CreateLabExportAsync(id, ct);
        return Ok(job, "Lab export job created.");
    }

    [HttpPost("lab-assignments/{id:guid}/grade")]
    [HttpPost("lab-assignments/{id:guid}/grade-all")]
    public async Task<IActionResult> TriggerGradingAsync(Guid id, CancellationToken ct)
    {
        var count = await service.TriggerGradingAsync(id, ct);
        return Ok(new { JobsCreated = count, Message = count == 0
            ? "No new grading jobs created — all submissions already have active jobs."
            : $"{count} grading job(s) created." });
    }
}
