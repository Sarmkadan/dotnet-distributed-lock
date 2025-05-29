# InMemoryLockRepository

The `InMemoryLockRepository` is a concrete implementation of a distributed lock storage mechanism designed for single-process or testing scenarios. It maintains the state of active locks entirely within the application's memory, providing fast, asynchronous access to lock acquisition, renewal, and release operations without requiring external infrastructure like Redis or a database. This repository is ideal for development environments, unit testing, or single-instance deployments where persistence across process restarts is not required.

## API

### `AcquireAsync`
Attempts to acquire a new lock for a specified key and owner.
- **Purpose**: Creates a new lock entry if the key is not currently locked or if the existing lock has expired.
- **Parameters**: Accepts arguments defining the lock key, owner identifier, expiration duration, and optional metadata.
- **Return Value**: Returns a `Task<bool>` indicating `true` if the lock was successfully acquired, or `false` if the key is already held by another active owner.
- **Exceptions**: May throw if the input arguments are invalid (e.g., null key or negative expiration).

### `GetByKeyAsync`
Retrieves the current lock state associated with a specific key.
- **Purpose**: Fetches the lock details regardless of the owner.
- **Parameters**: Takes the unique lock key as a string.
- **Return Value**: Returns a `Task<Lock?>` containing the lock object if found, or `null` if no lock exists for the key.
- **Exceptions**: Throws if the key argument is null or empty.

### `GetByKeyAndOwnerAsync`
Retrieves the lock state only if it matches both the key and the specific owner.
- **Purpose**: Verifies ownership and retrieves lock details in a single operation.
- **Parameters**: Takes the lock key and the owner identifier.
- **Return Value**: Returns a `Task<Lock?>` containing the lock object if found and owned by the specified owner, otherwise `null`.
- **Exceptions**: Throws if key or owner arguments are null or empty.

### `UpdateAsync`
Updates the metadata or expiration time of an existing lock.
- **Purpose**: Modifies properties of a lock, typically used to extend expiration or update context information.
- **Parameters**: Accepts a `Lock` object containing the updated state.
- **Return Value**: Returns a `Task<bool>` indicating `true` if the update was applied, or `false` if the lock does not exist or ownership validation fails.
- **Exceptions**: Throws if the provided lock object is null or invalid.

### `RenewAsync`
Extends the expiration time of an existing lock.
- **Purpose**: Keeps a lock alive beyond its original expiration window.
- **Parameters**: Takes the lock key, owner identifier, and the additional duration to extend.
- **Return Value**: Returns a `Task<bool>` indicating `true` if the renewal succeeded, or `false` if the lock is not held by the specified owner or has already expired.
- **Exceptions**: Throws if arguments are invalid or the duration is negative.

### `ReleaseAsync`
Explicitly releases a lock, making the key available for other owners.
- **Purpose**: Removes the lock entry or marks it as released before its natural expiration.
- **Parameters**: Takes the lock key and the owner identifier.
- **Return Value**: Returns a `Task<bool>` indicating `true` if the lock was successfully released by the owner, or `false` if the lock does not exist or is owned by someone else.
- **Exceptions**: Throws if key or owner arguments are null or empty.

### `ExistsAsync`
Checks whether a lock currently exists for a given key.
- **Purpose**: Performs a lightweight check for the presence of a lock without retrieving full details.
- **Parameters**: Takes the lock key.
- **Return Value**: Returns a `Task<bool>` indicating `true` if a lock (active or expired but not yet cleaned) exists for the key, otherwise `false`.
- **Exceptions**: Throws if the key argument is null or empty.

### `GetAllActiveLockAsync`
Retrieves a collection of all currently active locks in the repository.
- **Purpose**: Provides a snapshot of all held locks, useful for monitoring or administrative tasks.
- **Parameters**: No parameters required.
- **Return Value**: Returns a `Task<IEnumerable<Lock>>` containing all locks that have not yet expired.
- **Exceptions**: None typical.

### `GetByOwnerAsync`
Retrieves all active locks held by a specific owner.
- **Purpose**: Allows an owner to inspect all resources they currently lock.
- **Parameters**: Takes the owner identifier.
- **Return Value**: Returns a `Task<IEnumerable<Lock>>` containing all locks associated with the owner.
- **Exceptions**: Throws if the owner argument is null or empty.

