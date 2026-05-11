using Asp.Versioning;
using GradingSystem.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace GradingSystem.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
public abstract class BaseApiController : ControllerBase
{
    protected IActionResult Ok<T>(T data, string message = "Success")
        => base.Ok(ApiResponse.Success(data, message));

    protected IActionResult Created<T>(string routeName, object routeValues, T data, string message = "Created")
        => base.CreatedAtRoute(routeName, routeValues, ApiResponse.Success(data, message));

    protected IActionResult NotFound(string message)
        => base.NotFound(ApiResponse.Fail(message, traceId: HttpContext.TraceIdentifier));

    protected IActionResult BadRequest(string message, IEnumerable<string>? errors = null)
        => base.BadRequest(ApiResponse.Fail(message, errors, HttpContext.TraceIdentifier));

    protected new IActionResult NoContent()
        => base.NoContent();
}
