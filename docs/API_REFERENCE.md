# Complete API Reference

## ILockService

The primary interface for lock operations.

### AcquireAsync

Acquires a lock, blocking/retrying until successful or timeout.

```csharp
public Task<Lock> AcquireAsync(
    string lockKey,
    string ownerId,
    TimeSpan? duration = null,
    CancellationToken cancellationToken = default
);
```

**Parameters:**
- `lockKey` (string, required): Unique identifier for the protected resource
- `ownerId` (string, required): Identifier of the lock owner (process ID, worker name, etc.)
- `duration` (TimeSpan?, optional): How long the lock should be held; uses `DefaultLockDuration` if null
- `cancellationToken` (CancellationToken, optional): For cancellation support

**Returns:** `Task<Lock>` - The acquired lock object

**Throws:**
- `LockAcquisitionException`: Lock could not be acquired within configured timeout
- `ArgumentNullException`: lockKey or ownerId is null
- `ArgumentException`: lockKey or ownerId is empty
- `OperationCanceledException`: Cancellation requested

**Example:**
```csharp
try
{
    var @lock = await lockService.AcquireAsync("database-migration", "app-instance-1");
    Console.WriteLine($"Lock acquired: {lock.Key}");
}
catch (LockAcquisitionException ex)
{
    Console.WriteLine($"Failed: {ex.Message}");
}
```

---

### TryAcquireAsync

Non-blocking lock acquisition attempt.

```csharp
public Task<Lock?> TryAcquireAsync(
    string lockKey,
    string ownerId,
    TimeSpan? duration = null,
    CancellationToken cancellationToken = default
);
```

**Parameters:**
Same as `AcquireAsync`

**Returns:** `Task<Lock?>` - The lock object if acquired, null if already locked

**Throws:**
- `ArgumentNullException`: lockKey or ownerId is null
- `ArgumentException`: lockKey or ownerId is empty
- `OperationCanceledException`: Cancellation requested

**Example:**
```csharp
var @lock = await lockService.TryAcquireAsync("batch-job", "worker-1");

if (@lock != null)
{
    try
    {
        // Do work
    }
    finally
    {
        await lockService.ReleaseAsync("batch-job", "worker-1");
    }
}
else
{
    Console.WriteLine("Resource is locked");
}
```

---

### ReleaseAsync

Releases a held lock.

```csharp
public Task ReleaseAsync(
    string lockKey,
    string ownerId,
    CancellationToken cancellationToken = default
);
```

**Parameters:**
- `lockKey` (string, required): Lock key
- `ownerId` (string, required): Must match the lock owner
- `cancellationToken` (CancellationToken, optional): For cancellation

**Throws:**
- `LockNotOwnedException`: Lock is not owned by the specified owner
- `LockExpiredException`: Lock has already expired
- `ArgumentNullException`: lockKey or ownerId is null
- `ArgumentException`: lockKey or ownerId is empty

**Example:**
```csharp
try
{
    var @lock = await lockService.AcquireAsync("resource", "owner-1");
    // ... do work ...
}
finally
{
    await lockService.ReleaseAsync("resource", "owner-1");
}
```

---

### RenewAsync

Extends the expiration time of a held lock.

```csharp
public Task RenewAsync(
    string lockKey,
    string ownerId,
    TimeSpan? newDuration = null,
    CancellationToken cancellationToken = default
);
```

**Parameters:**
- `lockKey` (string, required): Lock key
- `ownerId` (string, required): Must match the lock owner
- `newDuration` (TimeSpan?, optional): New duration; uses `DefaultLockDuration` if null
- `cancellationToken` (CancellationToken, optional): For cancellation

**Throws:**
- `LockNotOwnedException`: Lock is not owned by the specified owner
- `LockExpiredException`: Lock has already expired
- `ArgumentNullException`: lockKey or ownerId is null

**Example:**
```csharp
var @lock = await lockService.AcquireAsync("task", "processor", TimeSpan.FromMinutes(5));

// Later, before expiration...
await lockService.RenewAsync("task", "processor", TimeSpan.FromMinutes(5));
```

---

### GetLockAsync

Retrieves information about a lock.

```csharp
public Task<Lock?> GetLockAsync(
    string lockKey,
    CancellationToken cancellationToken = default
);
```

**Parameters:**
- `lockKey` (string, required): Lock key to query
- `cancellationToken` (CancellationToken, optional): For cancellation

**Returns:** `Task<Lock?>` - Lock object if exists, null otherwise

**Example:**
```csharp
var @lock = await lockService.GetLockAsync("my-resource");

if (@lock != null)
{
    Console.WriteLine($"Owned by: {lock.OwnerId}");
    Console.WriteLine($"Expires at: {lock.ExpiresAt}");
}
else
{
    Console.WriteLine("Lock does not exist");
}
```

---

### IsLockedAsync

Checks if a resource is currently locked.

```csharp
public Task<bool> IsLockedAsync(
    string lockKey,
    CancellationToken cancellationToken = default
);
```

