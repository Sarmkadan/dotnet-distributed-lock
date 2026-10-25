#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SarmKadan.DistributedLock.Repository;

/// <summary>
/// Health check endpoints for monitoring the distributed lock system.
/// Used by load balancers and orchestration platforms to verify service health.
/// </summary>
[ApiController]
[Route(BaseRoute)]
[Produces(JsonContentType)]
public sealed class HealthCheckController : ControllerBase
{
    private const string BaseRoute = "api/health";
    private const string LivenessRoute = "live";
    private const string ReadinessRoute = "ready";
    private const string DetailedRoute = "detailed";
    private const string JsonContentType = "application/json";

    private const string HealthyStatus = "healthy";
    private const string ReadyStatus = "ready";
    private const string NotReadyStatus = "not_ready";
    private const string ErrorStatus = "error";

    private const string BackendHealthCheckKey = "__health_check__";
    private const string FrameworkName = "dotnet";
    private const string FallbackVersion = "1.0.0";

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
    [HttpGet(LivenessRoute)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<HealthCheckResponse> Liveness()
    {
        _logger.LogInformation("Liveness check initiated", new { });
        try
        {
            return Ok(HealthyStatus.ToHealthCheckResponse());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Liveness check failed - unexpected error");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ErrorStatus.ToHealthCheckResponse(new HealthDetails { ErrorMessage = ex.Message }));
        }
    }

    /// <summary>
    /// Performs a readiness check - indicates whether the service can accept requests.
    /// Verifies connectivity to the lock backend (Redis, PostgreSQL, SQLite, etc).
    /// </summary>
    [HttpGet(ReadinessRoute)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthCheckResponse>> Readiness()
    {
        _logger.LogInformation("Readiness check initiated", new { });
        try
        {
            var testLock = await _repository.GetByKeyAsync(BackendHealthCheckKey);
            _logger.LogInformation("Readiness check passed", new { });
            var details = new HealthDetails { BackendConnected = true };
            return Ok(ReadyStatus.ToHealthCheckResponse(details));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Readiness check failed - backend connectivity issue");
            var details = new HealthDetails
            {
                BackendConnected = false,
                ErrorMessage = ex.Message
            };
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                NotReadyStatus.ToHealthCheckResponse(details));
        }
    }

    /// <summary>
    /// Detailed health status including metrics and backend information.
    /// </summary>
    [HttpGet(DetailedRoute)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<DetailedHealthResponse>> DetailedHealth()
    {
        _logger.LogInformation("Detailed health check initiated", new { });
        var startTime = DateTime.UtcNow;
        try
        {
            var backendHealthy = await VerifyBackendConnectivity();
            _logger.LogInformation("Detailed health check passed", new { });
            var responseTime = DateTime.UtcNow.Subtract(startTime);
            return Ok(backendHealthy.ToDetailedHealthResponse((long)responseTime.TotalMilliseconds));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Detailed health check failed - unexpected error");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ErrorStatus.ToDetailedHealthResponse(new RuntimeInfo { Framework = FrameworkName, Uptime = DateTime.UtcNow - startTime }));
        }
    }

    private async Task<bool> VerifyBackendConnectivity()
    {
        try
        {
            await _repository.GetByKeyAsync(BackendHealthCheckKey);
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
            .Version?.ToString() ?? FallbackVersion;
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
