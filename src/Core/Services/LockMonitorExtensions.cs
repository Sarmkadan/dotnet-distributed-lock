#nullable enable

// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Services;

namespace SarmKadan.DistributedLock.Services;

/// <summary>
/// Extension methods for <see cref="LockMonitor"/> that provide additional functionality
/// for monitoring and managing distributed locks.
/// </summary>
public static class LockMonitorExtensions
{
    /// <summary>
    /// Checks if a specific lock is currently being monitored.
    /// </summary>
    /// <param name="monitor">The lock monitor instance.</param>
    /// <param name="lockKey">The lock key to check.</param>
    /// <returns>True if the lock is being monitored; otherwise, false.</returns>
    public static bool IsLockMonitored(this LockMonitor monitor, string lockKey)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        ArgumentException.ThrowIfNullOrWhiteSpace(lockKey);

        var monitoredLocks = monitor.GetMonitoredLocks();
        return monitoredLocks.Contains(lockKey);
    }

    /// <summary>
    /// Gets the total number of currently monitored locks.
    /// </summary>
    /// <param name="monitor">The lock monitor instance.</param>
    /// <returns>The count of monitored locks.</returns>
    public static int GetMonitoredLockCount(this LockMonitor monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        return monitor.GetMonitoredLocks().Count();
    }

    /// <summary>
    /// Checks if any locks are currently being monitored.
    /// </summary>
    /// <param name="monitor">The lock monitor instance.</param>
    /// <returns>True if at least one lock is monitored; otherwise, false.</returns>
    public static bool HasActiveLocks(this LockMonitor monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        return monitor.GetMonitoredLockCount() > 0;
    }

    /// <summary>
    /// Waits for a specific lock to be released with an optional timeout.
    /// </summary>
    /// <param name="monitor">The lock monitor instance.</param>
    /// <param name="lockKey">The lock key to wait for.</param>
    /// <param name="timeout">Optional timeout for the wait operation. Defaults to 30 seconds.</param>
    /// <returns>True if the lock was released within the timeout period; otherwise, false.</returns>
    public static async Task<bool> WaitForLockReleaseAsync(this LockMonitor monitor, string lockKey, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        ArgumentException.ThrowIfNullOrWhiteSpace(lockKey);

        var timeoutValue = timeout ?? TimeSpan.FromSeconds(30);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeoutValue)
        {
            if (!monitor.IsLockMonitored(lockKey))
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        return false;
    }
}
