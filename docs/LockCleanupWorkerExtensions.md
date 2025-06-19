# LockCleanupWorkerExtensions

Provides extension methods for configuring, executing, and inspecting the lock cleanup worker in the `dotnet-distributed-lock` library. These extensions support both production monitoring (with timeout enforcement) and testing scenarios (with configurable intervals), while exposing statistics about expired lock cleanup operations.

## API

### RunCleanupOnceAsyncWithTimeout

```csharp
public static async Task RunCleanupOnceAsyncWithTimeout(
    this LockCleanupWorker worker,
    TimeSpan timeout)
```

Executes a single cleanup pass on the worker, enforcing a maximum duration via the specified timeout. If the cleanup operation exceeds the timeout, the underlying task will be abandoned (though the worker's internal state may continue processing). Designed for scenarios where cleanup must not block the caller indefinitely.

**Parameters:**
- `worker` — the `LockCleanupWorker` instance to operate on.
- `timeout` — the maximum time allowed for the cleanup pass.

**Returns:** a `Task` that completes when the cleanup finishes or the timeout expires.

**Throws:** `TimeoutException` if the cleanup does not complete within the specified timeout.

---

### RunCleanupOnceAsyncWithStats

```csharp
public static async Task<(TimeSpan Duration, int CleanedCount)> RunCleanupOnceAsyncWithStats(
    this LockCleanupWorker worker)
```

Executes a single cleanup pass and returns timing and count statistics. The method measures the wall-clock duration of the operation and captures the number of expired locks that were cleaned in that pass.

**Parameters:**
- `worker` — the `LockCleanupWorker` instance to operate on.

**Returns:** a tuple containing:
- `Duration` — the elapsed time the cleanup pass took.
- `CleanedCount` — the number of expired lock records removed during this pass.

**Throws:** no additional exceptions beyond those propagated from the underlying cleanup logic (e.g., storage access failures).

---

### GetCleanedCount

```csharp
public static int GetCleanedCount(this LockCleanupWorker worker)
```

Retrieves the total number of expired locks cleaned by the worker across all cleanup passes since it was started or since the counter was last reset.

**Parameters:**
- `worker` — the `LockCleanupWorker` instance to query.

**Returns:** the cumulative cleaned count as an `int`.

**Throws:** does not throw under normal operation.

---

### WithTestInterval

```csharp
public static LockCleanupWorker WithTestInterval(
    this LockCleanupWorker worker,
    TimeSpan interval)
```

Returns a new `LockCleanupWorker` instance (or reconfigures the existing one) with a custom cleanup interval. Intended for unit testing and integration testing where the default production interval is too long; allows tests to trigger frequent cleanups without waiting for real-time delays.

**Parameters:**
- `worker` — the `LockCleanupWorker` instance to reconfigure or clone.
- `interval` — the desired interval between automatic cleanup passes.

**Returns:** a `LockCleanupWorker` configured with the specified interval.

**Throws:** `ArgumentOutOfRangeException` if the interval is negative or zero.

---

### LogConfiguration

```csharp
public static void LogConfiguration(this LockCleanupWorker worker)
```

Writes the current configuration of the worker (interval, timeout settings, and any other relevant parameters) to the configured logging output. Useful for diagnostics and confirming settings in both production and test environments.

**Parameters:**
- `worker` — the `LockCleanupWorker` whose configuration should be logged.

**Returns:** void.

**Throws:** does not throw; if logging is unavailable, the call is a no-op.

## Usage

### Example 1: Production monitoring with timeout and statistics

```csharp
using DistributedLock;

var worker = new LockCleanupWorker(/* storage provider */);

// Log current configuration for diagnostics
worker.LogConfiguration();

// Run a cleanup pass with a hard timeout of 30 seconds
try
{
    await worker.RunCleanupOnceAsyncWithTimeout(TimeSpan.FromSeconds(30));
}
catch (TimeoutException)
{
    Console.WriteLine("Cleanup timed out; locks may remain stale temporarily.");
}

// Later, run a pass and capture stats for telemetry
var (duration, cleaned) = await worker.RunCleanupOnceAsyncWithStats();
Console.WriteLine($"Cleanup took {duration.TotalMilliseconds}ms, removed {cleaned} locks.");

// Check cumulative count
int totalCleaned = worker.GetCleanedCount();
Console.WriteLine($"Total cleaned since start: {totalCleaned}");
```

### Example 2: Testing with accelerated intervals

```csharp
using DistributedLock;
using Xunit;

[Fact]
public async Task Cleanup_RemovesExpiredLocks_WithinShortInterval()
{
    var worker = new LockCleanupWorker(/* test storage provider */)
        .WithTestInterval(TimeSpan.FromMilliseconds(100));

    // Seed some expired locks in storage
    await SeedExpiredLocksAsync(5);

    // Allow the worker to run at least one cycle
    await Task.Delay(300);

    // Trigger an explicit pass and verify
    var (_, cleaned) = await worker.RunCleanupOnceAsyncWithStats();
    Assert.True(cleaned >= 5, "Expected at least 5 expired locks to be cleaned.");
    Assert.Equal(cleaned, worker.GetCleanedCount());
}
```

## Notes

- **Thread safety:** `GetCleanedCount` reads a cumulative counter that may be updated concurrently by background cleanup cycles. The returned value is a point-in-time snapshot and may be stale immediately after reading. No locking is performed by the extension methods themselves; thread safety depends on the underlying worker implementation.
- **Timeout behavior:** `RunCleanupOnceAsyncWithTimeout` enforces a timeout on the caller's wait, but does not cancel the underlying cleanup operation. If the timeout expires, the cleanup may continue in the background, and subsequent calls to `GetCleanedCount` or `RunCleanupOnceAsyncWithStats` may reflect partial results from the abandoned pass.
- **`WithTestInterval`:** This method is designed exclusively for testing. Using extremely short intervals in production may cause excessive storage access and degrade performance. The method may return a new instance rather than mutating the original, depending on the worker's design; callers should use the returned reference.
- **Statistics accuracy:** `RunCleanupOnceAsyncWithStats` measures duration from the caller's perspective, including any scheduling delays. The `CleanedCount` reflects only the locks removed during that specific pass, not the cumulative total.
- **Logging dependency:** `LogConfiguration` relies on the worker having an initialized logging sink. If logging is not configured, the call silently does nothing.
