#nullable enable

namespace SarmKadan.DistributedLock.Api.Controllers;

using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Extension methods for health check responses providing additional functionality for health status evaluation.
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
}