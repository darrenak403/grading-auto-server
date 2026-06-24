using System.Text.Json;
using GradingSystem.Application.Common;
using GradingSystem.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace GradingSystem.Api.Middleware;

public class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger,
    IHostEnvironment environment)
{
    private static readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception {TraceId}", context.TraceIdentifier);
            await WriteErrorAsync(context, ex, environment.IsDevelopment());
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, Exception ex, bool includeDetails)
    {
        var (status, message) = ex switch
        {
            NotFoundException n       => (StatusCodes.Status404NotFound, n.Message),
            BadRequestException b     => (StatusCodes.Status400BadRequest, b.Message),
            ConflictException c       => (StatusCodes.Status409Conflict, c.Message),
            DbUpdateException { InnerException: PostgresException { SqlState: "23505" } pg }
                                      => (StatusCodes.Status409Conflict, $"A record with the same unique value already exists. ({pg.ConstraintName})"),
            ArgumentException or ArgumentNullException => (StatusCodes.Status400BadRequest, ex.Message),
            KeyNotFoundException      => (StatusCodes.Status404NotFound, ex.Message),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, ex.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
        };

        if (context.Response.HasStarted)
            throw ex;

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";

        var body = includeDetails
            ? ApiResponse.Fail(
                message,
                BuildErrors(ex),
                context.TraceIdentifier)
            : ApiResponse.Fail(message, traceId: context.TraceIdentifier);
        await context.Response.WriteAsync(JsonSerializer.Serialize(body, _jsonOpts));
    }

    private static IEnumerable<string> BuildErrors(Exception ex)
    {
        var errors = new List<string> { $"{ex.GetType().Name}: {ex.Message}" };
        if (!string.IsNullOrWhiteSpace(ex.InnerException?.Message))
            errors.Add($"Inner: {ex.InnerException.Message}");
        return errors;
    }
}
