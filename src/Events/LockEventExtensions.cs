#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Events;

using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Models;
using System.Globalization;

/// <summary>
/// Provides extension methods for <see cref="LockEvent"/> types to enable common operations
/// such as formatting, validation, and conversion between event types.
/// </summary>
public static class LockEventExtensions
{
    /// <summary>
    /// Determines whether the lock event represents a successful acquisition.
    /// </summary>
    /// <param name="lockEvent">The lock event to check.</param>
    /// <returns>True if the event represents a successful lock acquisition; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lockEvent"/> is null.</exception>
    public static bool IsAcquisitionSuccessful(this LockEvent lockEvent)
    {
        ArgumentNullException.ThrowIfNull(lockEvent);

        return lockEvent is LockAcquiredEvent acquiredEvent &&
               acquiredEvent.Status == LockStatus.Acquired;
    }

    /// <summary>
    /// Determines whether the lock event represents a failure.
    /// </summary>
    /// <param name="lockEvent">The lock event to check.</param>
    /// <returns>True if the event represents a lock failure; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lockEvent"/> is null.</exception>
    public static bool IsFailure(this LockEvent lockEvent)
    {
        ArgumentNullException.ThrowIfNull(lockEvent);

        return lockEvent is LockFailedEvent or LockAcquisitionFailedEvent;
    }

    /// <summary>
    /// Gets the lock identifier from the event.
    /// For <see cref="LockAcquiredEvent"/> and <see cref="LockReleasedEvent"/>, returns the LockId.
    /// For other event types, returns the LockName if available.
    /// </summary>
    /// <param name="lockEvent">The lock event.</param>
    /// <returns>The lock identifier, or null if not available.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lockEvent"/> is null.</exception>
    public static string? GetLockId(this LockEvent lockEvent)
    {
        ArgumentNullException.ThrowIfNull(lockEvent);

        return lockEvent switch
        {
            LockAcquiredEvent acquired => acquired.LockId,
            LockReleasedEvent released => released.LockId,
            LockExpiredEvent expired => expired.LockId,
            LockRenewedEvent renewed => renewed.LockId,
            LockFailedEvent failed => failed.LockId,
            LockAcquisitionFailedEvent acquisitionFailed => acquisitionFailed.LockName,
            LockContentionEvent contention => contention.LockName,
            LockPerformanceEvent performance => performance.LockId,
            LockErrorEvent error => error.LockId,
            _ => null
        };
    }

    /// <summary>
    /// Gets the owner identifier from the event.
    /// </summary>
    /// <param name="lockEvent">The lock event.</param>
    /// <returns>The owner identifier, or null if not available.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lockEvent"/> is null.</exception>
    public static string? GetOwnerId(this LockEvent lockEvent)
    {
        ArgumentNullException.ThrowIfNull(lockEvent);

        return lockEvent switch
        {
            LockAcquiredEvent acquired => acquired.OwnerId,
            LockReleasedEvent released => released.OwnerId,
            LockExpiredEvent expired => expired.OwnerId,
            LockRenewedEvent renewed => renewed.OwnerId,
            LockFailedEvent failed => failed.OwnerId,
            LockAcquisitionFailedEvent acquisitionFailed => acquisitionFailed.RequesterId,
            LockContentionEvent contention => contention.CompetingParties.FirstOrDefault(),
            _ => null
        };
    }

