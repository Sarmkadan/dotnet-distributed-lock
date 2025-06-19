# LockRenewalWorkerExtensions

The `LockRenewalWorkerExtensions` class provides a set of static utility methods designed to manage the lifecycle and configuration of distributed lock renewal processes. It enables consumers to safely register locks for automatic renewal, query renewal schedules, dynamically adjust renewal intervals, and cleanly unregister locks to prevent resource leaks or unnecessary network traffic. These extensions act as a control plane for the background worker responsible for maintaining lock validity over extended periods.

## API

### `TryRegisterForRenewal`
```csharp
public static bool TryRegisterForRenewal(...)
```
Attempts to register a specific lock instance with the background renewal worker. This method initiates the automatic renewal cycle for the provided lock, ensuring it does not expire while the operation is active.
*   **Purpose**: To enroll a lock in the automatic renewal mechanism.
*   **Parameters**: Accepts the lock instance or identifier required by the underlying worker implementation.
*   **Return Value**: Returns `true` if the lock was successfully registered; returns `false` if the lock is already registered, invalid, or if the worker is in a state where new registrations are rejected.
*   **Exceptions**: This method does not throw exceptions for logical failures; it returns `false` instead. It may throw standard system exceptions (e.g., `ArgumentNullException`) if null arguments are passed where not permitted.

### `SafeUnregisterFromRenewal`
```csharp
public static void SafeUnregisterFromRenewal(...)
```
Stops the automatic renewal process for a specific lock. This method ensures that the background worker ceases attempts to renew the specified lock, typically called immediately after a lock is manually released or the owning operation completes.
*   **Purpose**: To cleanly remove a lock from the renewal queue and stop background traffic.
*   **Parameters**: Accepts the lock instance or identifier to be unregistered.
*   **Return Value**: `void`.
*   **Exceptions**: As implied by the "Safe" prefix, this method suppresses internal errors during unregistration. It will not throw exceptions if the lock was never registered or if the worker is already shutting down.

### `GetTimeUntilNextRenewal`
```csharp
public static TimeSpan? GetTimeUntilNextRenewal(...)
```
Retrieves the estimated time remaining before the next scheduled renewal attempt for a specific registered lock.
*   **Purpose**: To allow diagnostics or custom logic to determine the immediacy of the next renewal cycle.
*   **Parameters**: Accepts the lock instance or identifier to query.
*   **Return Value**: Returns a `TimeSpan` representing the duration until the next renewal. Returns `null` if the lock is not currently registered for renewal.
*   **Exceptions**: May throw `ArgumentException` if the provided lock identifier format is invalid.

### `TryUpdateRenewalInterval`
```csharp
public static bool TryUpdateRenewalInterval(...)
```
Dynamically updates the renewal interval frequency for a specific active lock without requiring unregistration.
*   **Purpose**: To adapt the renewal frequency based on changing network conditions or lease requirements.
*   **Parameters**: Accepts the lock instance or identifier and the new `TimeSpan` interval.
*   **Return Value**: Returns `true` if the interval was successfully updated; returns `false` if the lock is not registered or if the new interval is outside acceptable bounds.
*   **Exceptions**: Does not throw exceptions for logical failures (returns `false`). May throw `ArgumentOutOfRangeException` if the provided interval is negative or zero.

## Usage

### Example 1: Registering and Safely Cleaning Up a Lock
This example demonstrates the standard pattern of registering a lock for renewal upon acquisition and ensuring it is unregistered in a `finally` block to prevent orphaned renewal tasks.

```csharp
using DistributedLock.Extensions;

public async Task PerformLongRunningOperationAsync(ILock distributedLock)
{
    // Attempt to register the lock for automatic renewal
    if (!LockRenewalWorkerExtensions.TryRegisterForRenewal(distributedLock))
    {
        throw new InvalidOperationException("Failed to register lock for renewal.");
    }

    try
    {
        // Perform the long-running operation while the lock is actively renewed
        await DoWorkAsync();
    }
    finally
    {
        // Always unregister to stop the background worker once the operation completes
        LockRenewalWorkerExtensions.SafeUnregisterFromRenewal(distributedLock);
        
        // Release the lock manually
        await distributedLock.ReleaseAsync();
    }
}
```

### Example 2: Dynamic Interval Adjustment Based on Load
This example shows how to monitor the renewal schedule and adjust the interval dynamically if the system detects high latency or specific load conditions.

```csharp
using DistributedLock.Extensions;

public void AdjustRenewalStrategy(ILock distributedLock, TimeSpan newInterval)
{
    // Check current status before attempting changes
    var timeUntilNext = LockRenewalWorkerExtensions.GetTimeUntilNextRenewal(distributedLock);

    if (timeUntilNext.HasValue)
    {
        // Attempt to update the interval
        bool updated = LockRenewalWorkerExtensions.TryUpdateRenewalInterval(distributedLock, newInterval);

        if (updated)
        {
            Console.WriteLine($"Renewal interval updated. Next renewal was in {timeUntilNext.Value}, now scheduled based on {newInterval}.");
        }
        else
        {
            Console.WriteLine("Failed to update renewal interval; lock may have been released.");
        }
    }
    else
    {
        Console.WriteLine("Lock is not currently registered for renewal.");
    }
}
```

## Notes

*   **Thread Safety**: All methods in `LockRenewalWorkerExtensions` are thread-safe. Multiple threads may safely call `TryRegisterForRenewal`, `SafeUnregisterFromRenewal`, or `TryUpdateRenewalInterval` concurrently for the same or different lock instances without external synchronization.
*   **Idempotency**: `SafeUnregisterFromRenewal` is idempotent. Calling it multiple times for the same lock, or calling it on a lock that was never registered, will not result in an exception or side effects.
*   **Race Conditions**: When using `GetTimeUntilNextRenewal`, be aware that the returned `TimeSpan` represents a snapshot in time. The actual next renewal may occur slightly earlier or later due to timer resolution or system load, and the lock could be unregistered by another thread immediately after the check.
*   **Return Value Handling**: Methods prefixed with `Try` (`TryRegisterForRenewal`, `TryUpdateRenewalInterval`) follow the Try-Parse pattern. They return `bool` to indicate success rather than throwing exceptions for expected failure scenarios (e.g., lock not found). Consumers must check the return value to ensure the operation succeeded.
*   **Null Intervals**: `GetTimeUntilNextRenewal` returns `null` specifically to distinguish between "not registered" and "registered with 0 time remaining." A return value of `TimeSpan.Zero` implies a renewal is due immediately, whereas `null` implies no renewal schedule exists.
