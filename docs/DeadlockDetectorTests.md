# DeadlockDetectorTests

`DeadlockDetectorTests` is the unit test suite for the `DeadlockDetector` class in the `dotnet-distributed-lock` project. It validates the correctness of deadlock detection logic, ownership tracking, waiter registration, metrics collection, and thread-safety guarantees under concurrent workloads. The tests cover both synchronous and asynchronous code paths, ensuring that the detector correctly identifies circular wait conditions, handles null arguments with appropriate exceptions, and maintains internal consistency when multiple threads record waiting and acquisition events simultaneously.

## API

### public void Constructor_WithNullLogger_ThrowsArgumentNullException
Verifies that constructing a `DeadlockDetector` with a `null` logger argument throws an `ArgumentNullException`.  
**Parameters:** None (test method).  
**Returns:** void.  
**Throws:** `ArgumentNullException` (expected in the constructor under test).

### public void WouldDeadlock_WithNoExistingOwnership_ReturnsFalse
Ensures that `WouldDeadlock` returns `false` when no lock ownership records exist, meaning no circular wait can possibly be formed.  
**Parameters:** None (test method).  
**Returns:** void.

### public async Task WouldDeadlock_WithSimpleCircularWait_ReturnsTrue
Confirms that `WouldDeadlock` returns `true` for a minimal two-lock circular dependency (A waits for B, B waits for A).  
**Parameters:** None (test method).  
**Returns:** `Task` representing the asynchronous test operation.

### public void WouldDeadlock_WithoutCircularWait_ReturnsFalse
Validates that `WouldDeadlock` returns `false` when wait chains exist but do not form a cycle.  
**Parameters:** None (test method).  
**Returns:** void.

### public async Task WouldDeadlock_WithLongerChain_DetectsDeadlock
Tests deadlock detection across a chain of three or more locks forming a cycle, ensuring the traversal algorithm correctly identifies indirect circular waits.  
**Parameters:** None (test method).  
**Returns:** `Task` representing the asynchronous test operation.

### public async Task RecordWaitingAsync_WithNullOwnerId_ThrowsArgumentNullException
Asserts that calling `RecordWaitingAsync` with a `null` owner ID throws an `ArgumentNullException`.  
**Parameters:** None (test method).  
**Returns:** `Task`.  
**Throws:** `ArgumentNullException` (expected).

### public async Task RecordWaitingAsync_WithNullLockKey_ThrowsArgumentNullException
Asserts that calling `RecordWaitingAsync` with a `null` lock key throws an `ArgumentNullException`.  
**Parameters:** None (test method).  
**Returns:** `Task`.  
**Throws:** `ArgumentNullException` (expected).

### public async Task RecordWaitingAsync_TracksWaiter
Verifies that after `RecordWaitingAsync` is invoked, the waiter is correctly registered and appears in the detector’s internal waiter tracking structures.  
**Parameters:** None (test method).  
**Returns:** `Task`.

### public void RecordAcquired_RegistersOwnership
Confirms that calling `RecordAcquired` adds the specified owner as the current holder of the given lock key.  
**Parameters:** None (test method).  
**Returns:** void.

### public void RecordReleased_ClearsOwnership
Ensures that `RecordReleased` removes the ownership record for a lock when the releasing owner matches the recorded owner.  
**Parameters:** None (test method).  
**Returns:** void.

### public void RecordReleased_WithWrongOwner_DoesNotRemove
Validates that `RecordReleased` does not clear ownership if the owner attempting to release the lock differs from the currently recorded owner.  
**Parameters:** None (test method).  
**Returns:** void.

### public async Task GetMetrics_WithNoData_ReturnsNull
Checks that `GetMetrics` returns `null` (or an equivalent empty state) when no waiters or acquisitions have been recorded.  
**Parameters:** None (test method).  
**Returns:** `Task`.

### public async Task GetMetrics_AfterWaiterAdded_ReturnsMetrics
Verifies that `GetMetrics` returns a valid metrics object after at least one waiter has been recorded.  
**Parameters:** None (test method).  
**Returns:** `Task`.

### public async Task GetMetrics_AfterDeadlockDetected_IncrementsCounter
Ensures that the deadlock detection counter in the metrics is incremented after a deadlock is successfully identified.  
**Parameters:** None (test method).  
**Returns:** `Task`.

### public async Task GetAllMetrics_ReturnsAllTrackedLocks
Confirms that `GetAllMetrics` returns metrics entries for every lock key currently being tracked by the detector.  
**Parameters:** None (test method).  
**Returns:** `Task`.

### public async Task RecordWaitEndedAsync_RemovesWaiter
Validates that invoking `RecordWaitEndedAsync` removes the corresponding waiter from the internal tracking structures.  
**Parameters:** None (test method).  
**Returns:** `Task`.

