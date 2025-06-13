# AdvancedConcurrencyTests

The `AdvancedConcurrencyTests` class is a test fixture designed to validate the behavior of a distributed lock implementation under demanding concurrent scenarios. It exercises the lock’s correctness, fairness, renewal, expiration, error recovery, and metrics tracking when multiple workers compete for the same or different locks simultaneously. Each test method is asynchronous and returns `Task`, and is intended to be run by a unit test framework (e.g., xUnit, NUnit). The tests assume a shared distributed lock repository (e.g., Redis, Azure Blob) and a configurable lock manager.

## API

### `public AdvancedConcurrencyTests()`

Initializes a new instance of the test class. No parameters. The constructor typically sets up the distributed lock infrastructure (e.g., connection strings, lock factory) via dependency injection or test configuration.

### `public async Task HighContention_ManyWorkersRacingForSameLock()`

**Purpose:** Verifies that when a large number of workers (e.g., 50–100) attempt to acquire the same lock key concurrently, only one worker holds the lock at any time, and all workers eventually complete without deadlock or data corruption.

**Parameters:** None.

**Returns:** `Task` – completes when the test scenario finishes.

**Throws:** `AssertionException` (or equivalent) if more than one worker simultaneously holds the lock, if any worker fails to acquire within a reasonable timeout, or if the lock is not released correctly after use.

### `public async Task HighContention_MultipleLocksWithoutInterference()`

**Purpose:** Ensures that high contention on different lock keys does not cause cross‑key interference. Workers each race for a distinct key, and the test validates that acquisitions and releases for one key do not affect the availability of another.

**Parameters:** None.

**Returns:** `Task`.

**Throws:** If any lock acquisition fails due to spurious contention on unrelated keys, or if the lock state becomes inconsistent across keys.

### `public async Task RenewalUnderLoad_SimultaneousRenewalsAndAcquisitions()`

**Purpose:** Tests that lock renewal (extending the lease) works correctly while other workers are simultaneously trying to acquire the same lock. The lock should remain held by the renewing worker until it explicitly releases, and new acquisitions should only succeed after release.

**Parameters:** None.

**Returns:** `Task`.

**Throws:** If a renewal fails under load, if a worker acquires a lock that is still being renewed, or if the lock expires prematurely.

### `public async Task RapidCycle_AcquireReleaseAcquireSequence()`

**Purpose:** Validates that a single worker can rapidly acquire, release, and re‑acquire the same lock many times in succession without encountering stale state, race conditions, or throttling.

**Parameters:** None.

**Returns:** `Task`.

**Throws:** If any acquire or release operation in the sequence fails, or if the lock is not immediately available after release.

### `public async Task ConcurrentOperations_ManyKeysWithManyWorkers()`

**Purpose:** Combines many workers and many distinct lock keys. Each worker picks a random key from a large pool and performs acquire‑release cycles. The test verifies that the system scales correctly and that no key‑specific corruption occurs.

**Parameters:** None.

**Returns:** `Task`.

**Throws:** If the overall throughput degrades unexpectedly, if any lock operation fails, or if the final state of any key is inconsistent.

### `public async Task CompleteLifecycleStress_AcquireRenewReleaseRepeatedly()`

**Purpose:** Simulates a realistic workload where a worker acquires a lock, renews it several times, and then releases it, repeating the entire cycle many times under concurrent load from other workers.

**Parameters:** None.

**Returns:** `Task`.

**Throws:** If any step of the lifecycle fails (acquire, renew, release) or if the lock’s internal state machine becomes corrupted.

### `public async Task MetricsUnderConcurrentLoad_CorrectlyTrackOperations()`

**Purpose:** Checks that the distributed lock’s metrics (e.g., acquisition count, renewal count, failure count, duration) are accurately recorded even when many operations happen concurrently. The test compares observed metrics against expected values.

**Parameters:** None.

**Returns:** `Task`.

**Throws:** If metrics are missing, duplicated, or out of expected ranges.

### `public async Task ExpirationHandling_LocksExpireAndCanBeReacquired()`

**Purpose:** Verifies that a lock that is not renewed eventually expires, and that another worker can acquire it after expiration. The test also ensures that the expired lock does not leave behind orphaned state.

