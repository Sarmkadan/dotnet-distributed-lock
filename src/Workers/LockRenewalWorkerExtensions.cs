#nullable enable

// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Workers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

/// <summary>
/// Extension methods for <see cref="LockRenewalWorker"/> that provide convenient
/// ways to configure and manage lock renewal operations.
/// </summary>
public static class LockRenewalWorkerExtensions
{
    /// <summary>
    /// Registers a lock for automatic renewal with the specified parameters.
    /// </summary>
    /// <param name="worker">The lock renewal worker instance.</param>
    /// <param name="lockId">The unique identifier of the lock.</param>
    /// <param name="fencingToken">The fencing token to use for renewal operations.</param>
    /// <param name="renewalInterval">The interval at which the lock should be renewed.</param>
    /// <returns>True if the lock was successfully registered; false if already registered.</returns>
    public static bool TryRegisterForRenewal(
        this LockRenewalWorker worker,
        string lockId,
        ulong fencingToken,
        TimeSpan renewalInterval)
    {
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentException.ThrowIfNullOrWhiteSpace(lockId);

        try
        {
            worker.RegisterForRenewal(lockId, fencingToken, renewalInterval);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Safely unregisters a lock from renewal, suppressing any exceptions.
    /// </summary>
    /// <param name="worker">The lock renewal worker instance.</param>
    /// <param name="lockId">The unique identifier of the lock to unregister.</param>
    public static void SafeUnregisterFromRenewal(
        this LockRenewalWorker worker,
        string lockId)
    {
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentException.ThrowIfNullOrWhiteSpace(lockId);

        try
        {
            worker.UnregisterFromRenewal(lockId);
        }
        catch (Exception)
        {
            // Silently fail - this is a safe operation
        }
    }

    /// <summary>
    /// Gets the time remaining until the next scheduled renewal for the specified lock.
    /// </summary>
    /// <param name="worker">The lock renewal worker instance.</param>
    /// <param name="lockId">The unique identifier of the lock.</param>
    /// <returns>The time remaining until next renewal, or null if lock is not registered.</returns>
    public static TimeSpan? GetTimeUntilNextRenewal(
        this LockRenewalWorker worker,
        string lockId)
    {
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentException.ThrowIfNullOrWhiteSpace(lockId);

        if (worker is LockRenewalWorker renewalWorker)
        {
            var scheduleField = typeof(LockRenewalWorker)
                .GetField(
                    "_renewalSchedules",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (scheduleField?.GetValue(renewalWorker) is System.Collections.Concurrent.ConcurrentDictionary<string, dynamic> renewalSchedules)
            {
                if (renewalSchedules.TryGetValue(lockId, out var schedule) && schedule.NextRenewalTime > DateTime.UtcNow)
                {
                    return schedule.NextRenewalTime - DateTime.UtcNow;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Updates the renewal interval for an already registered lock.
    /// </summary>
    /// <param name="worker">The lock renewal worker instance.</param>
    /// <param name="lockId">The unique identifier of the lock.</param>
    /// <param name="newRenewalInterval">The new interval at which the lock should be renewed.</param>
    /// <returns>True if the interval was successfully updated; false otherwise.</returns>
    public static bool TryUpdateRenewalInterval(
        this LockRenewalWorker worker,
        string lockId,
        TimeSpan newRenewalInterval)
    {
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentException.ThrowIfNullOrWhiteSpace(lockId);

        try
        {
            var scheduleField = typeof(LockRenewalWorker)
                .GetField(
                    "_renewalSchedules",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (scheduleField?.GetValue(worker) is System.Collections.Concurrent.ConcurrentDictionary<string, dynamic> renewalSchedules)
            {
                if (renewalSchedules.TryGetValue(lockId, out var schedule))
                {
                    schedule.RenewalInterval = newRenewalInterval;
                    schedule.NextRenewalTime = DateTime.UtcNow.Add(newRenewalInterval);
                    return true;
                }
            }

            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}