**Parameters:**
- `lockKey` (string, required): Lock key to check
- `cancellationToken` (CancellationToken, optional): For cancellation

**Returns:** `Task<bool>` - true if locked, false otherwise

**Example:**
```csharp
if (await lockService.IsLockedAsync("critical-resource"))
{
    Console.WriteLine("Resource is locked");
}
```

---

### GetAllActiveLockAsync

Retrieves all currently held locks.

```csharp
public Task<List<Lock>> GetAllActiveLockAsync(
    CancellationToken cancellationToken = default
);
```

**Parameters:**
- `cancellationToken` (CancellationToken, optional): For cancellation

**Returns:** `Task<List<Lock>>` - List of all active locks

**Example:**
```csharp
var locks = await lockService.GetAllActiveLockAsync();

foreach (var @lock in locks)
{
    var ttl = @lock.ExpiresAt - DateTime.UtcNow;
    Console.WriteLine($"{lock.Key} - {lock.OwnerId} ({ttl.TotalSeconds}s)");
}
```

---

## FencingTokenService

Prevents zombie writes after lock expiration.

```csharp
public class FencingTokenService
{
    public FencingToken IssueToken(string resourceId);
    public bool ValidateToken(string resourceId, FencingToken token);
    public bool IsResourceLocked(string resourceId);
}
```

### IssueToken

Generates a new monotonically increasing token.

```csharp
public FencingToken IssueToken(string resourceId);
```

**Parameters:**
- `resourceId` (string, required): Resource identifier

**Returns:** `FencingToken` - New token with monotonic value

**Example:**
```csharp
var token = tokenService.IssueToken("database-connection");
Console.WriteLine($"Token: {token.Token}");
```

---

### ValidateToken

Verifies that a token is still valid for a resource.

```csharp
public bool ValidateToken(string resourceId, FencingToken token);
```

**Parameters:**
- `resourceId` (string, required): Resource identifier
- `token` (FencingToken, required): Token to validate

**Returns:** `bool` - true if token is valid, false if expired or invalid

**Example:**
```csharp
var token = tokenService.IssueToken("data-store");

if (tokenService.ValidateToken("data-store", token))
{
    // Safe to write to data store
    await WriteDataAsync();
}
else
{
    // Lock expired, token invalid, don't write
    Console.WriteLine("Lock expired - aborting write");
}
```

---

### IsResourceLocked

Checks if a resource is currently locked.

```csharp
public bool IsResourceLocked(string resourceId);
```

**Parameters:**
- `resourceId` (string, required): Resource identifier

**Returns:** `bool` - true if locked, false otherwise

---

## LockMonitor

Manages automatic lock renewal.

```csharp
public class LockMonitor
{
    public void RegisterLock(string lockKey, string ownerId, TimeSpan renewalInterval, TimeSpan lockDuration);
    public void UnregisterLock(string lockKey, string ownerId);
    public void StartMonitoring(TimeSpan checkInterval);
    public Task StopMonitoringAsync();
}
```

### RegisterLock

Registers a lock for automatic renewal.

```csharp
public void RegisterLock(
    string lockKey,
    string ownerId,
    TimeSpan renewalInterval,
    TimeSpan lockDuration
);
```

**Parameters:**
- `lockKey` (string, required): Lock key
- `ownerId` (string, required): Lock owner
- `renewalInterval` (TimeSpan, required): How often to renew the lock
- `lockDuration` (TimeSpan, required): Duration to extend by on each renewal

**Example:**
```csharp
monitor.RegisterLock(
    "long-running-task",
    "worker-1",
    TimeSpan.FromMinutes(5),    // Renew every 5 minutes
    TimeSpan.FromMinutes(10)    // Extend duration by 10 minutes
);
```

---

### UnregisterLock

Removes a lock from automatic renewal.

```csharp
public void UnregisterLock(string lockKey, string ownerId);
```

**Parameters:**
- `lockKey` (string, required): Lock key
- `ownerId` (string, required): Lock owner

---

### StartMonitoring

Starts the background renewal service.

```csharp
public void StartMonitoring(TimeSpan checkInterval);
```

**Parameters:**
- `checkInterval` (TimeSpan, required): How often to check for locks to renew

**Example:**
```csharp
monitor.StartMonitoring(TimeSpan.FromSeconds(1));
```

---

### StopMonitoringAsync

Gracefully stops the monitoring service.

```csharp
public Task StopMonitoringAsync();
```

**Example:**
```csharp
await monitor.StopMonitoringAsync();
```

---

## LockEventSubscriber

Subscribes to lock lifecycle events.

```csharp
public class LockEventSubscriber
{
    public void SubscribeToAcquiredEvent(Action<LockEvent> handler);
    public void SubscribeToReleasedEvent(Action<LockEvent> handler);
    public void SubscribeToRenewedEvent(Action<LockEvent> handler);
    public void SubscribeToFailedEvent(Action<LockEvent> handler);
}
```

### SubscribeToAcquiredEvent

