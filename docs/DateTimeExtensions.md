# DateTimeExtensions

Provides a collection of utility extension methods for `DateTime` and `TimeSpan` values, primarily focused on expiration checking, time-remaining calculations, formatting, rounding, jitter application, and Unix timestamp conversions. These methods are designed to support distributed lock scenarios where precise time handling, monotonic evaluation, and human-readable diagnostics are required.

## API

### IsExpired

```csharp
public static bool IsExpired(this DateTime expirationTime)
```

Determines whether the specified expiration time has already passed relative to the current UTC time.

**Parameters**
- `expirationTime` — The `DateTime` to evaluate. Typically represents a lock lease expiry.

**Return Value**
`true` if `expirationTime` is less than or equal to `DateTime.UtcNow`; otherwise `false`.

**Exceptions**
None.

---

### IsValid

```csharp
public static bool IsValid(this DateTime timestamp)
```

Checks whether a `DateTime` represents a meaningful, non-default value that can be used in time calculations.

**Parameters**
- `timestamp` — The `DateTime` to validate.

**Return Value**
`true` if the timestamp is not `DateTime.MinValue` and not `DateTime.MaxValue`; otherwise `false`.

**Exceptions**
None.

---

### GetRemainingTime

```csharp
public static TimeSpan GetRemainingTime(this DateTime expirationTime)
```

Calculates the time remaining until the given expiration time from the current UTC moment.

**Parameters**
- `expirationTime` — The target expiration `DateTime`.

**Return Value**
A `TimeSpan` representing the duration from `DateTime.UtcNow` to `expirationTime`. Returns `TimeSpan.Zero` or a negative value if the expiration time has already passed.

**Exceptions**
None.

---

### GetRemainingSeconds

```csharp
public static long GetRemainingSeconds(this DateTime expirationTime)
```

Returns the remaining time until expiration expressed as whole seconds.

**Parameters**
- `expirationTime` — The target expiration `DateTime`.

**Return Value**
The integer number of seconds remaining, computed from `GetRemainingTime` and rounded down. May be zero or negative if expired.

**Exceptions**
None.

---

### GetRemainingMilliseconds

```csharp
public static long GetRemainingMilliseconds(this DateTime expirationTime)
```

Returns the remaining time until expiration expressed as whole milliseconds.

**Parameters**
- `expirationTime` — The target expiration `DateTime`.

**Return Value**
The integer number of milliseconds remaining, computed from `GetRemainingTime` and rounded down. May be zero or negative if expired.

**Exceptions**
None.

---

### ExpiresWithin

```csharp
public static bool ExpiresWithin(this DateTime expirationTime, TimeSpan window)
```

Determines whether the expiration time falls within a specified time window from now.

**Parameters**
- `expirationTime` — The expiration `DateTime` to check.
- `window` — A `TimeSpan` representing the threshold duration.

**Return Value**
`true` if `GetRemainingTime(expirationTime)` is greater than or equal to `TimeSpan.Zero` and less than or equal to `window`; otherwise `false`.

**Exceptions**
None.

---

### ToIso8601String

```csharp
public static string ToIso8601String(this DateTime dateTime)
```

Formats the `DateTime` as a sortable ISO 8601 string in UTC.

**Parameters**
- `dateTime` — The `DateTime` to format.

**Return Value**
A string representation using the round-trip format specifier `"O"` (e.g., `2025-03-15T14:30:00.0000000Z`).

**Exceptions**
None.

---

### ToHumanReadableFormat

```csharp
public static string ToHumanReadableFormat(this DateTime dateTime)
```

Formats the `DateTime` as a human-friendly string including the local time and a UTC offset indicator.

**Parameters**
- `dateTime` — The `DateTime` to format.

**Return Value**
A string combining a short date/time pattern with an explicit `(UTC)` marker when the kind is UTC, or the local time with timezone offset otherwise.

**Exceptions**
None.

---

### RoundToNearest

```csharp
public static DateTime RoundToNearest(this DateTime dateTime, TimeSpan interval)
```

Rounds the given `DateTime` to the nearest multiple of the specified interval.

**Parameters**
- `dateTime` — The `DateTime` to round.
- `interval` — A positive `TimeSpan` representing the rounding granularity.

**Return Value**
A new `DateTime` whose ticks are rounded to the nearest multiple of `interval.Ticks`. Midpoint values round up.

**Exceptions**
- `ArgumentException` — Thrown when `interval` is less than or equal to `TimeSpan.Zero`.

