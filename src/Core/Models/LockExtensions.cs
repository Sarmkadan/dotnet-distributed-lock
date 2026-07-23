#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =====================================================================

using System.Threading;
using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Exceptions;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Services;

namespace SarmKadan.DistributedLock.Models;

/// <summary>
/// Provides extension methods for the <see cref="Lock"/> class to enhance lock management functionality.
/// </summary>
public static class LockExtensions
{
    /// <summary>
    /// Checks if the lock is currently being acquired or renewed.
    /// </summary>
    /// <param name="lock">The lock instance to check.</param>
    /// <returns>True if the lock is in an active state; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lock"/> is null.</exception>
    public static bool IsActive(this Lock @lock)
    {
        ArgumentNullException.ThrowIfNull(@lock);
        return @lock.Status is LockStatus.Acquiring or LockStatus.Renewing;
    }

    /// <summary>
    /// Checks if the lock is available for acquisition (not held and not expired).
    /// </summary>
    /// <param name="lock">The lock instance to check.</param>
    /// <returns>True if the lock is available; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lock"/> is null.</exception>
    public static bool IsAvailable(this Lock @lock)
    {
        ArgumentNullException.ThrowIfNull(@lock);
        return @lock.Status != LockStatus.Held && !@lock.IsExpired;
    }

    /// <summary>
    /// Gets the remaining time until the lock expires.
    /// </summary>
    /// <param name="lock">The lock instance to check.</param>
    /// <returns>The remaining time until expiration, or TimeSpan.Zero if already expired.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lock"/> is null.</exception>
    /// <exception cref="OverflowException">Thrown when the time calculation results in an overflow.</exception>
    public static TimeSpan GetRemainingTime(this Lock @lock)
    {
        ArgumentNullException.ThrowIfNull(@lock);
        var remaining = @lock.ExpiresAt - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>
    /// Safely renews the lock if it's still valid, otherwise releases it.
    /// </summary>
    /// <param name="lock">The lock instance to renew.</param>
    /// <param name="newDuration">Optional new duration for the lock.</param>
    /// <returns>True if the lock was successfully renewed; false if it was expired and released.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lock"/> is null.</exception>
    public static bool SafeRenew(this Lock @lock, TimeSpan? newDuration = null)
    {
        ArgumentNullException.ThrowIfNull(@lock);

        if (@lock.IsExpired)
        {
            @lock.Release();
            return false;
        }

        @lock.Renew(newDuration);
        return true;
    }
}