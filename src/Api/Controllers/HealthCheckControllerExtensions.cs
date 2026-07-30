#nullable enable

namespace SarmKadan.DistributedLock.Api.Controllers;

using System;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Extension methods for health check responses providing additional functionality for health status evaluation
/// and for constructing health response objects.
/// </summary>
public static class HealthCheckControllerExtensions
{
    /// <summary>
    /// Determines if the health check response indicates a healthy state.
    /// </summary>
    /// <param name="response">The health check response to evaluate.</param>
    /// <returns>True if the status is 'healthy' or 'ready'; otherwise false.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="response"/> is null.</exception>
    public static bool IsHealthy(this HealthCheckResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return response.Status.Equals("healthy", StringComparison.OrdinalIgnoreCase) ||
               response.Status.Equals("ready", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines if the health check response indicates a degraded or unhealthy state.
    /// </summary>
    /// <param name="response">The health check response to evaluate.</param>
    /// <returns>True if the status indicates degraded/unhealthy; otherwise false.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="response"/> is null.</exception>
    public static bool IsUnhealthy(this HealthCheckResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return !response.IsHealthy();
    }

    /// <summary>
    /// Determines if the detailed health response indicates a healthy state.
    /// </summary>
    /// <param name="response">The detailed health response to evaluate.</param>
    /// <returns>True if the status is 'healthy' or 'ready'; otherwise false.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="response"/> is null.</exception>
    public static bool IsHealthy(this DetailedHealthResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return response.Status.Equals("healthy", StringComparison.OrdinalIgnoreCase) ||
               response.Status.Equals("ready", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines if the detailed health response indicates a degraded or unhealthy state.
    /// </summary>
    /// <param name="response">The detailed health response to evaluate.</param>
    /// <returns>True if the status indicates degraded/unhealthy; otherwise false.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="response"/> is null.</exception>
    public static bool IsUnhealthy(this DetailedHealthResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return !response.IsHealthy();
    }

    /// <summary>
    /// Creates a <see cref="HealthCheckResponse"/> from a status string and optional details.
    /// </summary>
    /// <param name="status">The health status (e.g., "healthy", "ready", "not_ready").</param>
    /// <param name="details">Optional additional health details.</param>
    /// <returns>A populated <see cref="HealthCheckResponse"/> instance.</returns>
    public static HealthCheckResponse ToHealthCheckResponse(this string status, HealthDetails? details = null)
    {
        return new HealthCheckResponse
        {
            Status = status,
            Timestamp = DateTime.UtcNow,
            Version = GetAssemblyVersion(),
            Details = details
        };
    }

    /// <summary>
    /// Creates a <see cref="DetailedHealthResponse"/> from backend health and response time.
    /// </summary>
    /// <param name="backendHealthy">Indicates whether the backend is healthy.</param>
    /// <param name="responseTimeMs">The elapsed time for the health request in milliseconds.</param>
    /// <returns>A populated <see cref="DetailedHealthResponse"/> instance.</returns>
    public static DetailedHealthResponse ToDetailedHealthResponse(this bool backendHealthy, long responseTimeMs)
    {
        return new DetailedHealthResponse
        {
            Status = backendHealthy ? "healthy" : "degraded",
            Timestamp = DateTime.UtcNow,
            Version = GetAssemblyVersion(),
            ResponseTimeMs = responseTimeMs,
            Runtime = new RuntimeInfo
            {
                Framework = ".NET 10.0",
                Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64)
            }
        };
    }

    // Helper to retrieve the assembly version; kept internal to avoid exposing it publicly.
    private static string GetAssemblyVersion()
    {
        // The controller type resides in the same assembly, so we can use it to obtain the version.
        return typeof(HealthCheckController).Assembly
            .GetName()
            .Version?.ToString() ?? "1.0.0";
    }
}
