# Architecture Overview

## System Design

SarmKadan.DistributedLock is built on a layered architecture that separates concerns and enables pluggable backends.

```
┌─────────────────────────────────────────────────────────────────┐
│                         API Layer                               │
│  ILockService, FencingTokenService, LockMonitor, EventSystem  │
└────────────────────────┬────────────────────────────────────────┘
                         │
       ┌─────────────────┼─────────────────┐
       │                 │                 │
┌──────▼──────┐  ┌──────▼──────┐  ┌──────▼──────┐
│   Cache     │  │   Metrics   │  │   Events   │
│   Manager   │  │ Collection  │  │    & Bus   │
└──────┬──────┘  └──────┬──────┘  └──────┬──────┘
       │                 │                 │
       └─────────────────┼─────────────────┘
                         │
              ┌──────────▼──────────┐
              │   Lock Service      │
              │   (Core Logic)      │
              └──────────┬──────────┘
                         │
              ┌──────────▼──────────┐
              │ ILockRepository     │
              │ (Abstraction)       │
              └──────────┬──────────┘
                         │
        ┌────────┬───────┼───────┬────────┐
        │        │       │       │        │
    ┌───▼─┐ ┌───▼─┐ ┌───▼─┐ ┌──▼────┐ ┌─▼──┐
    │Redis│ │Pg  │ │Sqli │ │Memory  │ │API │
    └─────┘ └─────┘ └─────┘ └────────┘ └────┘
        │        │       │       │
    ┌───▼─────────▼───────▼───────▼────┐
    │    Backend Storage Systems        │
    │  (Redis, PostgreSQL, SQLite)     │
    └──────────────────────────────────┘
```

## Core Components

### 1. ILockService

The primary public API providing:

- **Lock Acquisition**: `AcquireAsync()` and `TryAcquireAsync()`
- **Lock Release**: `ReleaseAsync()`
- **Lock Renewal**: `RenewAsync()`
- **Lock Querying**: `GetLockAsync()`, `IsLockedAsync()`, `GetAllActiveLockAsync()`

Implementation: `LockService` class in `src/Core/Services/`

**Key Responsibilities:**
- Orchestrate lock operations across repositories
- Handle retry logic and backoff strategies
- Manage fencing tokens
- Trigger events for lock lifecycle
- Enforce access control (owner validation)

### 2. ILockRepository

Abstract interface for backend storage with implementations:

- **RedisLockRepository**: Uses Redis atomic operations
- **PostgresLockRepository**: Uses database transactions and row locking
- **SqliteLockRepository**: Uses WAL mode and PRAGMA busy_timeout
- **InMemoryLockRepository**: Uses ReaderWriterLockSlim

**Required Methods:**
```csharp
Task<Lock> CreateLockAsync(Lock @lock);
Task<bool> UpdateLockAsync(Lock @lock);
Task<bool> DeleteLockAsync(string lockKey);
Task<Lock?> GetLockAsync(string lockKey);
Task<List<Lock>> GetAllActiveLockAsync();
Task<bool> ExistsAsync(string lockKey);
```

### 3. FencingTokenService

Implements the fencing token algorithm to prevent zombie writes:

```csharp
// Issue a monotonically increasing token for a resource
FencingToken token = tokenService.IssueToken("resource-id");

// Validate token before critical operations
if (tokenService.ValidateToken("resource-id", token))
{
    // Safe to proceed - lock is valid
}
```

**Algorithm:**
1. Each resource has an associated monotonic counter
2. Lock acquisition increments counter and issues token
3. Before write operations, verify token matches current counter
4. Expired locks get new counter, invalidating old tokens

### 4. LockMonitor

Background service for automatic lock renewal:

**Features:**
- Register locks for monitoring
- Periodic renewal checks
- Event publishing on renewal
- Graceful shutdown with cleanup

**Usage Pattern:**
```csharp
monitor.RegisterLock(lockKey, ownerId, renewalInterval, duration);
monitor.StartMonitoring(checkInterval);
// ... do work ...
await monitor.StopMonitoringAsync();
```

### 5. LockCacheManager

In-memory cache layer to reduce backend load:

- Caches lock existence queries
- Configurable TTL (default 30 seconds)
- LRU eviction policy
- Automatic invalidation on updates

