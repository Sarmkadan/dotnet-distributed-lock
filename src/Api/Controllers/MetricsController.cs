#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Api.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock.Configuration;
using SarmKadan.DistributedLock.Services;
using SarmKadan.DistributedLock.Utilities.Extensions;

/// <summary>
/// Metrics and monitoring endpoints for distributed lock operations.
/// Provides insights into lock usage patterns, contention, and performance.
/// </summary>
/// <remarks>
/// This controller is read-only: metrics are written exclusively by
/// <see cref="SarmKadan.DistributedLock.Events.MetricsTrackingEventSubscriber"/> as lock events occur,
/// via the shared <see cref="IMetricsStore"/>. The <see cref="ResetMetrics"/> endpoint can be
/// enabled or disabled via the <see cref="DistributedLockOptions.EnableMetricsResetEndpoint"/> option.
/// </remarks>
[ApiController]
[Route(BaseRoute)]
[Produces(JsonContentType)]
public sealed class MetricsController : ControllerBase
{
    private const string BaseRoute = "api/[controller]";
    private const string JsonContentType = "application/json";

    private const string SystemRoute = "system";
    private const string LockRoute = "lock/{lockName}";
    private const string PerformanceRoute = "performance";
    private const string ResetRoute = "reset";

    private const string InvalidLockNameMessage =
        "Lock name must be 1-256 characters: letters, digits, '-', '_', '.', ':'.";
    private const string LockNotFoundMessagePrefix = "No metrics found for lock: ";
    private const string StoreUnavailableMessage = "Metrics are temporarily unavailable";
    private const string ResetDisabledMessage = "Metrics reset endpoint is disabled";
    private const string ResetCompletedMessage = "All metrics have been reset";

    private readonly IMetricsStore _metricsStore;
    private readonly ILogger<MetricsController> _logger;
    private readonly DistributedLockOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricsController"/> class.
    /// </summary>
    /// <param name="metricsStore">The store metrics are read from.</param>
    /// <param name="logger">The logger used for diagnostic output.</param>
    /// <param name="options">The distributed lock configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="metricsStore"/>, <paramref name="logger"/>, or <paramref name="options"/> is null.</exception>
    public MetricsController(IMetricsStore metricsStore, ILogger<MetricsController> logger, DistributedLockOptions options)
    {
        ArgumentNullException.ThrowIfNull(metricsStore);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _metricsStore = metricsStore;
        _logger = logger;
        _options = options;
    }

    /// <summary>
    /// Gets overall metrics for lock operations across the system.
    /// Includes acquisition attempts, successes, failures, and average hold times.
    /// </summary>
    [HttpGet(SystemRoute)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public ActionResult<SystemMetricsResponse> GetSystemMetrics()
    {
        _logger.LogInformation("Retrieving system-wide lock metrics");

        IReadOnlyList<LockMetricsSnapshot> allMetrics;
        try
        {
            allMetrics = _metricsStore.GetAllLockMetrics().Values.ToList();
        }
        catch (Exception ex)
        {
            return MetricsStoreUnavailable(ex);
        }

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
            ActiveLocks = allMetrics.Count(),
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Gets detailed metrics for a specific lock.
    /// </summary>
    /// <param name="lockName">The name of the lock to retrieve metrics for</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="lockName"/> is null or empty.</exception>
    [HttpGet(LockRoute)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public ActionResult<LockMetricsResponse> GetLockMetrics([FromRoute] string lockName)
    {
        ArgumentException.ThrowIfNullOrEmpty(lockName);

        // Lock names are validated on write, so a malformed or oversized name cannot have metrics.
        // Rejecting it here keeps such route values out of logs and the store lookup.
        if (!lockName.IsValidLockName())
            return BadRequest(InvalidLockNameMessage);

        _logger.LogInformation("Retrieving metrics for lock: {LockName}", lockName);

        LockMetricsSnapshot? metrics;
        try
        {
            if (!_metricsStore.TryGetLockMetrics(lockName, out metrics) || metrics is null)
                return NotFound($"{LockNotFoundMessagePrefix}{lockName}");
        }
        catch (Exception ex)
        {
            return MetricsStoreUnavailable(ex);
        }

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
    [HttpGet(PerformanceRoute)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public ActionResult<PerformanceMetricsResponse> GetPerformanceMetrics()
    {
        _logger.LogInformation("Retrieving performance metrics");

        List<double> holdTimes;
        try
        {
            holdTimes = _metricsStore.GetAllLockMetrics().Values
                .Select(m => m.AverageHoldTimeMs)
                .OrderBy(x => x)
                .ToList();
        }
        catch (Exception ex)
        {
            return MetricsStoreUnavailable(ex);
        }

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
    /// Builds the 503 response returned when the metrics store cannot be read, and logs the failure.
    /// </summary>
    /// <param name="exception">The exception raised while reading from the metrics store.</param>
    /// <returns>A 503 Service Unavailable result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception"/> is null.</exception>
    private ObjectResult MetricsStoreUnavailable(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        _logger.LogError(exception, "Metrics store is unavailable");
        return StatusCode(
            StatusCodes.Status503ServiceUnavailable,
            new { Message = StoreUnavailableMessage });
    }

    /// <summary>
    /// Resets all collected metrics.
    /// Useful for starting fresh observations in testing or debugging scenarios.
    /// </summary>
    /// <remarks>
    /// This endpoint requires no authentication in the current implementation.
    /// Consider securing it in production environments if metrics reset should be restricted.
    /// </remarks>
    [HttpPost(ResetRoute)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult ResetMetrics()
    {
        if (!_options.EnableMetricsResetEndpoint)
        {
            _logger.LogWarning("Metrics reset attempted but endpoint is disabled via configuration");
            return StatusCode(StatusCodes.Status403Forbidden, new { Message = ResetDisabledMessage });
        }

        _logger.LogWarning("Metrics reset triggered");
        _metricsStore.Reset();
        return Ok(new { Message = ResetCompletedMessage });
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