---

### AddRandomJitter

```csharp
public static TimeSpan AddRandomJitter(this TimeSpan baseTimeSpan, TimeSpan maxJitter)
```

Adds a uniformly distributed random jitter to a base `TimeSpan`, bounded by the specified maximum.

**Parameters**
- `baseTimeSpan` — The original `TimeSpan` to which jitter is added.
- `maxJitter` — The maximum amount of jitter that can be added. Must be non-negative.

**Return Value**
A new `TimeSpan` equal to `baseTimeSpan` plus a random offset between zero and `maxJitter`.

**Exceptions**
- `ArgumentOutOfRangeException` — Thrown when `maxJitter` is negative.

---

### FromUnixTimestamp

```csharp
public static DateTime FromUnixTimestamp(this long unixTimestamp)
```

Converts a Unix timestamp (seconds since the Unix epoch) to a UTC `DateTime`.

**Parameters**
- `unixTimestamp` — A `long` representing seconds elapsed since 1970-01-01T00:00:00Z.

**Return Value**
A `DateTime` of kind `Utc` corresponding to the given timestamp.

**Exceptions**
None. Values outside the representable `DateTime` range may produce `DateTime.MinValue` or `DateTime.MaxValue`.

---

### ToUnixTimestamp

```csharp
public static long ToUnixTimestamp(this DateTime dateTime)
```

Converts a `DateTime` to a Unix timestamp expressed in seconds.

**Parameters**
- `dateTime` — The `DateTime` to convert. It is internally normalized to UTC for the calculation.

**Return Value**
A `long` representing the number of seconds between the Unix epoch and the given time. Fractional seconds are truncated.

**Exceptions**
None.

## Usage

### Example 1: Checking and Extending a Lock Lease

```csharp
DateTime lockExpiry = DateTime.UtcNow.AddSeconds(30);

// Later, before performing a critical operation...
if (lockExpiry.IsExpired())
{
    throw new InvalidOperationException("Lock has expired.");
}

if (lockExpiry.ExpiresWithin(TimeSpan.FromSeconds(5)))
{
    // Renew the lease with jitter to avoid thundering herd
    TimeSpan extension = TimeSpan.FromSeconds(30)
        .AddRandomJitter(TimeSpan.FromMilliseconds(500));
    lockExpiry = DateTime.UtcNow.Add(extension);
    Console.WriteLine($"Lease extended to {lockExpiry.ToIso8601String()}");
}
```

### Example 2: Diagnostics and Scheduling

```csharp
DateTime nextRun = DateTime.UtcNow.AddHours(1).RoundToNearest(TimeSpan.FromMinutes(15));

long unixSchedule = nextRun.ToUnixTimestamp();
DateTime restored = unixSchedule.FromUnixTimestamp();

Console.WriteLine($"Next run: {restored.ToHumanReadableFormat()}");
Console.WriteLine($"Unix timestamp for scheduler: {unixSchedule}");

TimeSpan remaining = nextRun.GetRemainingTime();
Console.WriteLine($"Time until next run: {remaining.TotalSeconds:F0} seconds");
```

## Notes

- All expiration and remaining-time methods compare against `DateTime.UtcNow` at the moment of invocation. They do not accept an arbitrary reference time, so callers must account for clock drift or changes between successive calls.
- `IsValid` treats `DateTime.MinValue` and `DateTime.MaxValue` as sentinel values. A `DateTime` initialized to `default` (which equals `DateTime.MinValue`) is considered invalid.
- `GetRemainingTime`, `GetRemainingSeconds`, and `GetRemainingMilliseconds` can return zero or negative values when the expiration time has passed. Callers should guard against negative durations where a positive interval is required.
- `RoundToNearest` uses tick-level arithmetic. For intervals that do not evenly divide into `TimeSpan.TicksPerDay`, rounding near the upper boundary of `DateTime` may produce `DateTime.MaxValue`.
- `AddRandomJitter` relies on `System.Random` and is not suitable for cryptographic randomness. It is designed for contention reduction in distributed timers and lock renewal scheduling.
- `FromUnixTimestamp` and `ToUnixTimestamp` operate at second granularity. Sub-second precision is lost during conversion. Values outside the `DateTime` representable range clamp to `MinValue` or `MaxValue`.
- None of the methods maintain internal mutable state. All are static extension methods and are safe to call concurrently from multiple threads without external synchronization.
