# IDeadlockDetector

Interface that tracks lock acquisition attempts across distributed resources to detect potential deadlocks before they occur. Implementations maintain an in‑memory wait‑for graph and expose methods for recording lock lifecycle events and querying the current contention state.

## API

### DeadlockDetector  
**Type:** `DeadlockDetector` (read‑only property)  
**Purpose:** Provides access to the concrete detector instance used for internal tracking. Consumers can inspect or replace the underlying detector if needed.  
**Parameters:** None.  
**Return value:** The `DeadlockDetector` object that backs this interface.  
**Exceptions:** None.

### RecordWaitingAsync  
**Signature:** `Task RecordWaitingAsync(string resourceId, string ownerId, CancellationToken cancellationToken = default)`  
**Purpose:** Asynchronously records that `ownerId` is waiting to acquire the lock on `resourceId`. This adds a wait edge to the internal graph.  
**Parameters:**  
- `resourceId` – Identifier of the lock being waited on. Must not be `null` or whitespace.  
- `ownerId` – Identifier of the entity (e.g., thread, process, or replica) that is waiting. Must not be `null` or whitespace.  
- `cancellationToken` – Optional token to cancel the operation.  
**Return value:** A `Task` that completes when the wait has been recorded.  
**Exceptions:**  
- `ArgumentNullException` – If `resourceId` or `ownerId` is `null`.  
- `ArgumentException` – If either identifier is empty or consists only of whitespace.  
- `ObjectDisposedException` – If the detector has been disposed.  
- `OperationCanceledException` – If the supplied `cancellationToken` is triggered.

### RecordWaitEndedAsync  
**Signature:** `Task RecordWaitEndedAsync(string resourceId, string ownerId, bool acquired, CancellationToken cancellationToken = default)`  
**Purpose:** Asynchronously records the end of a wait attempt. If `acquired` is `true`, the wait is converted to an ownership edge; otherwise the wait is removed.  
**Parameters:**  
- `resourceId` – Identifier of the lock the wait was for.  
- `ownerId` – Identifier of the entity that was waiting.  
- `acquired` – `true` if the lock was successfully obtained, `false` if the wait was abandoned or timed out.  
- `cancellationToken` – Optional token to cancel the operation.  
**Return value:** A `Task` that completes when the wait end has been recorded.  
**Exceptions:** Same as `RecordWaitingAsync`, plus:  
- `InvalidOperationException` – If no matching wait record exists for the given `resourceId`/`ownerId` pair.

### RecordAcquired  
**Signature:** `void RecordAcquired(string resourceId, string ownerId)`  
**Purpose:** Synchronously records that `ownerId` has acquired the lock on `resourceId`. This should be called after a successful wait has been observed (or when a lock is taken optimistically).  
**Parameters:**  
- `resourceId` – Identifier of the acquired lock.  
- `ownerId` – Identifier of the acquiring entity.  
**Return value:** None.  
**Exceptions:**  
-  
  If either identifier is null or whitespace.  
