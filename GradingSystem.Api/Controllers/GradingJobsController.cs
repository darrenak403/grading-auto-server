using GradingSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GradingSystem.Api.Controllers;

public class GradingJobsController(IGradingJobService service) : BaseApiController
{
    [HttpGet("grading-jobs/{id:guid}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var dto = await service.GetByIdAsync(id, ct);
        return dto is null ? NotFound($"GradingJob '{id}' not found.") : Ok(dto);
    }

    [HttpGet("submissions/{submissionId:guid}/grading-jobs")]
    public async Task<IActionResult> GetBySubmissionAsync(Guid submissionId, CancellationToken ct)
        => Ok(await service.GetBySubmissionIdAsync(submissionId, ct));
}
