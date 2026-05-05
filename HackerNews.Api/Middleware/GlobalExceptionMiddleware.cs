using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace HackerNews.Api.Middleware;

/// <summary>
/// Catches unhandled exceptions globally and returns a consistent ProblemDetails response.
/// Prevents stack traces from leaking to callers in non-Development environments.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected — log at Debug, not Error
            _logger.LogDebug("Request was cancelled by the client");
            context.Response.StatusCode = 499; // Client Closed Request (nginx convention)
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Title = "An unexpected error occurred",
                Status = (int)HttpStatusCode.InternalServerError,
                Detail = _environment.IsDevelopment() ? ex.Message : null
            };

            var json = JsonSerializer.Serialize(problem);
            await context.Response.WriteAsync(json);
        }
    }
}