#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Events;

using SarmKadan.DistributedLock.Core.Models;

/// <summary>
/// Base event class for all lock-related events.
/// Provides common properties for tracking event source and timing.
/// </summary>
public abstract class LockEvent
{
    public string EventId { get; } = Guid.NewGuid().ToString();
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string? SourceSystem { get; set; }
    public string? CorrelationId { get; set; }

    public override string ToString()
    {
        return $"{GetType().Name} [ID: {EventId}, Time: {OccurredAt:O}]";
    }
}

/// <summary>
/// Event raised when a lock is successfully acquired.
/// </summary>
public sealed class LockAcquiredEvent : LockEvent
{
    public required string LockId { get; init; }
    public required string LockName { get; init; }
    public required string OwnerId { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required ulong FencingToken { get; init; }
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Event raised when a lock is released before expiration.
/// </summary>
public sealed class LockReleasedEvent : LockEvent
{
    public required string LockId { get; init; }
    public required string LockName { get; init; }
    public required string OwnerId { get; init; }
    public DateTime AcquiredAt { get; init; }
    public DateTime ReleasedAt { get; init; }
    public TimeSpan HeldDuration { get; init; }
}

/// <summary>
/// Event raised when a lock expires without being released.
/// </summary>
public sealed class LockExpiredEvent : LockEvent
{
    public required string LockId { get; init; }
    public required string LockName { get; init; }
    public required string OwnerId { get; init; }
    public required DateTime ExpiredAt { get; init; }
    public TimeSpan TotalDuration { get; init; }
}

/// <summary>
/// Event raised when a lock is renewed by its owner.
/// </summary>
public sealed class LockRenewedEvent : LockEvent
{
    public required string LockId { get; init; }
    public required string LockName { get; init; }
    public required string OwnerId { get; init; }
    public DateTime PreviousExpiresAt { get; init; }
    public required DateTime NewExpiresAt { get; init; }
    public TimeSpan RenewedDuration { get; init; }
}

/// <summary>
/// Event raised when a lock acquisition fails.
/// </summary>
public sealed class LockAcquisitionFailedEvent : LockEvent
{
    public required string LockName { get; init; }
    public required string RequesterId { get; init; }
    public required string Reason { get; init; }
    public string? DetailedError { get; init; }
}

/// <summary>
/// Event raised when there is contention for a lock.
/// Multiple parties are attempting to acquire the same lock.
/// </summary>
public sealed class LockContentionEvent : LockEvent
{
    public required string LockName { get; init; }
    public required int ContentionLevel { get; init; }
    public List<string> CompetingParties { get; init; } = new();
    public required DateTime ContentionDetectedAt { get; init; }
}

/// <summary>
/// Event raised when a lock operation takes longer than expected.
/// Could indicate performance degradation.
/// </summary>
public sealed class LockPerformanceEvent : LockEvent
{
    public required string LockId { get; init; }
    public required string OperationType { get; init; }
    public required long DurationMs { get; init; }
    public required long ThresholdMs { get; init; }
    public bool IsSlowerThanThreshold => DurationMs > ThresholdMs;
}

/// <summary>
/// Event raised when a lock operation encounters an error.
/// </summary>
public sealed class LockErrorEvent : LockEvent
{
    public required string LockId { get; init; }
    public required string OperationType { get; init; }
    public required string ErrorMessage { get; init; }
    public Exception? Exception { get; init; }
}
