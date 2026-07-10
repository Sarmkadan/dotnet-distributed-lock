# SqliteLockRepository

The `SqliteLockRepository` class provides a concrete implementation of a distributed lock storage mechanism backed by a SQLite database. It manages the persistence, retrieval, and lifecycle of lock entities, enabling coordination across multiple processes or threads by storing lock state in a file-based database. This repository handles critical operations such as acquiring new locks, renewing existing leases, releasing resources, and cleaning up expired entries, ensuring data consistency through asynchronous database interactions.

## API

### `public SqliteLockRepository`
Initializes a new instance of the `SqliteLockRepository` class. The constructor typically accepts configuration required to establish a connection to the underlying SQLite database, such as a connection string or file path, though specific parameters depend on the instantiation context defined in the consuming application.

### `public async Task<bool> AcquireAsync`
Attempts to create and persist a new lock entry in the database.
*   **Purpose**: Registers a new distributed lock with a specific key and owner.
*   **Parameters**: Accepts details defining the lock (e.g., key, owner ID, expiration time, fencing token).
*   **Return Value**: Returns `true` if the lock was successfully created; `false` if a conflict occurred (e.g., the key is already locked by another owner).
*   **Exceptions**: May throw database-related exceptions if the connection fails or the schema is invalid.

### `public async Task<Lock?> GetByKeyAsync`
Retrieves a lock entity based solely on its unique key.
*   **Purpose**: Fetches the current state of a lock identified by a specific key.
*   **Parameters**: The unique string key identifying the lock.
*   **Return Value**: Returns a `Lock` object if found; otherwise, `null`.
*   **Exceptions**: Throws on database read errors.

### `public async Task<Lock?> GetByKeyAndOwnerAsync`
Retrieves a lock entity verifying both the key and the owner identity.
*   **Purpose**: Ensures that the caller is the current owner of the lock before returning its details.
*   **Parameters**: The lock key and the owner identifier.
*   **Return Value**: Returns a `Lock` object if a match is found; otherwise, `null`.
*   **Exceptions**: Throws on database read errors.

### `public async Task<bool> UpdateAsync`
Persists changes to an existing lock entity.
*   **Purpose**: Updates metadata associated with a lock, such as extending expiration or modifying internal state.
*   **Parameters**: The `Lock` object containing updated fields.
*   **Return Value**: Returns `true` if the update affected a row; `false` if the lock no longer exists.
*   **Exceptions**: Throws on database write errors or concurrency conflicts.

### `public async Task<bool> RenewAsync`
Extends the expiration time of an existing lock.
*   **Purpose**: Keeps a lock alive beyond its original timeout, preventing premature release while work is still in progress.
*   **Parameters**: The lock key, owner identifier, and the new expiration timestamp.
*   **Return Value**: Returns `true` if the renewal succeeded; `false` if the lock does not exist or the owner does not match.
*   **Exceptions**: Throws on database write errors.

### `public async Task<bool> ReleaseAsync`
Explicitly removes a lock from the database.
*   **Purpose**: Frees a distributed lock before its natural expiration, allowing other waiters to acquire it immediately.
*   **Parameters**: The lock key and the owner identifier to verify ownership before deletion.
*   **Return Value**: Returns `true` if the lock was successfully released; `false` if the lock was not found or ownership validation failed.
*   **Exceptions**: Throws on database write errors.

### `public async Task<bool> ExistsAsync`
Checks for the presence of a lock entry without retrieving its full data.
*   **Purpose**: Efficiently determines if a specific key is currently locked.
*   **Parameters**: The unique string key.
*   **Return Value**: Returns `true` if the lock exists; otherwise, `false`.
*   **Exceptions**: Throws on database read errors.

### `public async Task<IEnumerable<Lock>> GetAllActiveLockAsync`
Retrieves all locks that are currently valid and not expired.
*   **Purpose**: Provides a snapshot of the current distributed lock landscape for monitoring or administrative tasks.
*   **Parameters**: None.
*   **Return Value**: An enumerable collection of active `Lock` objects.
*   **Exceptions**: Throws on database read errors.

### `public async Task<IEnumerable<Lock>> GetByOwnerAsync`
Retrieves all locks currently held by a specific owner.
*   **Purpose**: Allows an owner to inspect or manage all resources they currently hold.
*   **Parameters**: The owner identifier.
*   **Return Value**: An enumerable collection of `Lock` objects associated with the owner.
*   **Exceptions**: Throws on database read errors.

### `public async Task<int> DeleteExpiredLockAsync`
Removes lock entries that have passed their expiration timestamp.
*   **Purpose**: Performs maintenance to clean up stale locks and free up database space.
*   **Parameters**: None (operates based on internal expiration logic).
*   **Return Value**: The number of rows deleted.
*   **Exceptions**: Throws on database write errors.