**Parameters:** None.

**Returns:** `Task`.

**Throws:** If a lock does not expire within the expected time, if a worker cannot acquire an expired lock, or if the lock’s expiration causes data inconsistency.

### `public async Task ErrorRecovery_RepositoryRemainsConsistentAfterFailures()`

**Purpose:** Injects transient failures (e.g., network timeouts, repository unavailability) during lock operations and verifies that the distributed lock repository remains in a consistent state and that subsequent operations succeed.

**Parameters:** None.

**Returns:** `Task`.

**Throws:** If the repository becomes corrupted, if a lock is left in an unrecoverable state, or if the system does not gracefully handle the injected errors.

### `public async Task LockFairness_SomeWorkersCanAcquire()`

**Purpose:** Ensures that under contention, at least some workers are able to acquire the lock (i.e., the lock is not permanently starved by a single worker). It does not guarantee strict FIFO fairness, but verifies that no worker is indefinitely blocked.

**Parameters:** None.

**Returns:** `Task`.

**Throws:** If a worker is never able to acquire the lock after many attempts, or if the lock acquisition pattern is demonstrably unfair (e.g., one worker always wins).

## Usage

The following examples demonstrate how to instantiate and run the tests using xUnit. The tests assume that the distributed lock infrastructure is configured via a test fixture or dependency injection.

**Example 1: Running a single test with default configuration**

```csharp
using Xunit;

public class DistributedLockTestSuite : IClassFixture<AdvancedConcurrencyTests>
{
    private readonly AdvancedConcurrencyTests _tests;

    public DistributedLockTestSuite(AdvancedConcurrencyTests tests)
    {
        _tests = tests;
    }

    [Fact]
    public async Task HighContention_ShouldPass()
    {
        await _tests.HighContention_ManyWorkersRacingForSameLock();
    }
}
```

**Example 2: Running multiple tests in a custom test runner**

```csharp
using System;
using System.Threading.Tasks;

public class CustomTestRunner
{
    public async Task RunAllAdvancedConcurrencyTests()
    {
        var tests = new AdvancedConcurrencyTests();

        Console.WriteLine("Running HighContention_ManyWorkersRacingForSameLock...");
        await tests.HighContention_ManyWorkersRacingForSameLock();

        Console.WriteLine("Running HighContention_MultipleLocksWithoutInterference...");
        await tests.HighContention_MultipleLocksWithoutInterference();

        // ... continue with other tests

        Console.WriteLine("All advanced concurrency tests passed.");
    }
}
```

## Notes

- **Thread safety:** The test methods themselves are not thread‑safe; they should be executed sequentially within a single test runner process. Concurrent execution of multiple test methods may interfere with shared lock state (e.g., the same lock repository). Use test isolation (e.g., separate keys per test) or a dedicated test environment.
- **Timeouts:** Many tests rely on timeouts to detect deadlocks or starvation. Ensure the test environment has sufficient time budget (e.g., 30–60 seconds per test). Adjust the lock’s default lease duration and retry intervals accordingly.
- **Repository state:** Tests that verify expiration (`ExpirationHandling_LocksExpireAndCanBeReacquired`) and error recovery (`ErrorRecovery_RepositoryRemainsConsistentAfterFailures`) may leave temporary keys in the repository. Cleanup should be performed after each test run to avoid cross‑test contamination.
- **Metrics:** The `MetricsUnderConcurrentLoad_CorrectlyTrackOperations` test assumes that the lock implementation exposes a metrics interface (e.g., `ILockMetrics`). If metrics are not enabled, this test will fail.
- **Fairness:** `LockFairness_SomeWorkersCanAcquire` does not guarantee strict ordering; it only checks that no worker is permanently starved. In environments with very short leases and high contention, it is possible for a worker to be starved for many cycles; the test uses a generous timeout to distinguish true starvation from transient scheduling delays.
- **Error injection:** The `ErrorRecovery_RepositoryRemainsConsistentAfterFailures` test requires the ability to inject failures into the underlying repository (e.g., via a mock or a fault‑injection proxy). If the repository does not support such injection, the test may be skipped or adapted.
