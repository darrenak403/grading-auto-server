using GradingSystem.Application.DTOs;
using GradingSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GradingSystem.Api.Controllers;

public class QuestionResultsController(IQuestionResultService service) : BaseApiController
{
    [HttpGet("question-results/{id:guid}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var dto = await service.GetByIdAsync(id, ct);
        return dto is null ? NotFound($"QuestionResult '{id}' not found.") : Ok(dto);
    }

    [HttpGet("submissions/{submissionId:guid}/question-results")]
    public async Task<IActionResult> GetBySubmissionAsync(Guid submissionId, CancellationToken ct)
        => Ok(await service.GetBySubmissionIdAsync(submissionId, ct));

    [HttpPut("question-results/{id:guid}/adjust")]
    public async Task<IActionResult> AdjustAsync(Guid id, [FromBody] AdjustScoreRequest req, CancellationToken ct)
        => Ok(await service.AdjustAsync(id, req, ct));

    [HttpDelete("question-results/{id:guid}/adjust")]
    public async Task<IActionResult> ClearAdjustAsync(Guid id, CancellationToken ct)
        => Ok(await service.ClearAdjustAsync(id, ct));
}