### `public async Task<int> ClearAllAsync`
Deletes all lock entries from the repository.
*   **Purpose**: Resets the entire locking state, typically used for testing or emergency recovery.
*   **Parameters**: None.
*   **Return Value**: The total number of rows deleted.
*   **Exceptions**: Throws on database write errors.

### `public async Task<bool> ValidateFencingTokenAsync`
Verifies the integrity of a fencing token against the stored lock state.
*   **Purpose**: Ensures that an operation is being performed with the most recent authorization token, preventing race conditions in split-brain scenarios.
*   **Parameters**: The lock key and the fencing token to validate.
*   **Return Value**: Returns `true` if the token matches the current state; otherwise, `false`.
*   **Exceptions**: Throws on database read errors.

### `public async ValueTask DisposeAsync`
Releases unmanaged resources and closes the database connection.
*   **Purpose**: Cleans up the repository instance, ensuring the SQLite connection is properly terminated.
*   **Parameters**: None.
*   **Return Value**: A `ValueTask` representing the asynchronous disposal operation.
*   **Exceptions**: Throws if the underlying connection fails to close gracefully.

## Usage

### Example 1: Acquiring and Renewing a Lock
This example demonstrates acquiring a lock for a specific resource key, performing work, renewing the lease if the operation takes longer than expected, and finally releasing the lock.

```csharp
using var repository = new SqliteLockRepository("Data Source=locks.db");

string resourceKey = "inventory-update-123";
string ownerId = "worker-node-01";
TimeSpan lockDuration = TimeSpan.FromSeconds(30);

// Attempt to acquire the lock
bool acquired = await repository.AcquireAsync(new Lock 
{ 
    Key = resourceKey, 
    Owner = ownerId, 
    ExpiresAt = DateTime.UtcNow.Add(lockDuration) 
});

if (acquired)
{
    try
    {
        // Simulate long-running work
        await Task.Delay(45000);

        // Work exceeds initial duration; attempt to renew
        bool renewed = await repository.RenewAsync(
            resourceKey, 
            ownerId, 
            DateTime.UtcNow.Add(lockDuration)
        );

        if (!renewed)
        {
            throw new InvalidOperationException("Failed to renew lock; operation aborted.");
        }

        // Complete work
        await PerformInventoryUpdate();
    }
    finally
    {
        // Always release the lock when done
        await repository.ReleaseAsync(resourceKey, ownerId);
    }
}
else
{
    Console.WriteLine("Could not acquire lock; resource is busy.");
}
```

### Example 2: Maintenance and Cleanup
This example illustrates how to monitor active locks and perform periodic cleanup of expired entries to maintain database health.

```csharp
using var repository = new SqliteLockRepository("Data Source=locks.db");

// Audit: List all locks held by a specific owner
var ownerLocks = await repository.GetByOwnerAsync("worker-node-01");
Console.WriteLine($"Active locks for worker-node-01: {ownerLocks.Count()}");

// Maintenance: Remove expired locks
int deletedCount = await repository.DeleteExpiredLockAsync();
if (deletedCount > 0)
{
    Console.WriteLine($"Cleaned up {deletedCount} expired lock entries.");
}

// Emergency: Validate fencing token before a critical write
string targetKey = "critical-section-alpha";
long currentToken = 1024;

bool isValid = await repository.ValidateFencingTokenAsync(targetKey, currentToken);
if (isValid)
{
    await ExecuteCriticalOperation();
}
else
{
    Console.WriteLine("Fencing token mismatch; operation rejected to prevent data corruption.");
}
```

## Notes

*   **Thread Safety**: While the methods are asynchronous, SQLite itself has specific concurrency limitations depending on the journal mode and connection configuration. It is recommended to ensure that the underlying `SqliteConnection` is not shared concurrently across multiple threads without proper locking mechanisms external to this repository, or that the connection string enables appropriate serialization (e.g., `BusyTimeout`).
*   **Expiration Logic**: The `DeleteExpiredLockAsync` method relies on the `ExpiresAt` timestamp stored in the database. Clock skew between different nodes hosting the application may result in premature deletion or retention of locks; synchronized system clocks are required for correct behavior.
*   **Fencing Tokens**: The `ValidateFencingTokenAsync` method is critical for preventing split-brain scenarios. If this method returns `false`, the caller must assume the lock state has changed and abort any protected operations immediately.
*   **Disposal**: The class implements `IAsyncDisposable`. Consumers should strictly use `await using` patterns or explicitly call `DisposeAsync` to prevent file handle leaks, as SQLite locks the database file while the connection is open.
*   **Return Values**: Most boolean return methods (`AcquireAsync`, `ReleaseAsync`, `RenewAsync`) return `false` rather than throwing exceptions for logical failures (e.g., lock already held, owner mismatch). Exceptions are reserved for infrastructure failures (e.g., disk full, corruption).