- `InvalidOperationException – If there is no pending wait for the given pair, indicating an inconsistent state.

### RecordReleased  
**Signature:** `void RecordReleased(string resourceId, string ownerId)`  
**Purpose:** Synchronously records that `ownerId` has released the lock on `resourceId`, removing the ownership edge from the graph.  
**Parameters:**  
- `resourceId` – Identifier of the released lock.  
- `ownerId` – Identifier of the releasing entity.  
**Return value:** None.  
**Exceptions:**  
- `ArgumentNullException` – If either identifier is `null`.  
- `ArgumentException` – If either identifier is empty or whitespace.  
- `InvalidOperationException` – If the lock is not currently recorded as held by `ownerId`.

### WouldDeadlock  
**Type:** `bool` (read‑only property)  
**Purpose:** Indicates whether recording an additional wait for the most recently tracked lock would introduce a cycle in the wait‑for graph, i.e., cause a deadlock. Consumers can query this before committing to a wait to avoid entering a deadlocked state.  
**Parameters:** None.  
**Return value:** `true` if a deadlock would occur; otherwise `false`.  
**Exceptions:** None.

### GetMetrics  
**Signature:** `ContentionMetrics? GetMetrics(string resourceId)`  
**Purpose:** Retrieves contention metrics for a specific lock, such as the number of waiters, average wait time, and owner information. Returns `null` if no metrics are tracked for the given resource.  
**Parameters:**  
- `resourceId` – Identifier of the lock to query.  
**Return value:** A `ContentionMetrics` instance containing current statistics, or `null` if none exist.  
**Exceptions:**  
- `ArgumentNullException` – If `resourceId` is `null`.  
- `ArgumentException` – If `resourceId` is empty or whitespace.

### GetAllMetrics  
**Signature:** `IReadOnlyCollection<ContentionMetrics> GetAllMetrics()`  
**Purpose:** Returns a snapshot of contention metrics for all locks currently being tracked. The collection is read‑only and reflects the state at the moment of invocation.  
**Parameters:** None.  
**Return value:** A collection of `ContentionMetrics` objects, one per tracked lock. Empty collection if no locks are being monitored.  
**Exceptions:**  
- `ObjectDisposedException` – If the detector has been disposed.  

## Usage

### Example 1: Basic lock acquisition with deadlock avoidance
```csharp
async Task<bool> TryAcquireLockAsync(IDeadlockDetector detector, string lockId, string ownerId)
{
    // Record that we are about to wait.
    await detector.RecordWaitingAsync(lockId, ownerId);

    // If recording this wait would cause a deadlock, abort early.
    if (detector.WouldDeadlock)
    {
        await detector.RecordWaitEndedAsync(lockId, ownerId, acquired: false);
        return false;
    }

    // Attempt to acquire the actual distributed lock (pseudo‑code).
    bool acquired = await DistributedLock.TryAcquireAsync(lockId);

    // Record the outcome of the wait.
    await detector.RecordWaitEndedAsync(lockId, ownerId, acquired: acquired);

    if (acquired)
    {
        detector.RecordAcquired(lockId, ownerId);
    }
    else
    {
        // No ownership recorded because we failed to acquire.
    }

    return acquired;
}
```

### Example 2: Reporting contention metrics after a workload
```csharp
void ReportMetrics(IDeadlockDetector detector)
{
    var allMetrics = detector.GetAllMetrics();
    foreach (var metrics in allMetrics)
    {
        Console.WriteLine(
            $"Lock {metrics.ResourceId}: Owner={metrics.CurrentOwner}, " +
            $"Waiters={metrics.WaiterCount}, AvgWaitMs={metrics.AverageWaitTime.TotalMilliseconds:F1}");
    }

    // Example of drilling down into a specific lock.
    var specific = detector.GetMetrics("payment-service-lock");
    if (specific != null)
    {
        Console.WriteLine(
            $"Detailed metrics for payment-service-lock: MaxWait={specific.MaxWaitTime}");
    }
}
```

## Notes

- **Thread safety:** All members of `IDeadlockDetector` are intended to be called concurrently from multiple threads. Implementations should guard internal state with appropriate synchronization primitives (e.g., `ReaderWriterLockSlim` or immutable data structures) to prevent race conditions.  
- **State consistency:** Callers must pair each call to `RecordWaitingAsync` with a corresponding call to `RecordWaitEndedAsync` (whether the lock was acquired or not). Failure to do so will leave the detector in an inconsistent state and may cause `InvalidOperationException` on subsequent calls.  
- **Resource lifetimes:** The detector may hold references to lock identifiers until `RecordReleased` is invoked for the corresponding owner. Long‑lived waits without a release can cause memory leaks; consider timing out abandoned waits and calling `RecordWaitEndedAsync` with `acquired = false`.  
- **Disposal:** If the underlying `DeadlockDetector` implements `IDisposable`, calling `Dispose` renders all further calls invalid and will throw `ObjectDisposedException`. Consumers should dispose the detector when lock monitoring is no longer needed (e.g., at application shutdown).  
- **WouldDeadlock semantics:** The property evaluates the graph *as it stands* after the most recent `RecordWaitingAsync` call but before the wait is officially recorded. It does **not** consider future lock acquisitions that have not yet been signaled via `RecordAcquired`. Therefore, a `false` result does not guarantee that a deadlock cannot occur later; it only indicates that adding the current wait edge would not immediately create a cycle.  
- **Metrics granularity:** `ContentionMetrics` is a snapshot; values may change immediately after the method returns. For accurate trending, sample metrics at regular intervals and compute deltas manually.  
- **Exception policy:** Validation exceptions (`ArgumentNullException`, `ArgumentException`) are thrown synchronously before any asynchronous work begins, allowing callers to handle them without awaiting the returned task. Asynchronous methods only throw exceptions related to cancellation or object disposal through the returned task.