**Configuration:**
```csharp
options.EnableCaching = true;
options.CacheDurationSeconds = 30;
options.MaxCacheSize = 10000;
```

### 6. LockEventSystem

Event publisher and subscriber system for lock lifecycle:

**Events:**
- `LockAcquiredEvent`: Emitted when lock is successfully acquired
- `LockReleasedEvent`: Emitted when lock is released
- `LockRenewedEvent`: Emitted when lock is renewed
- `LockFailedEvent`: Emitted when acquisition fails

**Subscribers:**
- `LockEventSubscriber`: In-process event handling
- `WebhookPublisher`: HTTP webhook delivery for external systems

### 7. MetricsCollectionWorker

Collects and aggregates performance metrics:

**Metrics Tracked:**
- Total acquisition attempts
- Successful vs. failed acquisitions
- Average acquisition time
- Current active locks
- Success rate percentage
- Contention indicators

**Access Pattern:**
```csharp
if (lockService is LockService concrete)
{
    var metrics = concrete.GetMetrics();
    Console.WriteLine(metrics.AcquisitionSuccessRate);
}
```

## Data Flow

### Lock Acquisition Flow

```
AcquireAsync() called
    │
    ├─► Validate parameters
    │
    ├─► Check cache (if enabled)
    │   └─► Hit? Return cached result
    │
    ├─► Check fencing token (if enabled)
    │   └─► Invalid? Throw InvalidFencingTokenException
    │
    ├─► Call Repository.CreateLockAsync()
    │   │
    │   ├─► Redis: SETNX with TTL
    │   ├─► PostgreSQL: INSERT with conflict handling
    │   ├─► SQLite: INSERT OR IGNORE
    │   └─► InMemory: Lock/Check/Add pattern
    │
    ├─► If failed, apply backoff strategy
    │   ├─► NonBlocking: Return null
    │   ├─► Blocking: Retry until timeout
    │   ├─► ExponentialBackoff: Delay * 2^attempt
    │   └─► LinearBackoff: Delay * attempt
    │
    ├─► Success? Update cache
    │
    ├─► Publish LockAcquiredEvent
    │
    └─► Return Lock object
```

### Lock Renewal Flow

```
RenewAsync() called
    │
    ├─► Validate ownership
    │   └─► Not owned? Throw LockNotOwnedException
    │
    ├─► Update TTL in repository
    │   ├─► Redis: EXPIRE or PEXPIRE
    │   ├─► PostgreSQL: UPDATE expires_at
    │   ├─► SQLite: UPDATE expires_at
    │   └─► InMemory: Update expiration time
    │
    ├─► Invalidate cache entry
    │
    ├─► Issue new fencing token (if enabled)
    │
    ├─► Publish LockRenewedEvent
    │
    └─► Return success
```

### Backend Comparison

#### Redis Backend

**Strengths:**
- Atomic operations
- Sub-millisecond latency
- Horizontal scaling with clustering
- Automatic key expiration (TTL)

**Implementation:**
- Uses SETNX for atomic acquire
- Uses WATCH/MULTI/EXEC for transactions
- Uses EXPIRE for automatic cleanup

#### PostgreSQL Backend

**Strengths:**
- ACID compliance
- Complex query capabilities
- Audit trail
- Integration with existing systems

**Implementation:**
- Row-level locking (FOR UPDATE)
- Triggers for automatic cleanup
- Transaction isolation level: SERIALIZABLE

#### SQLite Backend

**Strengths:**
- Zero configuration
- File-based
- Suitable for single-machine

**Implementation:**
- Uses journal mode: WAL (Write-Ahead Logging)
- PRAGMA busy_timeout for lock wait handling
- REPLACE INTO for atomic operations

#### In-Memory Backend

**Strengths:**
- Maximum performance
- No external dependencies
- Great for testing

**Implementation:**
- ReaderWriterLockSlim for concurrent access
- Dictionary<string, Lock> for storage
- Timer-based expiration cleanup

## Concurrency Model

### Lock Acquisition Isolation

**Redis:**
```lua
-- Atomic SETNX operation
if redis.call('EXISTS', key) == 0 then
    redis.call('SETEX', key, duration, ownerId)
    return 1
else
    return 0
end
```

