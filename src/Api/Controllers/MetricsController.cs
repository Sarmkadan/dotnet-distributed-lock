#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SarmKadan.DistributedLock.Services;

/// <summary>
/// Metrics and monitoring endpoints for distributed lock operations.
/// Provides insights into lock usage patterns, contention, and performance.
/// </summary>
/// <remarks>
/// This controller is read-only: metrics are written exclusively by
/// <see cref="SarmKadan.DistributedLock.Events.MetricsTrackingEventSubscriber"/> as lock events occur,
/// via the shared <see cref="IMetricsStore"/>. There is no HTTP endpoint for submitting metrics,
/// so an unauthenticated client cannot poison the reported figures.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class MetricsController : ControllerBase
{
    private readonly IMetricsStore _metricsStore;
    private readonly ILogger<MetricsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricsController"/> class.
    /// </summary>
    /// <param name="metricsStore">The store metrics are read from.</param>
    /// <param name="logger">The logger used for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="metricsStore"/> or <paramref name="logger"/> is null.</exception>
    public MetricsController(IMetricsStore metricsStore, ILogger<MetricsController> logger)
    {
        ArgumentNullException.ThrowIfNull(metricsStore);
        ArgumentNullException.ThrowIfNull(logger);

        _metricsStore = metricsStore;
        _logger = logger;
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

        var allMetrics = _metricsStore.GetAllLockMetrics().Values;

        var totalAttempts = allMetrics.Sum(m => m.AcquisitionAttempts);
        var totalSuccesses = allMetrics.Sum(m => m.SuccessfulAcquisitions);
        var totalFailures = allMetrics.Sum(m => m.FailedAcquisitions);
        var avgHoldTime = allMetrics.Any()
            ? allMetrics.Average(m => m.AverageHoldTimeMs)
            : 0;

        return Ok(new SystemMetricsResponse
        {
            TotalLockOperations = totalAttempts,
            SuccessfulAcquisitions = totalSuccesses,
            FailedAcquisitions = totalFailures,
            SuccessRate = totalAttempts > 0 ? (double)totalSuccesses / totalAttempts * 100 : 0,
            AverageHoldTimeMs = avgHoldTime,
            ActiveLocks = allMetrics.Count,
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
        ArgumentException.ThrowIfNullOrEmpty(lockName);

        _logger.LogInformation("Retrieving metrics for lock: {LockName}", lockName);

        if (!_metricsStore.TryGetLockMetrics(lockName, out var metrics) || metrics is null)
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

        var holdTimes = _metricsStore.GetAllLockMetrics().Values
            .Select(m => m.AverageHoldTimeMs)
            .OrderBy(x => x)
            .ToList();

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
    /// Resets all collected metrics.
    /// Useful for starting fresh observations in testing or debugging scenarios.
    /// </summary>
    [HttpPost("reset")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult ResetMetrics()
    {
        _logger.LogWarning("Metrics reset triggered");
        _metricsStore.Reset();
        return Ok(new { Message = "All metrics have been reset" });
    }
}

/// <summary>
/// Response payload describing system-wide lock metrics.
/// </summary>
public record SystemMetricsResponse
{
    /// <summary>Gets the total number of lock operations recorded across all locks.</summary>
    public long TotalLockOperations { get; init; }

    /// <summary>Gets the total number of successful acquisitions across all locks.</summary>
    public long SuccessfulAcquisitions { get; init; }

    /// <summary>Gets the total number of failed acquisitions across all locks.</summary>
    public long FailedAcquisitions { get; init; }

    /// <summary>Gets the overall acquisition success rate, as a percentage.</summary>
    public double SuccessRate { get; init; }

    /// <summary>Gets the average hold time, in milliseconds, across all locks.</summary>
    public double AverageHoldTimeMs { get; init; }

    /// <summary>Gets the number of distinct locks currently tracked.</summary>
    public int ActiveLocks { get; init; }

    /// <summary>Gets the timestamp at which this response was generated.</summary>
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Response payload describing metrics for a single named lock.
/// </summary>
public record LockMetricsResponse
{
    /// <summary>Gets the name of the lock these metrics describe.</summary>
    public required string LockName { get; init; }

    /// <summary>Gets the total number of acquisition attempts recorded for the lock.</summary>
    public long AcquisitionAttempts { get; init; }

    /// <summary>Gets the number of successful acquisition attempts recorded for the lock.</summary>
    public long SuccessfulAcquisitions { get; init; }

    /// <summary>Gets the number of failed acquisition attempts recorded for the lock.</summary>
    public long FailedAcquisitions { get; init; }

    /// <summary>Gets the acquisition success rate for the lock, as a percentage.</summary>
    public double SuccessRate { get; init; }

    /// <summary>Gets the running average hold time, in milliseconds, for the lock.</summary>
    public double AverageHoldTimeMs { get; init; }

    /// <summary>Gets the maximum observed hold time, in milliseconds, for the lock.</summary>
    public long MaxHoldTimeMs { get; init; }

    /// <summary>Gets the total number of contention events recorded for the lock.</summary>
    public long TotalContentionEvents { get; init; }

    /// <summary>Gets the timestamp of the most recent acquisition, if any.</summary>
    public DateTime? LastAcquiredAt { get; init; }

    /// <summary>Gets the timestamp at which this response was generated.</summary>
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Response payload describing performance-related latency metrics.
/// </summary>
public record PerformanceMetricsResponse
{
    /// <summary>Gets the average acquisition-related time, in milliseconds, across all locks.</summary>
    public double AverageAcquisitionTimeMs { get; init; }

    /// <summary>Gets the median acquisition-related time, in milliseconds, across all locks.</summary>
    public double MedianAcquisitionTimeMs { get; init; }

    /// <summary>Gets the 95th percentile acquisition-related time, in milliseconds.</summary>
    public double P95AcquisitionTimeMs { get; init; }

    /// <summary>Gets the 99th percentile acquisition-related time, in milliseconds.</summary>
    public double P99AcquisitionTimeMs { get; init; }

    /// <summary>Gets the maximum observed acquisition-related time, in milliseconds.</summary>
    public double MaxAcquisitionTimeMs { get; init; }

    /// <summary>Gets the timestamp at which this response was generated.</summary>
    public DateTime Timestamp { get; init; }
}
