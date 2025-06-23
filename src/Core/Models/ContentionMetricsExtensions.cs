#nullable enable

// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using System.Text;

namespace SarmKadan.DistributedLock.Models;

/// <summary>
/// Provides useful extension methods for <see cref="ContentionMetrics"/> to simplify common operations
/// and provide additional analytical capabilities.
/// </summary>
public static class ContentionMetricsExtensions
{
    /// <summary>
    /// Gets the current contention level as a percentage (0-100) based on current waiters vs peak waiters.
    /// Returns 0 when no waiters have been observed.
    /// </summary>
    /// <param name="metrics">The contention metrics instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="metrics"/> is null.</exception>
    /// <returns>The current contention percentage (0-100).</returns>
    public static double GetContentionPercentage(this ContentionMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        var current = metrics.CurrentWaiters;
        var peak = metrics.PeakWaiters;

        return peak == 0 ? 0d : Math.Min(100d, (current * 100d) / peak);
    }

    /// <summary>
    /// Gets a human-readable summary of the contention statistics.
    /// </summary>
    /// <param name="metrics">The contention metrics instance.</param>
    /// <param name="includeHistory">Whether to include historical data in the summary.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="metrics"/> is null.</exception>
    /// <returns>A formatted string with the metrics summary.</returns>
    public static string ToDetailedString(this ContentionMetrics metrics, bool includeHistory = true)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        var sb = new StringBuilder();
        sb.AppendLine($"Lock Metrics: {metrics.LockKey}");
        sb.AppendLine($" Current Waiters: {metrics.CurrentWaiters}");
        sb.AppendLine($" Peak Waiters: {metrics.PeakWaiters}");
        sb.AppendLine($" Contention Events: {metrics.TotalContentionEvents}");
        sb.AppendLine($" Total Waiters: {metrics.TotalWaiters}");
        sb.AppendLine($" Deadlocks: {metrics.DeadlocksDetected}");
        sb.AppendLine($" Average Wait: {metrics.AverageWaitTimeMs:F2}ms");
        sb.AppendLine($" Created: {metrics.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($" Last Updated: {metrics.LastUpdatedAt:yyyy-MM-dd HH:mm:ss}");

        if (includeHistory && metrics.TotalContentionEvents > 0)
        {
            var contentionPct = metrics.GetContentionPercentage();
            sb.AppendLine($" Current Contention: {contentionPct:F1}%");

            if (metrics.TotalWaiters > 100)
            {
                var durationSeconds = Math.Max(1, (metrics.LastUpdatedAt - metrics.CreatedAt).TotalSeconds);
                sb.AppendLine($" Waiter Throughput: {metrics.TotalWaiters / durationSeconds:F2} waiters/sec");
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Determines if the lock is currently experiencing high contention.
    /// High contention is defined as having more than 5 current waiters or contention percentage above 50%.
    /// </summary>
    /// <param name="metrics">The contention metrics instance.</param>
    /// <param name="thresholdWaiters">Minimum waiter count to consider as high contention. Default is 5.</param>
    /// <param name="thresholdPercentage">Minimum contention percentage to consider as high contention. Default is 50.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="metrics"/> is null.</exception>
    /// <returns>True if high contention is detected; otherwise false.</returns>
    public static bool IsHighContention(this ContentionMetrics metrics, int thresholdWaiters = 5, double thresholdPercentage = 50d)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        thresholdWaiters = Math.Max(0, thresholdWaiters);
        thresholdPercentage = Math.Max(0, thresholdPercentage);

        var current = metrics.CurrentWaiters;
        var contentionPct = metrics.GetContentionPercentage();

        return current >= thresholdWaiters || contentionPct >= thresholdPercentage;
    }

    /// <summary>
    /// Gets the estimated time saved by reducing contention, based on current wait times.
    /// This provides a rough estimate of how much cumulative wait time could be saved if contention was reduced.
    /// </summary>
    /// <param name="metrics">The contention metrics instance.</param>
    /// <param name="targetContentionPercentage">The target contention percentage to calculate savings against. Default is 10%.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="metrics"/> is null.</exception>
    /// <returns>Estimated time saved in milliseconds.</returns>
    public static double GetEstimatedTimeSaved(this ContentionMetrics metrics, double targetContentionPercentage = 10d)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        targetContentionPercentage = Math.Max(0, targetContentionPercentage);
        if (targetContentionPercentage >= 100)
            return 0d;

        var currentWaiters = metrics.CurrentWaiters;
        var peakWaiters = metrics.PeakWaiters;
        var avgWaitTime = metrics.AverageWaitTimeMs;

        if (currentWaiters <= 1 || peakWaiters <= 1 || avgWaitTime <= 0)
            return 0d;

        var currentContentionPct = metrics.GetContentionPercentage();
        if (currentContentionPct <= targetContentionPercentage)
            return 0d;

        var reductionFactor = 1d - (targetContentionPercentage / currentContentionPct);
        var estimatedWaitersAtTarget = currentWaiters * (1d - reductionFactor);

        // Estimate time saved per waiter
        var timeSavedPerWaiter = avgWaitTime * reductionFactor;

        // Total estimated time saved across all current waiters
        var totalTimeSaved = timeSavedPerWaiter * estimatedWaitersAtTarget;

        return Math.Round(totalTimeSaved, 2);
    }
}