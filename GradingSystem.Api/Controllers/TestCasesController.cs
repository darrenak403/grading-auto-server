using GradingSystem.Application.Common;
using GradingSystem.Application.DTOs;
using GradingSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GradingSystem.Api.Controllers;

public class TestCasesController(ITestCaseService testCaseService) : BaseApiController
{
    [HttpPost("questions/{questionId:guid}/test-cases")]
    public async Task<IActionResult> CreateAsync(
        Guid questionId,
        [FromBody] List<CreateTestCaseRequest> reqs,
        CancellationToken ct)
    {
        if (reqs is null || reqs.Count == 0)
        {
            return BadRequest("At least one test case is required.");
        }

        var created = await testCaseService.CreateManyAsync(questionId, reqs, ct);
        return Ok(created, "Test cases created.");
    }

    [HttpGet("questions/{questionId:guid}/test-cases")]
    public async Task<IActionResult> GetByQuestionIdAsync(Guid questionId, CancellationToken ct)
    {
        var testCases = await testCaseService.GetByQuestionIdAsync(questionId, ct);
        return Ok(testCases);
    }

    [HttpDelete("questions/{questionId:guid}/test-cases")]
    public async Task<IActionResult> DeleteByQuestionIdAsync(Guid questionId, CancellationToken ct)
    {
        var count = await testCaseService.DeleteByQuestionIdAsync(questionId, ct);
        return Ok(ApiResponse.Success(new { deleted = count }, $"Deleted {count} test case(s)."));
    }

    [HttpDelete("test-cases/{testCaseId:guid}")]
    public async Task<IActionResult> DeleteByIdAsync(Guid testCaseId, CancellationToken ct)
    {
        var deleted = await testCaseService.DeleteByIdAsync(testCaseId, ct);
        return Ok(deleted, "Test case deleted.");
    }

    [HttpPut("test-cases/{testCaseId:guid}")]
    public async Task<IActionResult> UpdateAsync(
        Guid testCaseId,
        [FromBody] CreateTestCaseRequest req,
        CancellationToken ct)
    {
        var updated = await testCaseService.UpdateAsync(testCaseId, req, ct);
        return Ok(updated, "Test case updated.");
    }
}
