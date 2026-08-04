using GradingSystem.Application.DTOs;
using GradingSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GradingSystem.Api.Controllers;

public class LabTestCasesController(ILabTestCaseService service) : BaseApiController
{
    [HttpGet("lab-testcases/{id:guid}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var result = await service.GetByIdAsync(id, ct);
        return result is null ? NotFound($"LabTestCase '{id}' not found.") : Ok(result);
    }

    [HttpPut("lab-testcases/{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateLabTestCaseRequest req, CancellationToken ct)
    {
        var result = await service.UpdateAsync(id, req, ct);
        return Ok(result, "Test case updated.");
    }

    [HttpDelete("lab-testcases/{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPatch("lab-testcases/{id:guid}/status")]
    public async Task<IActionResult> SetStatusAsync(Guid id, [FromBody] SetLabTestCaseStatusRequest req, CancellationToken ct)
    {
        var result = await service.SetStatusAsync(id, req.Status, ct);
        return Ok(result, $"Test case status set to '{req.Status}'.");
    }

    [HttpPost("lab-assignments/{id:guid}/testcases/batch")]
    public async Task<IActionResult> BulkCreateAsync(
        Guid id,
        [FromBody] IEnumerable<CreateLabTestCaseRequest> reqs,
        CancellationToken ct)
    {
        var results = await service.BulkCreateAsync(id, reqs, ct);
        var list = results.ToList();
        return Ok(list, $"{list.Count} test case(s) created.");
    }
}
