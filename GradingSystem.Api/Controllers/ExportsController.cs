using GradingSystem.Application.DTOs;
using GradingSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GradingSystem.Api.Controllers;

public class ExportsController(IExportService svc) : BaseApiController
{
    [HttpPost("exports")]
    public async Task<IActionResult> CreateAsync([FromBody] CreateExportRequest req, CancellationToken ct)
    {
        var job = await svc.CreateAsync(req, ct);
        return Ok(job, "Export job created.");
    }

    [HttpGet("exports/{id:guid}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var job = await svc.GetByIdAsync(id, ct);
        return job is null ? NotFound($"Export job '{id}' not found.") : Ok(job);
    }

    [HttpGet("exports/{id:guid}/download")]
    public async Task<IActionResult> DownloadAsync(Guid id, CancellationToken ct)
    {
        var path = await svc.GetFilePathAsync(id, ct);
        if (path is null)
            return NotFound("Export not ready or not found.");

        return PhysicalFile(
            path,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            Path.GetFileName(path));
    }
}