    /// <summary>
    /// Formats the event details as a JSON-like string for logging purposes.
    /// </summary>
    /// <param name="lockEvent">The lock event to format.</param>
    /// <param name="includeTimestamp">Whether to include the timestamp in the output.</param>
    /// <returns>A formatted string representation of the event.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lockEvent"/> is null.</exception>
    public static string ToLogString(this LockEvent lockEvent, bool includeTimestamp = true)
    {
        ArgumentNullException.ThrowIfNull(lockEvent);

        var parts = new List<string>();

        if (includeTimestamp)
        {
            parts.Add($"Timestamp={lockEvent.OccurredAt:O}");
        }

        parts.Add($"EventType={lockEvent.GetType().Name}");
        parts.Add($"EventId={lockEvent.EventId}");

        if (!string.IsNullOrEmpty(lockEvent.SourceSystem))
        {
            parts.Add($"SourceSystem={lockEvent.SourceSystem}");
        }

        if (!string.IsNullOrEmpty(lockEvent.CorrelationId))
        {
            parts.Add($"CorrelationId={lockEvent.CorrelationId}");
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Creates a new <see cref="LockFailedEvent"/> from an existing lock event.
    /// Useful for converting any lock event into a failure notification.
    /// </summary>
    /// <param name="lockEvent">The original lock event.</param>
    /// <param name="reason">The failure reason.</param>
    /// <returns>A new <see cref="LockFailedEvent"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lockEvent"/> or <paramref name="reason"/> is null.</exception>
    public static LockFailedEvent ToFailureEvent(this LockEvent lockEvent, string reason)
    {
        ArgumentNullException.ThrowIfNull(lockEvent);
        ArgumentException.ThrowIfNullOrEmpty(reason);

        var lockId = lockEvent.GetLockId();
        var ownerId = lockEvent.GetOwnerId();

        return new LockFailedEvent
        {
            LockId = lockId ?? string.Empty,
            OwnerId = ownerId ?? string.Empty,
            Reason = reason,
            SourceSystem = lockEvent.SourceSystem,
            CorrelationId = lockEvent.CorrelationId,
            OccurredAt = lockEvent.OccurredAt
        };
    }

    /// <summary>
    /// Determines whether the event occurred within a specified time range.
    /// </summary>
    /// <param name="lockEvent">The lock event to check.</param>
    /// <param name="startTime">The start of the time range (inclusive).</param>
    /// <param name="endTime">The end of the time range (inclusive).</param>
    /// <returns>True if the event occurred within the time range; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lockEvent"/> is null.</exception>
    public static bool IsWithinTimeRange(this LockEvent lockEvent, DateTime startTime, DateTime endTime)
    {
        ArgumentNullException.ThrowIfNull(lockEvent);

        return lockEvent.OccurredAt >= startTime && lockEvent.OccurredAt <= endTime;
    }

    /// <summary>
    /// Gets the duration of the lock operation from the event.
    /// Returns TimeSpan.Zero for events that don't have duration information.
    /// </summary>
    /// <param name="lockEvent">The lock event.</param>
    /// <returns>The duration of the lock operation, or TimeSpan.Zero.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lockEvent"/> is null.</exception>
    public static TimeSpan GetDuration(this LockEvent lockEvent)
    {
        ArgumentNullException.ThrowIfNull(lockEvent);

        return lockEvent switch
        {
            LockAcquiredEvent acquired => acquired.Duration,
            LockReleasedEvent released => released.HeldDuration,
            LockExpiredEvent expired => expired.TotalDuration,
            LockRenewedEvent renewed => renewed.RenewedDuration,
            _ => TimeSpan.Zero
        };
    }

    /// <summary>
    /// Gets the expiration timestamp from the event.
    /// Returns null for events that don't have expiration information.
    /// </summary>
    /// <param name="lockEvent">The lock event.</param>
    /// <returns>The expiration timestamp, or null.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lockEvent"/> is null.</exception>
    public static DateTime? GetExpirationTime(this LockEvent lockEvent)
    {
        ArgumentNullException.ThrowIfNull(lockEvent);

        return lockEvent switch
        {
            LockAcquiredEvent acquired => acquired.ExpiresAt,
            LockRenewedEvent renewed => renewed.NewExpiresAt,
            _ => null
        };
    }

    /// <summary>
    /// Determines whether the event is related to a specific lock by name.
    /// </summary>
    /// <param name="lockEvent">The lock event.</param>
    /// <param name="lockName">The lock name to match.</param>
    /// <returns>True if the event is related to the specified lock; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lockEvent"/> or <paramref name="lockName"/> is null.</exception>
    public static bool IsRelatedToLock(this LockEvent lockEvent, string lockName)
    {
        ArgumentNullException.ThrowIfNull(lockEvent);
        ArgumentException.ThrowIfNullOrEmpty(lockName);

        var eventLockId = lockEvent.GetLockId();
        var eventLockName = lockEvent switch
        {
            LockAcquiredEvent acquired => acquired.LockName,
            LockReleasedEvent released => released.LockName,
            LockExpiredEvent expired => expired.LockName,
            LockRenewedEvent renewed => renewed.LockName,
            _ => null
        };

        return string.Equals(eventLockId, lockName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(eventLockName, lockName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the fencing token from the event if available.
    /// Returns null for events that don't have fencing token information.
    /// </summary>
    /// <param name="lockEvent">The lock event.</param>
    /// <returns>The fencing token as a string, or null.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lockEvent"/> is null.</exception>
    public static string? GetFencingToken(this LockEvent lockEvent)
    {
        ArgumentNullException.ThrowIfNull(lockEvent);

        return lockEvent switch
        {
            LockAcquiredEvent acquired => acquired.FencingToken.ToString(),
            _ => null
        };
    }
}