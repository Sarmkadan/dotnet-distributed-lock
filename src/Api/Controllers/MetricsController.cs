// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SarmKadan.DistributedLock.Core.Models;
using SarmKadan.DistributedLock.Core.Services;

/// <summary>
/// Metrics and monitoring endpoints for distributed lock operations.
/// Provides insights into lock usage patterns, contention, and performance.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MetricsController : ControllerBase
{
    private readonly ILockService _lockService;
    private readonly ILogger<MetricsController> _logger;
    private static readonly Dictionary<string, LockMetrics> _metricsCache = new();

    public MetricsController(ILockService lockService, ILogger<MetricsController> logger)
    {
        _lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets overall metrics for lock operations across the system.
    /// Includes acquisition attempts, successes, failures, and average hold times.
    /// </summary>
    [HttpGet("system")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<SystemMetricsResponse> GetSystemMetrics()
    {
        _logger.LogInformation("Retrieving system-wide lock metrics");

        var totalAttempts = _metricsCache.Values.Sum(m => m.AcquisitionAttempts);
        var totalSuccesses = _metricsCache.Values.Sum(m => m.SuccessfulAcquisitions);
        var totalFailures = _metricsCache.Values.Sum(m => m.FailedAcquisitions);
        var avgHoldTime = _metricsCache.Values.Any()
            ? _metricsCache.Values.Average(m => m.AverageHoldTimeMs)
            : 0;

        return Ok(new SystemMetricsResponse
        {
            TotalLockOperations = totalAttempts,
            SuccessfulAcquisitions = totalSuccesses,
            FailedAcquisitions = totalFailures,
            SuccessRate = totalAttempts > 0 ? (double)totalSuccesses / totalAttempts * 100 : 0,
            AverageHoldTimeMs = avgHoldTime,
            ActiveLocks = _metricsCache.Count,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Gets detailed metrics for a specific lock.
    /// </summary>
    /// <param name="lockName">The name of the lock to retrieve metrics for</param>
    [HttpGet("lock/{lockName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<LockMetricsResponse> GetLockMetrics([FromRoute] string lockName)
    {
        _logger.LogInformation("Retrieving metrics for lock: {LockName}", lockName);

        if (!_metricsCache.TryGetValue(lockName, out var metrics))
            return NotFound($"No metrics found for lock: {lockName}");

        return Ok(new LockMetricsResponse
        {
            LockName = lockName,
            AcquisitionAttempts = metrics.AcquisitionAttempts,
            SuccessfulAcquisitions = metrics.SuccessfulAcquisitions,
            FailedAcquisitions = metrics.FailedAcquisitions,
            SuccessRate = metrics.AcquisitionAttempts > 0
                ? (double)metrics.SuccessfulAcquisitions / metrics.AcquisitionAttempts * 100
                : 0,
            AverageHoldTimeMs = metrics.AverageHoldTimeMs,
            MaxHoldTimeMs = metrics.MaxHoldTimeMs,
            TotalContentionEvents = metrics.ContentionCount,
            LastAcquiredAt = metrics.LastAcquisitionTime,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Gets performance-related metrics including latency percentiles.
    /// </summary>
    [HttpGet("performance")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<PerformanceMetricsResponse> GetPerformanceMetrics()
    {
        _logger.LogInformation("Retrieving performance metrics");

        var holdTimes = _metricsCache.Values.Select(m => m.AverageHoldTimeMs).OrderBy(x => x).ToList();

        return Ok(new PerformanceMetricsResponse
        {
            AverageAcquisitionTimeMs = holdTimes.Any() ? holdTimes.Average() : 0,
            MedianAcquisitionTimeMs = holdTimes.Any() ? holdTimes[holdTimes.Count / 2] : 0,
            P95AcquisitionTimeMs = holdTimes.Any()
                ? holdTimes[(int)(holdTimes.Count * 0.95)]
                : 0,
            P99AcquisitionTimeMs = holdTimes.Any()
                ? holdTimes[(int)(holdTimes.Count * 0.99)]
                : 0,
            MaxAcquisitionTimeMs = holdTimes.Any() ? holdTimes.Max() : 0,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Records metrics for a lock operation.
    /// Called internally by the lock service to track statistics.
    /// </summary>
    [HttpPost("record")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public ActionResult RecordMetrics([FromBody] RecordMetricsRequest request)
    {
        if (string.IsNullOrEmpty(request.LockName))
            return BadRequest("Lock name is required");

        if (!_metricsCache.TryGetValue(request.LockName, out var metrics))
        {
            metrics = new LockMetrics();
            _metricsCache[request.LockName] = metrics;
        }

        metrics.AcquisitionAttempts++;
        if (request.Successful)
        {
            metrics.SuccessfulAcquisitions++;
        }
        else
        {
            metrics.FailedAcquisitions++;
        }

        metrics.AverageHoldTimeMs = (metrics.AverageHoldTimeMs + request.HoldTimeMs) / 2;
        metrics.MaxHoldTimeMs = Math.Max(metrics.MaxHoldTimeMs, request.HoldTimeMs);
        metrics.LastAcquisitionTime = DateTime.UtcNow;

        if (request.ContentionDetected)
            metrics.ContentionCount++;

        return Ok();
    }

    /// <summary>
    /// Resets all collected metrics.
    /// Useful for starting fresh observations in testing or debugging scenarios.
    /// </summary>
    [HttpPost("reset")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult ResetMetrics()
    {
        _logger.LogWarning("Metrics reset triggered");
        _metricsCache.Clear();
        return Ok(new { Message = "All metrics have been reset" });
    }
}

public record SystemMetricsResponse
{
    public long TotalLockOperations { get; init; }
    public long SuccessfulAcquisitions { get; init; }
    public long FailedAcquisitions { get; init; }
    public double SuccessRate { get; init; }
    public double AverageHoldTimeMs { get; init; }
    public int ActiveLocks { get; init; }
    public DateTime Timestamp { get; init; }
}

public record LockMetricsResponse
{
    public required string LockName { get; init; }
    public long AcquisitionAttempts { get; init; }
    public long SuccessfulAcquisitions { get; init; }
    public long FailedAcquisitions { get; init; }
    public double SuccessRate { get; init; }
    public double AverageHoldTimeMs { get; init; }
    public long MaxHoldTimeMs { get; init; }
    public long TotalContentionEvents { get; init; }
    public DateTime? LastAcquiredAt { get; init; }
    public DateTime Timestamp { get; init; }
}

public record PerformanceMetricsResponse
{
    public double AverageAcquisitionTimeMs { get; init; }
    public double MedianAcquisitionTimeMs { get; init; }
    public double P95AcquisitionTimeMs { get; init; }
    public double P99AcquisitionTimeMs { get; init; }
    public double MaxAcquisitionTimeMs { get; init; }
    public DateTime Timestamp { get; init; }
}

public record RecordMetricsRequest
{
    public required string LockName { get; init; }
    public bool Successful { get; init; }
    public long HoldTimeMs { get; init; }
    public bool ContentionDetected { get; init; }
}
