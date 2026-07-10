# Lock

The `Lock` class represents the state and control surface of an acquired distributed lock within the `dotnet-distributed-lock` system. It encapsulates critical metadata such as the resource key, owner identity, fencing tokens for safety, and temporal boundaries like acquisition and expiration times. Beyond serving as a data container, this type provides actionable methods to extend the lock's lifetime, explicitly release the resource, or verify that the current context still holds valid ownership, enabling robust coordination across distributed nodes.

## API

### `Key`
```csharp
public string Key
```
Gets the unique identifier of the resource being locked. This string corresponds to the logical name of the shared resource guarded by this lock instance.

### `OwnerId`
```csharp
public string OwnerId
```
Gets the unique identifier of the client or process that currently holds the lock. This value is used to distinguish between competing owners attempting to access the same resource.

### `FencingToken`
```csharp
public FencingToken? FencingToken
```
Gets the optional fencing token associated with this lock acquisition. If present, this token must be presented during subsequent write operations to the protected resource to prevent split-brain scenarios where an expired lock is mistakenly considered valid.

### `Status`
```csharp
public LockStatus Status
```
Gets the current state of the lock (e.g., `Acquired`, `Expired`, `Released`). This property reflects the immediate lifecycle stage of the lock instance.

### `AcquiredAt`
```csharp
public DateTime AcquiredAt
```
Gets the precise UTC timestamp when the lock was successfully acquired by the owner.

### `ExpiresAt`
```csharp
public DateTime ExpiresAt
```
Gets the UTC timestamp at which the lock will automatically expire if not renewed. Accessing the resource after this time without a successful renewal is unsafe.

### `RenewedAt`
```csharp
public DateTime? RenewedAt
```
Gets the UTC timestamp of the last successful renewal operation. Returns `null` if the lock has never been renewed since its initial acquisition.

### `RenewalCount`
```csharp
public int RenewalCount
```
Gets the total number of times this lock instance has been successfully renewed.

### `Duration`
```csharp
public TimeSpan Duration
```
Gets the initial time span granted for the lock before expiration. This represents the baseline TTL (Time To Live) configured at acquisition.

### `Metadata`
```csharp
public string? Metadata
```
Gets optional user-defined data attached to the lock. This can be used to store context, trace IDs, or diagnostic information relevant to the lock holder.

### `Renew`
```csharp
public void Renew()
```
Extends the expiration time of the lock, preventing it from expiring while the owner is still working.
*   **Parameters**: None.
*   **Return Value**: None.
*   **Exceptions**: Throws an exception if the lock has already expired, been released, or if the current instance is no longer the valid owner (e.g., ownership was lost to another node).

### `Release`
```csharp
public void Release()
```
Explicitly releases the lock, making the resource available for other owners immediately.
*   **Parameters**: None.
*   **Return Value**: None.
*   **Exceptions**: Throws an exception if the lock is not currently held by this owner or if the lock has already been released.

### `ValidateOwnership`
```csharp
public void ValidateOwnership()
```
Verifies that this instance still represents the valid, active owner of the lock. This is a safety check to ensure no silent expiration or preemption has occurred.
*   **Parameters**: None.
*   **Return Value**: None.
*   **Exceptions**: Throws an exception if the lock status indicates it is no longer owned by this `OwnerId` or if the lock has expired.

### `ToString`
```csharp
public override string ToString()
```
Returns a string representation of the lock, typically including the `Key`, `OwnerId`, `Status`, and expiration details for logging and debugging purposes.
*   **Parameters**: None.
*   **Return Value**: A formatted string describing the current lock state.

## Usage

### Example 1: Acquiring and Safely Releasing a Lock
This example demonstrates acquiring a lock, performing work, and explicitly releasing it within a `try-finally` block to ensure cleanup even if errors occur.

```csharp
var lockProvider = new DistributedLockProvider(); // Hypothetical provider
var lockRequest = new LockRequest("inventory-item-123", "worker-node-01");

Lock myLock = await lockProvider.AcquireAsync(lockRequest);

try
{
    // Verify we actually own the lock before proceeding
    myLock.ValidateOwnership();
    
    // Perform critical section work
    Console.WriteLine($"Processing {myLock.Key} with token {myLock.FencingToken}");
    
    // Simulate work
    await Task.Delay(1000);
}
finally
{
    // Ensure the lock is released so others can acquire it
    if (myLock.Status == LockStatus.Acquired)
    {
        myLock.Release();
    }
}
```

### Example 2: Long-Running Process with Renewal
This example illustrates a long-running task that periodically renews the lock to prevent expiration while work is ongoing.

```csharp
Lock myLock = await lockProvider.AcquireAsync(new LockRequest("batch-job-99", "scheduler-service"));

try
{
    while (!IsJobComplete())
    {
        // Validate that we haven't lost ownership due to network partitions
        myLock.ValidateOwnership();
        
        ProcessNextBatch();
        
        // If the job takes longer than the lock duration, renew it
        if (DateTime.UtcNow > myLock.ExpiresAt.AddSeconds(-10))
        {
            myLock.Renew();
            Console.WriteLine($"Lock renewed. Count: {myLock.RenewalCount}, New Expiry: {myLock.ExpiresAt}");
        }
        
        await Task.Delay(500);
    }
}
finally
{
    myLock.Release();
}
```

## Notes

*   **Thread Safety**: The `Lock` instance itself is not inherently thread-safe for concurrent modification of its state by multiple threads within the same process. While `Renew`, `Release`, and `ValidateOwnership` interact with the distributed system, calling these methods concurrently on the same `Lock` object from multiple threads may result in race conditions regarding local state properties like `RenewedAt` or `Status`. External synchronization (e.g., `lock` statement) is recommended if a single `Lock` instance is shared across threads.
*   **Expiration vs. Release**: A lock may transition to an expired state automatically if `Renew` is not called before `ExpiresAt`. Once expired, calling `Release` or `Renew` will throw an exception. Always check `Status` or catch specific exceptions when managing locks that may have long gaps between operations.
*   **Fencing Token Usage**: If `FencingToken` is not null, it is critical to pass this token to the underlying storage or resource system during write operations. Relying solely on the existence of the `Lock` object without checking the token can lead to data corruption in split-brain scenarios.
*   **Ownership Validation**: `ValidateOwnership` is a proactive check. It does not guarantee that the lock will remain valid for the next millisecond, but it confirms that, at the moment of invocation, the distributed system recognizes the `OwnerId` as the current holder.
