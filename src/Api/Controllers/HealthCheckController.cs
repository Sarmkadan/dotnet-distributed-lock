#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SarmKadan.DistributedLock.Core.Repository;

/// <summary>
/// Health check endpoints for monitoring the distributed lock system.
/// Used by load balancers and orchestration platforms to verify service health.
/// </summary>
[ApiController]
[Route("api/health")]
[Produces("application/json")]
public sealed class HealthCheckController : ControllerBase
{
    private readonly ILockRepository _repository;
    private readonly ILogger<HealthCheckController> _logger;

    public HealthCheckController(ILockRepository repository, ILogger<HealthCheckController> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Performs a liveness check - indicates whether the service is running.
    /// </summary>
    [HttpGet("live")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<HealthCheckResponse> Liveness()
    {
        return Ok(new HealthCheckResponse
        {
            Status = "healthy",
            Timestamp = DateTime.UtcNow,
            Version = GetAssemblyVersion()
        });
    }

    /// <summary>
    /// Performs a readiness check - indicates whether the service can accept requests.
    /// Verifies connectivity to the lock backend (Redis, PostgreSQL, SQLite, etc).
    /// </summary>
    [HttpGet("ready")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthCheckResponse>> Readiness()
    {
        try
        {
            // Test repository connectivity by attempting a lightweight operation
            var testLock = await _repository.GetLockAsync("__health_check__");

            return Ok(new HealthCheckResponse
            {
                Status = "ready",
                Timestamp = DateTime.UtcNow,
                Version = GetAssemblyVersion(),
                Details = new HealthDetails { BackendConnected = true }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Readiness check failed - backend connectivity issue");

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new HealthCheckResponse
                {
                    Status = "not_ready",
                    Timestamp = DateTime.UtcNow,
                    Version = GetAssemblyVersion(),
                    Details = new HealthDetails
                    {
                        BackendConnected = false,
                        ErrorMessage = ex.Message
                    }
                });
        }
    }

    /// <summary>
    /// Detailed health status including metrics and backend information.
    /// </summary>
    [HttpGet("detailed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<DetailedHealthResponse>> DetailedHealth()
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var backendHealthy = await VerifyBackendConnectivity();
            var responseTime = DateTime.UtcNow.Subtract(startTime);

            return Ok(new DetailedHealthResponse
            {
                Status = backendHealthy ? "healthy" : "degraded",
                Timestamp = DateTime.UtcNow,
                Version = GetAssemblyVersion(),
                ResponseTimeMs = (long)responseTime.TotalMilliseconds,
                Runtime = new RuntimeInfo
                {
                    Framework = ".NET 10.0",
                    Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64)
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Detailed health check failed");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private async Task<bool> VerifyBackendConnectivity()
    {
        try
        {
            await _repository.GetLockAsync("__health_check__");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string GetAssemblyVersion()
    {
        return typeof(HealthCheckController).Assembly
            .GetName()
            .Version?.ToString() ?? "1.0.0";
    }
}

public record HealthCheckResponse
{
    public required string Status { get; init; }
    public DateTime Timestamp { get; init; }
    public string Version { get; init; } = string.Empty;
    public HealthDetails? Details { get; init; }
}

public record HealthDetails
{
    public bool BackendConnected { get; init; }
    public string? ErrorMessage { get; init; }
}

public record DetailedHealthResponse
{
    public required string Status { get; init; }
    public DateTime Timestamp { get; init; }
    public string Version { get; init; } = string.Empty;
    public long ResponseTimeMs { get; init; }
    public RuntimeInfo? Runtime { get; init; }
}

public record RuntimeInfo
{
    public string Framework { get; init; } = string.Empty;
    public TimeSpan Uptime { get; init; }
}
