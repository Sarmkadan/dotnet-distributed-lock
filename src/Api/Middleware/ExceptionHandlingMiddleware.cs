// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Api.Middleware;

using System.Text.Json;
using SarmKadan.DistributedLock.Core.Exceptions;

/// <summary>
/// Global exception handling middleware that catches all unhandled exceptions
/// and converts them to appropriate HTTP responses with meaningful error messages.
/// This prevents sensitive stack traces from being exposed to clients.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred during request processing");
            await HandleExceptionAsync(context, ex);
        }
    }

    /// <summary>
    /// Converts exceptions to appropriate HTTP status codes and error responses.
    /// Maps domain exceptions to their corresponding HTTP status codes for client clarity.
    /// </summary>
    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = new ErrorResponseBody();

        switch (exception)
        {
            case LockAcquisitionException:
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                response.Message = exception.Message;
                response.ErrorCode = "LOCK_ACQUISITION_FAILED";
                break;

            case LockNotOwnedException:
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                response.Message = exception.Message;
                response.ErrorCode = "LOCK_NOT_OWNED";
                break;

            case LockExpiredException:
                context.Response.StatusCode = StatusCodes.Status410Gone;
                response.Message = exception.Message;
                response.ErrorCode = "LOCK_EXPIRED";
                break;

            case InvalidFencingTokenException:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                response.Message = exception.Message;
                response.ErrorCode = "INVALID_FENCING_TOKEN";
                break;

            case InvalidOperationException:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                response.Message = exception.Message;
                response.ErrorCode = "INVALID_OPERATION";
                break;

            case ArgumentException:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                response.Message = exception.Message;
                response.ErrorCode = "INVALID_ARGUMENT";
                break;

            default:
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                response.Message = "An unexpected error occurred";
                response.ErrorCode = "INTERNAL_ERROR";
                break;
        }

        response.Timestamp = DateTime.UtcNow;
        return context.Response.WriteAsJsonAsync(response);
    }
}

public class ErrorResponseBody
{
    public string Message { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