### `DeleteExpiredLockAsync`
Removes locks that have passed their expiration time.
- **Purpose**: Cleans up stale entries to free memory and ensure accurate state.
- **Parameters**: No parameters required (scans all keys).
- **Return Value**: Returns a `Task<int>` representing the number of locks deleted.
- **Exceptions**: None typical.

### `ClearAllAsync`
Removes all locks from the repository regardless of state or owner.
- **Purpose**: Resets the repository to an empty state, primarily used for testing teardown.
- **Parameters**: No parameters required.
- **Return Value**: Returns a `Task<int>` representing the total number of locks cleared.
- **Exceptions**: None typical.

### `ValidateFencingTokenAsync`
Validates a fencing token against the current lock state.
- **Purpose**: Ensures that an operation is performed only if the fencing token matches the current lock generation, preventing race conditions in distributed scenarios.
- **Parameters**: Takes the lock key, owner identifier, and the fencing token to validate.
- **Return Value**: Returns a `Task<bool>` indicating `true` if the token is valid and matches the current lock state, otherwise `false`.
- **Exceptions**: Throws if arguments are null or invalid.

## Usage

### Example 1: Basic Lock Acquisition and Release
This example demonstrates acquiring a lock, performing a critical section operation, and explicitly releasing the lock.

```csharp
var repository = new InMemoryLockRepository();
string resourceKey = "order-processing-123";
string ownerId = "worker-node-01";

// Attempt to acquire the lock with a 30-second expiration
bool acquired = await repository.AcquireAsync(resourceKey, ownerId, TimeSpan.FromSeconds(30));

if (acquired)
{
    try
    {
        // Critical section: Perform protected operations
        await ProcessOrderAsync(resourceKey);
        
        // Optionally renew the lock if processing takes longer than expected
        await repository.RenewAsync(resourceKey, ownerId, TimeSpan.FromSeconds(30));
    }
    finally
    {
        // Ensure the lock is released even if an exception occurs
        await repository.ReleaseAsync(resourceKey, ownerId);
    }
}
else
{
    Console.WriteLine("Could not acquire lock; resource is busy.");
}
```

### Example 2: Monitoring and Cleanup
This example shows how to inspect active locks for a specific owner and clean up expired entries.

```csharp
var repository = new InMemoryLockRepository();
string ownerId = "worker-node-01";

// Retrieve all locks held by this owner
var myLocks = await repository.GetByOwnerAsync(ownerId);
Console.WriteLine($"Active locks for {ownerId}: {myLocks.Count()}");

// Perform maintenance: Remove expired locks from memory
int deletedCount = await repository.DeleteExpiredLockAsync();
Console.WriteLine($"Cleaned up {deletedCount} expired locks.");

// In a test teardown scenario, clear all locks
int totalCleared = await repository.ClearAllAsync();
Console.WriteLine($"Total locks cleared: {totalCleared}");
```

## Notes

- **Thread Safety**: As an in-memory implementation, `InMemoryLockRepository` relies on internal concurrency controls to handle simultaneous access. While the methods are asynchronous and designed to be thread-safe within a single process instance, they do not provide coordination across multiple processes or servers. Using this repository in a multi-instance deployment will result in isolated lock states per instance, breaking distributed locking guarantees.
- **Volatility**: All lock data is stored in RAM. Restarting the application or recycling the AppDomain will result in the immediate loss of all lock states. Any locks held at the time of shutdown will be lost, potentially leading to resource contention issues upon restart if external systems assume the locks persist.
- **Expiration Handling**: Expired locks are not automatically removed the moment they expire; they remain in memory until `DeleteExpiredLockAsync` is called or until they are overwritten by a new `AcquireAsync` call for the same key. In long-running processes with high lock churn, periodic calls to `DeleteExpiredLockAsync` are recommended to prevent memory growth.
- **Fencing Tokens**: The `ValidateFencingTokenAsync` method is critical for ensuring safety when lock ownership changes hands. If a lock expires and is reacquired by a different owner, the fencing token changes. Operations relying on stale tokens must be rejected to prevent split-brain scenarios, even within a single-process simulation.
