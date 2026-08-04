using GradingSystem.Application.DTOs;
using GradingSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GradingSystem.Api.Controllers;

public class SemestersController(ISemesterService service) : BaseApiController
{
    [HttpGet("semesters")]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var result = await service.ListAsync(ct);
        return Ok(result);
    }

    [HttpGet("semesters/{id:guid}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var result = await service.GetByIdAsync(id, ct);
        return result is null ? NotFound($"Semester '{id}' not found.") : Ok(result);
    }

    [HttpPost("semesters")]
    public async Task<IActionResult> CreateAsync([FromBody] CreateSemesterRequest req, CancellationToken ct)
    {
        var result = await service.CreateAsync(req, ct);
        return Ok(result, "Semester created.");
    }

    [HttpPut("semesters/{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateSemesterRequest req, CancellationToken ct)
    {
        var result = await service.UpdateAsync(id, req, ct);
        return Ok(result, "Semester updated.");
    }

    [HttpDelete("semesters/{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
