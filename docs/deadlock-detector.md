# DeadlockDetector

`DeadlockDetector` (`src/Core/Services/DeadlockDetector.cs`) is the default implementation of `IDeadlockDetector`. It keeps an in-memory wait-for graph of lock owners and warns when a new wait would close a circular chain. It also keeps per-key contention metrics.

The detector is advisory. It logs and counts potential deadlocks, but it does not block, cancel or fail the waiting caller.

## Data model

The detector holds four concurrent dictionaries, all using ordinal string comparison:

| Field | Maps | Meaning |
|---|---|---|
| `_ownership` | `lockKey` → `ownerId` | Current holder of each lock |
| `_waitingFor` | `ownerId` → `lockKey` | The one lock each owner is currently waiting for |
| `_metrics` | `lockKey` → `ContentionMetrics` | Per-key contention counters |

Because each owner can wait for at most one key at a time, `_waitingFor` is a functional graph. Following it from any owner yields a single path.

## Algorithm

Deadlock detection is a chain walk in `WouldDeadlock(ownerId, lockKey)`:

1. Start with `current = lockKey`.
2. Look up the holder of `current` in `_ownership`.
   - No holder: the chain ends. Return `false`.
   - Holder equals `ownerId`: a cycle exists. Increment `DeadlocksDetected` on the metrics for the requested `lockKey`, log a warning, and return `true`.
3. Add the holder to `visitedHolders`. If it was already there, stop and return `false`. This guards against infinite loops when state changes concurrently.
4. Set `current` to the key that the holder is waiting for (`_waitingFor[holder]`). If there is none, the chain ends. Return `false`.
5. Repeat from step 2.

Example: owner `A` holds `L1`. Owner `B` holds `L2` and is waiting for `L1`. If `A` now requests `L2`:

- `current = L2`, holder is `B`, `B != A`.
- `B` waits for `L1`, `current = L1`, holder is `A`, which equals `A`. Cycle detected.

A self-wait (an owner requesting a lock it already holds) is caught on the first step.

Detection runs as follows:

- `RecordWaitingAsync` calls `WouldDeadlock` and then unconditionally records the wait edge. The return value is ignored, so the wait proceeds.
- `WouldDeadlock` can also be called directly by code that wants to make its own decision. The method itself only logs and counts. It does not change any state except the metrics and the log.

## State transitions

| Method | Effect on state |
|---|---|
| `RecordWaitingAsync(ownerId, lockKey)` | Runs cycle check. Sets `_waitingFor[ownerId] = lockKey`. Calls `RecordWaiterAdded` on the key's metrics. |
| `RecordWaitEndedAsync(ownerId, lockKey, waitTimeMs)` | Removes `ownerId` from `_waitingFor`. Calls `RecordWaiterRemoved(waitTimeMs)`. Does not check whether the lock was acquired. |
| `RecordAcquired(ownerId, lockKey)` | Sets `_ownership[lockKey] = ownerId`. Removes `ownerId` from `_waitingFor`. |
| `RecordReleased(ownerId, lockKey)` | Removes `lockKey` from `_ownership` only if the current holder is `ownerId`. A release by a non-holder is a no-op. |
| `GetMetrics(lockKey)` | Returns the metrics object, or `null` if the key has never been seen. |
| `GetAllMetrics()` | Returns a snapshot list of all metrics objects. |

Notes on the metrics:

- `CurrentWaiters` and `TotalWaiters` / `TotalWaiterRegistrations` are maintained by `RecordWaiterAdded` and `RecordWaiterRemoved`.
- `DeadlocksDetected` counts cycles found while the given key was being requested.
- `AverageWaitTimeMs` is the mean of all `waitTimeMs` values passed to `RecordWaitEndedAsync` for the key. It is `0` when there are no samples.
- `ContentionMetrics` objects are created on first use and are not removed.

## Configuration

There are no tunable options. `DeadlockDetector` takes only an `ILogger<DeadlockDetector>`, which must not be `null`.

Registration is a singleton. The graph must be process-wide, otherwise different callers would not see each other's ownership and waits. Two equivalent entry points exist:

```csharp
// Via the core locking setup
services.AddDistributedLocking(opts => { /* ... */ });

// Or explicitly, for contention tracking on top of the core setup
services.AddDistributedLocking(opts => { /* ... */ })
        .AddLockContentionTracking();
```

Both register `IDeadlockDetector` as a singleton. `AddLockContentionTracking` is defined in `src/Core/Configuration/ContentionMetricsExtensions.cs`. The core setup in `src/Core/Configuration/ServiceCollectionExtensions.cs` also registers the detector, so calling both adds the same implementation twice. Resolution uses the last registration.

Log output comes from the configured logging provider:

- Debug: wait registration and wait end, per owner and key.
- Warning: each detected cycle, with owner and key.

## Usage

The detector is designed to be called around lock acquisition by the caller or by the lock service. A typical sequence:

```csharp
public class OrderProcessor(IDeadlockDetector detector, ILockService locks)
{
    public async Task ProcessAsync(string ownerId, string lockKey, TimeSpan duration, CancellationToken ct)
    {
        await detector.RecordWaitingAsync(ownerId, lockKey, ct);

        var waitStart = Stopwatch.GetTimestamp();
        var (success, _, _) = await locks.TryAcquireAsync(lockKey, ownerId, duration, ct);
        var waitMs = Stopwatch.GetElapsedTime(waitStart).TotalMilliseconds;

        await detector.RecordWaitEndedAsync(ownerId, lockKey, waitMs, ct);

        if (!success)
            return;

        detector.RecordAcquired(ownerId, lockKey);
        try
        {
            // critical section
        }
        finally
        {
            detector.RecordReleased(ownerId, lockKey);
        }
    }
}
```

Points to keep in mind:

- Call `RecordWaitingAsync` before the blocking acquire, and `RecordWaitEndedAsync` after it, whether the acquire succeeded or not.
- Call `RecordAcquired` only after the lock is actually held, and `RecordReleased` in a `finally` block so the ownership record is not left behind.
- Use the same `ownerId` string throughout. The graph is keyed on it.
- The detector does not talk to the lock backend. It only sees what the caller reports. If a caller skips a call, the graph becomes stale and detection may miss cycles or report false ones.
- Check `GetMetrics` or `GetAllMetrics` to surface contention and deadlock counts, for example from `LockMonitor` or a health endpoint.

## Limitations

- State is in process memory. Cycles that span multiple processes or replicas are not visible unless each replica reports into a shared detector, which this implementation does not provide.
- Only one holder per key is tracked. `RecordAcquired` overwrites any previous holder for the key.
- Detection is point-in-time. Concurrent updates can cause a cycle to be missed or a stale one to be reported. The visited-holder guard prevents hangs, not inaccuracy.
- The `CancellationToken` parameters are accepted but not observed. The operations complete immediately.
- Metrics and wait records accumulate per key and are never evicted. Workloads with very many distinct lock keys will grow memory use.
- The detector never refuses a wait. Acting on a `true` result from `WouldDeadlock` is up to the caller.

## Tests

`tests/dotnet-distributed-lock.Tests/DeadlockDetectorTests.cs` covers the cycle detection paths and metric updates. See `docs/DeadlockDetectorTests.md` for the test list.

## See also

- `docs/IDeadlockDetector.md` - interface reference
- `docs/ContentionMetrics.md` - metrics object
- `docs/LockMonitor.md` - consumer of contention data
