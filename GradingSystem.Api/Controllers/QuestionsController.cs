using GradingSystem.Application.DTOs;
using GradingSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GradingSystem.Api.Controllers;

public class QuestionsController(IQuestionService questionService) : BaseApiController
{
    [HttpPost("assignments/{assignmentId:guid}/questions")]
    public async Task<IActionResult> CreateAsync(
        Guid assignmentId,
        [FromBody] List<CreateQuestionRequest> reqs,
        CancellationToken ct)
    {
        if (reqs is null || reqs.Count == 0)
        {
            return BadRequest("At least one question is required.");
        }

        var created = await questionService.CreateManyAsync(assignmentId, reqs, ct);
        return Ok(created, "Questions created.");
    }

    [HttpGet("assignments/{assignmentId:guid}/questions")]
    public async Task<IActionResult> GetByAssignmentIdAsync(Guid assignmentId, CancellationToken ct)
    {
        var questions = await questionService.GetByAssignmentIdAsync(assignmentId, ct);
        return Ok(questions);
    }

    [HttpDelete("questions/{questionId:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid questionId, CancellationToken ct)
    {
        var deleted = await questionService.DeleteAsync(questionId, ct);
        return Ok(deleted, "Question deleted.");
    }
}
