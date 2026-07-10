# RedisLockRepository

`RedisLockRepository` provides a Redis-backed implementation for managing distributed locks. It offers atomic operations to acquire, renew, release, and validate locks using fencing tokens, ensuring mutual exclusion across distributed processes. The repository also supports querying active locks by key or owner, cleaning up expired entries, and performing bulk maintenance operations.

## API

### RedisLockRepository
Constructor. Initializes a new instance of the repository with the specified Redis connection configuration. The underlying connection multiplexer is typically registered via dependency injection and managed internally.

### AcquireAsync
```csharp
public async Task<bool> AcquireAsync(Lock lock)
```
Attempts to acquire a distributed lock atomically. Returns `true` if the lock was successfully acquired; `false` if the key is already held by another owner. Throws `ArgumentNullException` when the provided `Lock` object is null, or `RedisException` when the Redis server is unreachable.

### GetByKeyAsync
```csharp
public async Task<Lock?> GetByKeyAsync(string key)
```
Retrieves the current lock state for the specified key. Returns the `Lock` instance if one exists, or `null` if no lock is held for that key. Throws `ArgumentNullException` when `key` is null or empty.

### GetByKeyAndOwnerAsync
```csharp
public async Task<Lock?> GetByKeyAndOwnerAsync(string key, string owner)
```
Retrieves a lock by both its key and owner identifier. Returns the matching `Lock` if the specified owner currently holds the lock for that key; otherwise returns `null`. Throws `ArgumentNullException` when either parameter is null or empty.

### UpdateAsync
```csharp
public async Task<bool> UpdateAsync(Lock lock)
```
Updates the metadata of an existing lock (e.g., extending its expiry or modifying auxiliary data) without changing ownership. Returns `true` if the update succeeded; `false` if the lock no longer exists or the owner does not match. Throws `ArgumentNullException` when the `Lock` argument is null.

### RenewAsync
```csharp
public async Task<bool> RenewAsync(Lock lock)
```
Extends the expiry time of an existing lock. The operation succeeds only if the lock is still held by the same owner. Returns `true` on successful renewal; `false` if the lock has expired or been taken by another owner. Throws `ArgumentNullException` when the `Lock` argument is null.

### ReleaseAsync
```csharp
public async Task<bool> ReleaseAsync(Lock lock)
```
Releases a distributed lock. The lock is only released if the owner matches, preventing accidental removal of locks held by other processes. Returns `true` if the lock was released; `false` if the lock was already released, expired, or owned by a different owner. Throws `ArgumentNullException` when the `Lock` argument is null.

### ExistsAsync
```csharp
public async Task<bool> ExistsAsync(string key)
```
Checks whether any lock exists for the given key, regardless of owner. Returns `true` if a lock entry is present; `false` otherwise. Throws `ArgumentNullException` when `key` is null or empty.

### GetAllActiveLockAsync
```csharp
public async Task<IEnumerable<Lock>> GetAllActiveLockAsync()
```
Returns all currently active (non-expired) locks stored in Redis. The returned collection may be empty if no locks are held. Throws `RedisException` on connection failures.

### GetByOwnerAsync
```csharp
public async Task<IEnumerable<Lock>> GetByOwnerAsync(string owner)
```
Retrieves all active locks held by the specified owner. Returns an empty collection if the owner holds no locks. Throws `ArgumentNullException` when `owner` is null or empty.

### DeleteExpiredLockAsync
```csharp
public async Task<int> DeleteExpiredLockAsync()
```
Removes all locks whose expiry time has passed. Returns the number of expired locks deleted. This is a maintenance operation that can be called periodically to clean up stale entries. Throws `RedisException` on connection failures.

### ClearAllAsync
```csharp
public async Task<int> ClearAllAsync()
```
Removes all lock entries from the store, regardless of their state. Returns the total number of locks deleted. This is a destructive operation intended for administrative or testing scenarios. Throws `RedisException` on connection failures.

### ValidateFencingTokenAsync
```csharp
public async Task<bool> ValidateFencingTokenAsync(string key, long fencingToken)
```
Validates that the provided fencing token is still the current token for the given lock key. Returns `true` if the token matches the active lock's token; `false` if the lock has been re-acquired with a newer token or no longer exists. Throws `ArgumentNullException` when `key` is null or empty.

### DisposeAsync
```csharp
public async ValueTask DisposeAsync()
```
Asynchronously releases managed Redis resources held by the repository. Should be called when the repository is no longer needed, typically through the dependency injection container's lifetime management.

## Usage

### Example 1: Acquire, work, and release
```csharp
var repository = serviceProvider.GetRequiredService<RedisLockRepository>();
var lockObj = new Lock("resource:order:123", "service-instance-01", TimeSpan.FromSeconds(30));

bool acquired = await repository.AcquireAsync(lockObj);
if (!acquired)
{
    Console.WriteLine("Lock is held by another process.");
    return;
}

try
{
    // Perform critical work while holding the lock.
    await ProcessOrderAsync(123);

    // Optionally renew if work takes longer than expected.
    await repository.RenewAsync(lockObj);
}
finally
{
    bool released = await repository.ReleaseAsync(lockObj);
    if (!released)
    {
        Console.WriteLine("Lock may have expired or been taken by another owner.");
    }
}
```

### Example 2: Fencing token validation for resource access
```csharp
var repository = serviceProvider.GetRequiredService<RedisLockRepository>();
var lockObj = new Lock("file:document:456", "worker-node-07", TimeSpan.FromMinutes(2));

if (await repository.AcquireAsync(lockObj))
{
    long token = lockObj.FencingToken;

    // Perform an operation that requires monotonic validation.
    await WriteToSharedStorageAsync("document-456", token);

    // Before a subsequent write, validate the fencing token is still current.
    bool valid = await repository.ValidateFencingTokenAsync("file:document:456", token);
    if (!valid)
    {
        throw new InvalidOperationException("Lock was overtaken; aborting to prevent corruption.");
    }

    await AppendToSharedStorageAsync("document-456", token);
    await repository.ReleaseAsync(lockObj);
}
```

## Notes

- **Atomicity**: All lock-modifying operations (`AcquireAsync`, `ReleaseAsync`, `RenewAsync`, `UpdateAsync`) are implemented using Redis Lua scripts to guarantee atomic evaluation of ownership and expiry conditions. Partial failures due to network interruptions are not possible at the script execution level.
- **Fencing tokens**: The repository assigns monotonically increasing fencing tokens on each successful acquisition. `ValidateFencingTokenAsync` enables callers to implement the fencing token pattern, preventing stale processes from accessing shared resources after their lock has been overtaken.
- **Expiry and drift**: Lock expiry is enforced by Redis key expiration. System clock drift between the application host and the Redis server can cause locks to expire earlier or later than expected. Callers should use conservative expiry durations and renew proactively.
- **Thread safety**: The repository itself is thread-safe; all public methods can be called concurrently from multiple threads. The underlying Redis connection multiplexer handles multiplexing and pipelining safely.
- **Null arguments**: Methods that accept `string` or `Lock` parameters throw `ArgumentNullException` when required arguments are null or empty. Callers should guard inputs before invocation.
- **Disposal**: `DisposeAsync` releases the Redis connection if the repository owns it. When the connection is shared via dependency injection, disposal is typically managed by the container and calling `DisposeAsync` directly may be unnecessary or harmful. Consult the composition root configuration.
- **`ClearAllAsync` and `DeleteExpiredLockAsync`**: These are bulk operations that run on the server without per-key atomicity guarantees relative to concurrent lock operations. They should be used during maintenance windows or in administrative tools, not as part of normal lock lifecycle management.
