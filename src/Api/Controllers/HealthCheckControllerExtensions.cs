#nullable enable

namespace SarmKadan.DistributedLock.Api.Controllers;

using Microsoft.AspNetCore.Mvc;

using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Extension methods for <see cref="HealthCheckController"/> providing additional health monitoring capabilities.
/// </summary>
public static class HealthCheckControllerExtensions
{
    /// <summary>
    /// Creates a standardized health check response with additional metadata.
    /// </summary>
    /// <param name="controller">The health check controller instance.</param>
    /// <param name="status">The health status to report.</param>
    /// <param name="responseTimeMs">The response time in milliseconds.</param>
    /// <returns>An ActionResult containing the health check response.</returns>
    public static ActionResult<HealthCheckResponse> CreateHealthResponse(
        this HealthCheckController controller,
        string status,
        long responseTimeMs = 0)
    {
        ArgumentNullException.ThrowIfNull(controller);

        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        return new HealthCheckResponse
        {
            Status = status,
            Timestamp = DateTime.UtcNow,
            Version = controller.Version,
            Details = new HealthDetails
            {
                BackendConnected = controller.BackendConnected,
                ErrorMessage = controller.ErrorMessage
            },
            ResponseTimeMs = responseTimeMs
        };
    }

    /// <summary>
    /// Creates a detailed health response with runtime information.
    /// </summary>
    /// <param name="controller">The health check controller instance.</param>
    /// <param name="status">The health status to report.</param>
    /// <param name="responseTimeMs">The response time in milliseconds.</param>
    /// <returns>An ActionResult containing the detailed health response.</returns>
    public static ActionResult<DetailedHealthResponse> CreateDetailedResponse(
        this HealthCheckController controller,
        string status,
        long responseTimeMs = 0)
    {
        ArgumentNullException.ThrowIfNull(controller);

        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        return new DetailedHealthResponse
        {
            Status = status,
            Timestamp = DateTime.UtcNow,
            Version = controller.Version,
            ResponseTimeMs = responseTimeMs,
            Runtime = new RuntimeInfo
            {
                Framework = controller.Runtime?.Framework ?? ".NET 10.0",
                Uptime = controller.Runtime?.Uptime ?? TimeSpan.FromMilliseconds(Environment.TickCount64)
            }
        };
    }

    /// <summary>
    /// Determines if the health check indicates a healthy state.
    /// </summary>
    /// <param name="controller">The health check controller instance.</param>
    /// <returns>True if the status is 'healthy' or 'ready'; otherwise false.</returns>
    public static bool IsHealthy(this HealthCheckController controller)
    {
        ArgumentNullException.ThrowIfNull(controller);

        return controller.Status?.Equals("healthy", StringComparison.OrdinalIgnoreCase) == true ||
               controller.Status?.Equals("ready", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Determines if the health check indicates a degraded or unhealthy state.
    /// </summary>
    /// <param name="controller">The health check controller instance.</param>
    /// <returns>True if the status indicates degraded/unhealthy; otherwise false.</returns>
    public static bool IsUnhealthy(this HealthCheckController controller)
    {
        ArgumentNullException.ThrowIfNull(controller);

        return !controller.IsHealthy();
    }
}