# LockingIntegrationTests

`LockingIntegrationTests` is a test fixture that validates the behavior of a distributed locking implementation. It contains integration tests covering basic workflows, renewal semantics, fencing tokens, concurrency scenarios, edge cases, and metrics tracking. Each test method exercises a specific aspect of the lock API and asserts correct behavior under both normal and exceptional conditions.

## API

All test methods are `public async Task` and take no parameters. They throw `Xunit.Sdk.XunitException` (or equivalent assertion exceptions) when an expected condition is not met.

- **`BasicWorkflow_AcquireRenewRelease_Succeeds`**  
  Verifies that a lock can be acquired, renewed, and then released without error. Asserts that the lock is held after acquisition and becomes free after release.

- **`BasicWorkflow_CannotAcquireTwice`**  
  Attempts to acquire the same lock twice concurrently. Asserts that the second acquisition attempt fails (returns `null` or throws, depending on the API contract).

- **`BasicWorkflow_DifferentKeysAreIndependent`**  
  Acquires two locks with different keys simultaneously. Asserts that both acquisitions succeed and that operations on one key do not affect the other.

- **`RenewalWorkflow_MultipleRenewalsExtendExpiration`**  
  Acquires a lock, performs multiple renewals, and verifies that the lock’s expiration time is extended accordingly. Asserts that the lock remains held after the original expiry would have passed.

- **`RenewalWorkflow_CannotRenewExpiredLock`**  
  Acquires a lock, waits for it to expire, then attempts to renew. Asserts that the renewal fails (returns `false` or throws).

- **`RenewalWorkflow_OnlyOwnerCanRenew`**  
  Acquires a lock with one owner, then attempts to renew it using a different owner identity. Asserts that the renewal is rejected.

- **`FencingTokenWorkflow_IssueAndValidate`**  
  Acquires a lock and retrieves its fencing token. Asserts that the token is a monotonically increasing value and that it can be validated against the lock instance.

- **`FencingTokenWorkflow_PreventStaleWrites`**  
  Simulates a stale write scenario: acquires a lock, obtains a fencing token, then releases the lock. Attempts to use the old token to perform a write operation; asserts that the write is rejected.

- **`ConcurrencyTest_MultipleWorkersRacing`**  
  Spawns multiple concurrent workers that all attempt to acquire the same lock. Asserts that only one worker holds the lock at any given time and that all workers eventually complete.

- **`ConcurrencyTest_ConcurrentRenewal`**  
  Acquires a lock and then initiates multiple concurrent renewal requests. Asserts that all renewals succeed and that the lock’s expiration is correctly extended.

- **`ConcurrencyTest_AcquireReleaseRapidly`**  
  Repeatedly acquires and releases a lock in rapid succession from multiple threads. Asserts that no deadlocks occur and that the lock state remains consistent.

- **`EdgeCase_ReleaseUnheldLock`**  
  Attempts to release a lock that was never acquired. Asserts that the operation either returns `false` or throws an appropriate exception (e.g., `InvalidOperationException`).

- **`EdgeCase_ReleaseWithWrongOwner`**  
  Acquires a lock with one owner, then attempts to release it using a different owner identity. Asserts that the release is rejected.

- **`EdgeCase_GetAllLocksFiltersExpired`**  
  Acquires a lock, waits for it to expire, then calls a method to list all locks. Asserts that the expired lock is not included in the result.

- **`MetricsTest_TrackingAcquisitionAndRelease`**  
  Acquires and releases a lock multiple times, then checks that the associated metrics (e.g., acquisition count, release count, duration) are correctly recorded.

## Usage

These tests are designed to run with an xUnit test runner. They require a configured distributed lock provider (e.g., Redis, SQL, or in-memory) and a shared test context.

**Example 1 – Running a single test with xUnit**

```csharp
using Xunit;

public class LockingIntegrationTests : IClassFixture<LockFixture>
{
    private readonly LockFixture _fixture;

    public LockingIntegrationTests(LockFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task BasicWorkflow_AcquireRenewRelease_Succeeds()
    {
        var locker = _fixture.CreateLocker();
        // Test logic follows the implementation in the test class
    }
}
```

**Example 2 – Using a shared test collection**

```csharp
[Collection("DistributedLock")]
public class LockingIntegrationTests
{
    private readonly LockTestContext _context;

    public LockingIntegrationTests(LockTestContext context)
    {
        _context = context;
    }

    [Fact]
    public async Task ConcurrencyTest_MultipleWorkersRacing()
    {
        var locker = _context.CreateLocker();
        // Concurrent worker logic
    }
}
```

## Notes

- **Thread safety**: The tests themselves are not thread-safe in the sense that they assume exclusive access to the lock resource during execution. When run in parallel with other tests that use the same lock key, results may be unpredictable. Use test isolation (e.g., unique keys per test) or a dedicated test environment.
- **Expiration handling**: Tests that rely on lock expiration (e.g., `CannotRenewExpiredLock`, `GetAllLocksFiltersExpired`) may be sensitive to clock skew or timing. Ensure the lock provider’s expiration granularity is fine enough for the test’s wait intervals.
- **Owner identity**: The lock API requires an owner identifier (e.g., a GUID or string). Tests that check owner mismatch (`OnlyOwnerCanRenew`, `ReleaseWithWrongOwner`) assume that the owner is passed explicitly and compared strictly.
- **Fencing tokens**: The fencing token mechanism depends on the lock provider guaranteeing monotonicity. Tests may fail if the provider does not support fencing or if tokens are not strictly increasing.
- **Metrics**: The `MetricsTest` assumes that a metrics sink (e.g., `IMeterFactory` or `IMetricsCollector`) is registered and accessible. If metrics are not enabled, the test will be skipped or fail.
- **Cleanup**: Tests that acquire locks should release them in a `finally` block or via `IAsyncLifetime` to avoid leaking locks between test runs. The test fixture should handle cleanup of any leftover locks.