### public async Task RecordWaitEndedAsync_RecordsWaitTime
Ensures that `RecordWaitEndedAsync` captures the elapsed wait time for the waiter and includes it in the relevant metrics.  
**Parameters:** None (test method).  
**Returns:** `Task`.

### public async Task RecordWaitEndedAsync_MultipleWaits_CalculatesAverageWaitTime
Verifies that when multiple wait periods are recorded and ended, the detector correctly computes the average wait time across all recorded waits.  
**Parameters:** None (test method).  
**Returns:** `Task`.

### public async Task ConcurrentWaitingAndAcquisition_MaintainsConsistency
Stress-tests the detector under concurrent calls to `RecordWaitingAsync`, `RecordAcquired`, `RecordReleased`, and `WouldDeadlock`, asserting that no internal state corruption, race conditions, or inconsistent deadlock results occur.  
**Parameters:** None (test method).  
**Returns:** `Task`.

## Usage

### Example 1: Basic deadlock detection workflow
```csharp
[Test]
public async Task DeadlockDetector_IntegrationScenario_DetectsAndReports()
{
    var logger = new NullLogger<DeadlockDetector>();
    var detector = new DeadlockDetector(logger);

    // Simulate lock acquisition
    detector.RecordAcquired("resource-A", "owner-1");
    detector.RecordAcquired("resource-B", "owner-2");

    // owner-1 now waits for resource-B
    await detector.RecordWaitingAsync("resource-B", "owner-1");

    // owner-2 now waits for resource-A — this completes the cycle
    bool wouldDeadlock = await detector.WouldDeadlock("resource-A", "owner-2");
    Assert.That(wouldDeadlock, Is.True);

    // Metrics should reflect the detected deadlock
    var metrics = await detector.GetMetrics("resource-A");
    Assert.That(metrics.DeadlocksDetected, Is.GreaterThan(0));
}
```

### Example 2: Concurrent stress test pattern
```csharp
[Test]
public async Task DeadlockDetector_HighConcurrency_RemainsConsistent()
{
    var logger = new NullLogger<DeadlockDetector>();
    var detector = new DeadlockDetector(logger);
    var tasks = new List<Task>();
    var barrier = new Barrier(4);

    for (int i = 0; i < 4; i++)
    {
        int ownerId = i;
        tasks.Add(Task.Run(async () =>
        {
            barrier.SignalAndWait();
            for (int j = 0; j < 100; j++)
            {
                string lockKey = $"lock-{j % 10}";
                detector.RecordAcquired(lockKey, $"owner-{ownerId}");
                await detector.RecordWaitingAsync(lockKey, $"owner-{(ownerId + 1) % 4}");
                await detector.WouldDeadlock(lockKey, $"owner-{ownerId}");
                detector.RecordReleased(lockKey, $"owner-{ownerId}");
                await detector.RecordWaitEndedAsync(lockKey, $"owner-{ownerId}");
            }
        }));
    }

    await Task.WhenAll(tasks);

    // Verify no corruption: all metrics retrievable
    var allMetrics = await detector.GetAllMetrics();
    Assert.That(allMetrics, Is.Not.Null);
    Assert.That(allMetrics.Count, Is.GreaterThan(0));
}
```

## Notes

- **Null handling:** `RecordWaitingAsync` enforces non-null `ownerId` and `lockKey` arguments by throwing `ArgumentNullException`. The constructor likewise rejects a null logger. Other methods may assume valid inputs; callers should validate arguments before invocation.
- **Ownership lifecycle:** `RecordAcquired` establishes ownership, `RecordReleased` clears it only when the releasing owner matches the recorded owner. Releasing with a mismatched owner is silently ignored, preventing accidental removal of another owner’s claim.
- **Wait tracking:** `RecordWaitingAsync` registers a waiter, and `RecordWaitEndedAsync` removes it while recording the wait duration. Multiple waits for the same lock key by the same owner are aggregated into an average wait time metric.
- **Metrics availability:** `GetMetrics` returns `null` (or an empty result) when no data exists for the requested lock. After waiters or acquisitions are recorded, a metrics object becomes available. `GetAllMetrics` returns entries for all actively tracked locks.
- **Thread safety:** The `ConcurrentWaitingAndAcquisition_MaintainsConsistency` test explicitly validates that concurrent calls to recording, detection, and metrics methods do not corrupt internal state. The detector is designed to be used in multi-threaded environments where distributed lock clients may report events from different threads concurrently.
- **Deadlock detection scope:** `WouldDeadlock` traverses the directed graph of waiters and owners. It returns `true` only when a cycle exists that includes the queried lock and owner. Linear chains without cycles return `false`, even if they are long.
