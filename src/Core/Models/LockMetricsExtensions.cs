#nullable enable

// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Models;

/// <summary>
/// Provides helper extension methods for <see cref="LockMetrics"/> that derive
/// aggregate figures and health indicators without changing the metrics state.
/// </summary>
public static class LockMetricsExtensions
{
    /// <summary>
    /// Gets the number of failed operations across acquisitions and renewals.
    /// </summary>
    /// <param name="metrics">The lock metrics instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="metrics"/> is null.</exception>
    /// <returns>The sum of failed acquisitions and failed renewals.</returns>
    public static long GetFailedOperationCount(this LockMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        return metrics.FailedAcquisitions + metrics.FailedRenewals;
    }

    /// <summary>
    /// Gets the total number of tracked operations (acquisition attempts, renewals and releases).
    /// </summary>
    /// <param name="metrics">The lock metrics instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="metrics"/> is null.</exception>
    /// <returns>The combined count of tracked operations.</returns>
    public static long GetTotalOperationCount(this LockMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        return metrics.TotalAcquisitionAttempts + metrics.TotalRenewals + metrics.TotalReleases;
    }

    /// <summary>
    /// Gets the acquisition failure rate as a percentage (0-100).
    /// Returns 0 when no acquisition attempts have been recorded.
    /// </summary>
    /// <param name="metrics">The lock metrics instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="metrics"/> is null.</exception>
    /// <returns>The percentage of acquisition attempts that failed.</returns>
    public static double GetAcquisitionFailureRate(this LockMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        if (metrics.TotalAcquisitionAttempts == 0)
        {
            return 0d;
        }

        return (double)metrics.FailedAcquisitions / metrics.TotalAcquisitionAttempts * 100d;
    }

    /// <summary>
    /// Gets the share of released locks that ended by expiration rather than explicit release, as a percentage (0-100).
    /// Returns 0 when no locks have been released or expired.
    /// </summary>
    /// <param name="metrics">The lock metrics instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="metrics"/> is null.</exception>
    /// <returns>The percentage of locks that expired out of all locks that ended.</returns>
    public static double GetExpiredLockRate(this LockMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        var totalEnded = metrics.TotalReleases + metrics.ExpiredLocks;
        if (totalEnded == 0)
        {
            return 0d;
        }

        return (double)metrics.ExpiredLocks / totalEnded * 100d;
    }

    /// <summary>
    /// Determines whether the lock subsystem is healthy, meaning the overall success rate
    /// meets the given threshold. Returns true when no operations have been recorded yet.
    /// </summary>
    /// <param name="metrics">The lock metrics instance.</param>
    /// <param name="minimumSuccessRate">The minimum acceptable overall success rate (0-100). Defaults to 95.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="metrics"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="minimumSuccessRate"/> is outside 0-100.</exception>
    /// <returns>True when the overall success rate is at or above the threshold; otherwise false.</returns>
    public static bool IsHealthy(this LockMetrics metrics, double minimumSuccessRate = 95d)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        if (minimumSuccessRate < 0d || minimumSuccessRate > 100d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumSuccessRate),
                minimumSuccessRate,
                "Minimum success rate must be between 0 and 100.");
        }

        if (metrics.GetTotalOperationCount() == 0)
        {
            return true;
        }

        return metrics.OverallSuccessRate >= minimumSuccessRate;
    }
}
