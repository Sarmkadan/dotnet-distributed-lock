# LockEvent

The `LockEvent` class provides a structured representation of state changes and lifecycle events within a distributed locking system. It encapsulates essential metadata, including identification for the lock and owner, timing constraints, and the specific status of the lock operation, enabling robust auditing, observability, and debugging of distributed synchronization mechanisms.

## API

### Properties

*   `EventId` (`string`)
    A unique identifier for the specific event instance.

*   `OccurredAt` (`DateTime`)
    The timestamp indicating when the lock event occurred.

*   `SourceSystem` (`string?`)
    An optional identifier representing the originating system or node that emitted this event.

*   `CorrelationId` (`string?`)
    An optional identifier used to correlate this event with other operations across distributed systems.

*   `LockId` (`string`)
    The unique identifier of the lock involved in this event. Required property.

*   `LockName` (`string`)
    The human-readable name or resource key associated with the lock. Required property.

*   `OwnerId` (`string`)
    The unique identifier of the process, thread, or entity holding or requesting the lock. Required property.

*   `ExpiresAt` (`DateTime`)
    The absolute timestamp at which the lock is set to expire. Required property.

*   `FencingToken` (`ulong`)
    A monotonic identifier used to prevent "split-brain" scenarios by fencing against operations from stale lock holders. Required property.

*   `Duration` (`TimeSpan`)
    The total time duration associated with the lifecycle state represented by this event.

*   `Status` (`LockStatus`)
    The `LockStatus` enumeration indicating the specific type or outcome of the lock event (e.g., Acquired, Released, Expired).

### Methods

*   `ToString()` (`string`)
    Returns a string representation of the `LockEvent`, useful for logging and diagnostics.

## Usage

### Auditing Lock Transitions
```csharp
public void LogLockEvent(LockEvent lockEvent)
{
    Console.WriteLine($"[{lockEvent.OccurredAt}] Status: {lockEvent.Status} " +
                      $"| Lock: {lockEvent.LockName} | Owner: {lockEvent.OwnerId} " +
                      $"| Fencing Token: {lockEvent.FencingToken}");
}
```

### Filtering Based on Lock Owner
```csharp
public IEnumerable<LockEvent> GetEventsForOwner(IEnumerable<LockEvent> events, string ownerId)
{
    return events.Where(e => e.OwnerId == ownerId && e.Status == LockStatus.Acquired);
}
```

## Notes

*   **Thread Safety**: The `LockEvent` is designed as an immutable record or class. As such, once initialized, instances are thread-safe for read operations.
*   **Required Members**: The `LockId`, `LockName`, `OwnerId`, `ExpiresAt`, and `FencingToken` properties are required, meaning they must be initialized upon object creation to ensure the event contains complete context.
*   **Nullable Fields**: `SourceSystem` and `CorrelationId` are nullable. Consumers should implement defensive checks to handle cases where these identifiers are not provided by the originating system.
*   **Fencing Token Usage**: The `FencingToken` should be checked by downstream services to ensure that the request comes from the current, valid lock holder and not a previous, expired one.