**PostgreSQL:**
```sql
BEGIN;
LOCK TABLE locks IN EXCLUSIVE MODE;
INSERT INTO locks (...) VALUES (...)
  ON CONFLICT DO NOTHING;
COMMIT;
```

**SQLite:**
```sql
BEGIN IMMEDIATE;
INSERT OR IGNORE INTO locks (...) VALUES (...);
COMMIT;
```

### Automatic Cleanup

**Redis:**
- EXPIRE automatically removes expired keys

**PostgreSQL:**
- Trigger-based cleanup on access
- Vacuum removes stale rows

**SQLite:**
- Background cleanup job
- PRAGMA auto_vacuum

## Extension Points

### Custom Repository Implementation

Implement `ILockRepository` to support new backends:

```csharp
public class CustomLockRepository : ILockRepository
{
    public Task<Lock> CreateLockAsync(Lock @lock) { ... }
    public Task<bool> UpdateLockAsync(Lock @lock) { ... }
    public Task<bool> DeleteLockAsync(string lockKey) { ... }
    // ... implement other methods
}

// Register in DI
services.AddScoped<ILockRepository, CustomLockRepository>();
```

### Custom Event Handlers

Subscribe to lock events for custom behavior:

```csharp
var subscriber = serviceProvider.GetRequiredService<LockEventSubscriber>();

subscriber.SubscribeToAcquiredEvent(@event =>
{
    // Custom handling: logging, metrics, alerts, etc.
});
```

### Webhook Integration

Publish events to external systems:

```csharp
services.AddDistributedLocking(options =>
{
    options.WebhookEndpoint = "https://monitoring.example.com/locks";
    options.EnableWebhookRetry = true;
    options.MaxWebhookRetries = 3;
});
```

## Thread Safety

All components are designed for concurrent access:

- **ILockService**: Thread-safe API
- **ILockRepository**: Thread-safe implementations
- **FencingTokenService**: Atomic token generation
- **LockMonitor**: Concurrent lock registry
- **LockEventBus**: Thread-safe event publishing

## Performance Optimization

### Caching Strategy

1. **Query Cache**: Cache lock existence
2. **TTL**: Auto-invalidate after 30 seconds
3. **LRU**: Evict least-recently-used entries
4. **Invalidation**: Clear cache on write operations

### Retry Strategy

1. **NonBlocking**: No retries, immediate return
2. **Blocking**: Exponential backoff up to timeout
3. **ExponentialBackoff**: 2^attempt delay
4. **LinearBackoff**: Linear delay increase

### Connection Pooling

- Redis: Built-in connection multiplexing
- PostgreSQL: Connection pool (default 25)
- SQLite: Single connection with JOURNAL=WAL

## Security Considerations

### Ownership Verification

All operations verify owner matches lock holder:

```csharp
if (@lock.OwnerId != requestedOwnerId)
    throw new LockNotOwnedException();
```

### Fencing Tokens

Prevent zombie writes after expiration:

```csharp
if (!tokenService.ValidateToken(resourceId, token))
    throw new InvalidFencingTokenException();
```

### Connection Security

- Redis: Supports password authentication
- PostgreSQL: SSL/TLS support
- SQLite: File permissions

## Monitoring and Observability

### Metrics

- Acquisition success rate
- Lock contention indicators
- Average acquisition time
- Active lock count

### Logging

Comprehensive structured logging at key points:
- Lock acquisition started/completed
- Lock renewal started/completed
- Lock conflicts
- Backend errors

### Events

Publish events for external monitoring:
- Lock acquired
- Lock released
- Lock renewed
- Acquisition failed

## Disaster Recovery

### Data Persistence

- **Redis**: Configurable persistence (RDB, AOF)
- **PostgreSQL**: Transaction log WAL
- **SQLite**: File-based with WAL mode
- **InMemory**: Not persistent (dev only)

### Lock Expiration

Automatic cleanup prevents orphaned locks:
- TTL-based expiration (primary)
- Trigger-based cleanup (PostgreSQL)
- Background cleanup job (SQLite, InMemory)

### Failover

- **Redis**: Sentinel and Cluster support
- **PostgreSQL**: Replication support
- **SQLite**: Manual backup strategy
