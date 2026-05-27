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

    [HttpPatch("lab-testcases/{id:guid}/approve")]
    public async Task<IActionResult> ApproveAsync(Guid id, CancellationToken ct)
    {
        var result = await service.ApproveAsync(id, ct);
        return Ok(result, "Test case approved.");
    }

    [HttpPatch("lab-testcases/{id:guid}/reject")]
    public async Task<IActionResult> RejectAsync(Guid id, CancellationToken ct)
    {
        var result = await service.RejectAsync(id, ct);
        return Ok(result, "Test case rejected.");
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