```csharp
public void SubscribeToAcquiredEvent(Action<LockEvent> handler);
```

**Example:**
```csharp
subscriber.SubscribeToAcquiredEvent(@event =>
{
    Console.WriteLine($"Lock acquired: {event.LockKey} by {event.OwnerId}");
});
```

---

### SubscribeToReleasedEvent

```csharp
public void SubscribeToReleasedEvent(Action<LockEvent> handler);
```

---

### SubscribeToRenewedEvent

```csharp
public void SubscribeToRenewedEvent(Action<LockEvent> handler);
```

---

### SubscribeToFailedEvent

```csharp
public void SubscribeToFailedEvent(Action<LockEvent> handler);
```

---

## DistributedLockOptions

Configuration options for dependency injection.

```csharp
public class DistributedLockOptions
{
    // Backend selection
    public BackendType BackendType { get; set; }
    public string ConnectionString { get; set; }

    // Lock timing
    public TimeSpan DefaultLockDuration { get; set; }
    public TimeSpan DefaultAcquisitionTimeout { get; set; }
    public TimeSpan DefaultRenewalInterval { get; set; }

    // Retry strategy
    public AcquisitionMode DefaultAcquisitionMode { get; set; }
    public int DefaultMaxRetries { get; set; }
    public int DefaultRetryDelayMs { get; set; }

    // Features
    public bool EnableAutoRenewal { get; set; }
    public bool UseFencingTokens { get; set; }
    public bool EnableMetrics { get; set; }
    public bool EnableLogging { get; set; }

    // Cache configuration
    public bool EnableCaching { get; set; }
    public int CacheDurationSeconds { get; set; }
    public int MaxCacheSize { get; set; }

    // Capacity
    public int MaxConcurrentLocks { get; set; }

    // Webhooks
    public string? WebhookEndpoint { get; set; }
    public TimeSpan WebhookTimeout { get; set; }
    public bool EnableWebhookRetry { get; set; }
    public int MaxWebhookRetries { get; set; }
}
```

---

## Exception Types

### LockAcquisitionException

Thrown when lock acquisition fails or times out.

```csharp
public class LockAcquisitionException : DistributedLockException
{
    public string LockKey { get; }
    public string OwnerId { get; }
    public TimeSpan Timeout { get; }
}
```

---

### LockNotOwnedException

Thrown when attempting to operate on a lock not owned by the caller.

```csharp
public class LockNotOwnedException : DistributedLockException
{
    public string LockKey { get; }
    public string ExpectedOwner { get; }
    public string ActualOwner { get; }
}
```

---

### LockExpiredException

Thrown when operating on an expired lock.

```csharp
public class LockExpiredException : DistributedLockException
{
    public string LockKey { get; }
    public DateTime ExpirationTime { get; }
}
```

---

### InvalidFencingTokenException

Thrown when fencing token validation fails.

```csharp
public class InvalidFencingTokenException : DistributedLockException
{
    public string ResourceId { get; }
    public long ExpectedToken { get; }
    public long ProvidedToken { get; }
}
```

---

### DistributedLockException

Base exception for all lock-related errors.

```csharp
public class DistributedLockException : Exception
{
    public string? ErrorCode { get; set; }
    public DateTime OccurredAt { get; set; }
}
```

---

## Lock Model

Represents a lock instance.

```csharp
public record Lock(
    string Key,              // Unique lock identifier
    string OwnerId,          // Owner identifier
    DateTime CreatedAt,      // When lock was acquired
    DateTime ExpiresAt,      // When lock expires
    LockStatus Status,       // Current status
    int RenewalCount         // Number of times renewed
);
```

---

## LockMetrics

Metrics from the metrics collection worker.

```csharp
public record LockMetrics(
    long TotalAcquisitionAttempts,
    long SuccessfulAcquisitions,
    long FailedAcquisitions,
    decimal AcquisitionSuccessRate,
    double AverageAcquisitionTimeMs,
    int CurrentActiveLocks,
    long TotalLocksCreated,
    long TotalLocksReleased
);
```

---

## Enums

### BackendType

```csharp
public enum BackendType
{
    InMemory,
    Redis,
    PostgreSQL,
    SQLite
}
```

---

### AcquisitionMode

```csharp
public enum AcquisitionMode
{
    NonBlocking,           // Return immediately if locked
    Blocking,              // Retry until acquired
    ExponentialBackoff,    // 2^attempt delay
    LinearBackoff          // Linear delay increase
}
```

---

### LockStatus

```csharp
public enum LockStatus
{
    Active,                // Lock is valid
    Expired,               // Lock has expired
    Released,              // Lock was released
    Abandoned              // Lock abandoned without release
}
```

---

## Extension Methods

### Service Collection Extensions

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDistributedLocking(
        this IServiceCollection services,
        Action<DistributedLockOptions> configureOptions
    );
}
```

**Usage:**
```csharp
services.AddDistributedLocking(options =>
{
    options.BackendType = BackendType.Redis;
    options.ConnectionString = "localhost:6379";
});
```
