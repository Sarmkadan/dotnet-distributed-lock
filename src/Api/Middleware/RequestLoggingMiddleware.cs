// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Api.Middleware;

/// <summary>
/// Middleware that logs all incoming HTTP requests and outgoing responses.
/// Tracks request duration, status codes, and provides detailed diagnostics for troubleshooting.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = context.TraceIdentifier;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Log incoming request
            _logger.LogInformation(
                "Request started: {RequestId} {Method} {Path}",
                requestId,
                context.Request.Method,
                context.Request.Path);

            await _next(context);

            stopwatch.Stop();

            // Log response with timing information
            _logger.LogInformation(
                "Request completed: {RequestId} {Method} {Path} {StatusCode} {DurationMs}ms",
                requestId,
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Request failed: {RequestId} {Method} {Path} {DurationMs}ms",
                requestId,
                context.Request.Method,
                context.Request.Path,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}
