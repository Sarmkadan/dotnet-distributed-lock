# LockEventExtensions

Provides extension methods for inspecting and converting lock lifecycle events. These methods enable uniform access to common properties across different lock event types, facilitating logging, monitoring, and diagnostic workflows without pattern matching on concrete event types.

## API

### `IsAcquisitionSuccessful`

```csharp
public static bool IsAcquisitionSuccessful(this LockEvent @event)
```

Determines whether the event represents a successful lock acquisition.

**Parameters**
- `@event` — The lock event to evaluate.

**Returns**
`true` if the event indicates the lock was acquired; `false` otherwise (including for release, failure, or expiration events).

**Exceptions**
Throws `ArgumentNullException` if `@event` is `null`.

---

### `IsFailure`

```csharp
public static bool IsFailure(this LockEvent @event)
```

Determines whether the event represents a lock acquisition failure.

**Parameters**
- `@event` — The lock event to evaluate.

**Returns**
`true` if the event is a `LockFailedEvent`; `false` otherwise.

**Exceptions**
Throws `ArgumentNullException` if `@event` is `null`.

---

### `GetLockId`

```csharp
public static string? GetLockId(this LockEvent @event)
```

Retrieves the identifier of the lock associated with the event.

**Parameters**
- `@event` — The lock event to query.

**Returns**
The lock identifier, or `null` if the event does not carry a lock ID.

**Exceptions**
Throws `ArgumentNullException` if `@event` is `null`.

---

### `GetOwnerId`

```csharp
public static string? GetOwnerId(this LockEvent @event)
```

Retrieves the identifier of the lock owner (client) associated with the event.

**Parameters**
- `@event` — The lock event to query.

**Returns**
The owner identifier, or `null` if the event does not carry an owner ID.

**Exceptions**
Throws `ArgumentNullException` if `@event` is `null`.

---

### `ToLogString`

```csharp
public static string ToLogString(this LockEvent @event)
```

Produces a structured, human-readable string representation of the event suitable for logging.

**Parameters**
- `@event` — The lock event to format.

**Returns**
A formatted string containing the event type, lock ID, owner ID, timestamp, and relevant outcome details.

**Exceptions**
Throws `ArgumentNullException` if `@event` is `null`.

---

### `ToFailureEvent`

```csharp
public static LockFailedEvent ToFailureEvent(this LockEvent @event)
```

Casts the event to a `LockFailedEvent`, providing access to failure-specific details.

**Parameters**
- `@event` — The lock event to cast.

**Returns**
The event as a `LockFailedEvent`.

**Exceptions**
Throws `ArgumentNullException` if `@event` is `null`.
Throws `InvalidCastException` if the event is not a `LockFailedEvent`.

---

### `IsWithinTimeRange`

```csharp
public static bool IsWithinTimeRange(this LockEvent @event, DateTime start, DateTime end)
```

Checks whether the event's timestamp falls within the specified inclusive time range.

**Parameters**
- `@event` — The lock event to check.
- `start` — The inclusive start of the time range (UTC).
- `end` — The inclusive end of the time range (UTC).

**Returns**
`true` if the event timestamp is between `start` and `end` (inclusive); `false` otherwise.

**Exceptions**
Throws `ArgumentNullException` if `@event` is `null`.
Throws `ArgumentException` if `start > end`.

---

### `GetDuration`

```csharp
public static TimeSpan GetDuration(this LockEvent @event)
```

Calculates the duration the lock was held, based on the event's timestamp and acquisition time.

**Parameters**
- `@event` — The lock event to measure.

**Returns**
A `TimeSpan` representing the hold duration. Returns `TimeSpan.Zero` for events that do not represent a held lock (e.g., acquisition failures).

**Exceptions**
Throws `ArgumentNullException` if `@event` is `null`.

---

### `GetExpirationTime`

```csharp
public static DateTime? GetExpirationTime(this LockEvent @event)
```

Retrieves the lock's expiration time as recorded in the event.

**Parameters**
- `@event` — The lock event to query.

**Returns**
The expiration time in UTC, or `null` if the event does not contain expiration information.

**Exceptions**
Throws `ArgumentNullException` if `@event` is `null`.

---

### `IsRelatedToLock`

```csharp
public static bool IsRelatedToLock(this LockEvent @event, string lockId)
```

Determines whether the event pertains to the specified lock identifier.

**Parameters**
- `@event` — The lock event to check.
- `lockId` — The lock identifier to match against.

**Returns**
`true` if the event's lock ID matches `lockId` (ordinal comparison); `false` otherwise or if the event has no lock ID.

**Exceptions**
Throws `ArgumentNullException` if `@event` is `null`.
Throws `ArgumentNullException` if `lockId` is `null`.

---

### `GetFencingToken`

```csharp
public static string? GetFencingToken(this LockEvent @event)
```

Retrieves the fencing token associated with the lock acquisition, if present.

**Parameters**
- `@event` — The lock event to query.

**Returns**
The fencing token as a string, or `null` if the event does not carry a fencing token (e.g., non-acquisition events or locks without fencing).

**Exceptions**
Throws `ArgumentNullException` if `@event` is `null`.

## Usage

### Filtering and logging events in a monitoring pipeline

```csharp
public async Task ProcessEventsAsync(IAsyncEnumerable<LockEvent> events, ILogger logger)
{
    await foreach (var evt in events)
    {
        if (evt.IsAcquisitionSuccessful())
        {
            var duration = evt.GetDuration();
            var token = evt.GetFencingToken();
            logger.LogInformation(
                "Lock {LockId} acquired by {OwnerId} (duration: {Duration}, token: {Token})",
                evt.GetLockId(), evt.GetOwnerId(), duration, token ?? "none");
        }
        else if (evt.IsFailure())
        {
            var failure = evt.ToFailureEvent();
            logger.LogWarning(
                "Lock {LockId} acquisition failed for {OwnerId}: {Reason}",
                evt.GetLockId(), evt.GetOwnerId(), failure.FailureReason);
        }
        else
        {
            logger.LogDebug("Lock event: {Event}", evt.ToLogString());
        }
    }
}
```

### Querying events within a time window for audit reporting

```csharp
public IEnumerable<LockEvent> GetLockHistory(
    IEnumerable<LockEvent> allEvents,
    string lockId,
    DateTime windowStart,
    DateTime windowEnd)
{
    return allEvents
        .Where(e => e.IsRelatedToLock(lockId))
        .Where(e => e.IsWithinTimeRange(windowStart, windowEnd))
        .OrderBy(e => e.Timestamp);
}

// Usage
var history = GetLockHistory(events, "resource-42", DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);
foreach (var evt in history)
{
    Console.WriteLine($"{evt.Timestamp:O} | {evt.ToLogString()}");
}
```

## Notes

- All methods throw `ArgumentNullException` when the `LockEvent` parameter is `null`, ensuring fail-fast behavior in pipelines.
- `GetDuration` returns `TimeSpan.Zero` for non-acquisition events rather than throwing, allowing safe use in LINQ projections without filtering.
- `GetFencingToken` returns `null` for events that do not represent successful acquisitions with fencing enabled; callers should not assume a token is present.
- `IsWithinTimeRange` uses inclusive bounds and expects UTC `DateTime` values; passing local or unspecified kinds may produce incorrect results.
- The extensions are pure functions with no shared state, making them inherently thread-safe for concurrent use across multiple threads.
- `ToFailureEvent` performs a runtime cast; prefer checking `IsFailure()` first to avoid `InvalidCastException` in hot paths.
