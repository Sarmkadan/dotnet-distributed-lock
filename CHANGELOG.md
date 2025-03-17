# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.0] - 2026-01-15

### Added

- **Webhook Integration**: Publish lock events to external HTTP endpoints for monitoring system integration
  - New `WebhookPublisher` service for event delivery with retry support
  - Configuration options: `WebhookEndpoint`, `WebhookTimeout`, `EnableWebhookRetry`, `MaxWebhookRetries`
  - Supports event filtering and batching

- **Cache Manager**: In-memory caching layer to reduce backend load
  - Configurable TTL-based cache with LRU eviction
  - Automatic cache invalidation on lock updates
  - Configuration: `EnableCaching`, `CacheDurationSeconds`, `MaxCacheSize`

- **Enhanced Metrics**: Extended metrics collection for better observability
  - Lock contention indicators
  - Renewal success rates
  - Backend latency tracking
  - Percentile-based latency metrics (p50, p95, p99)

- **Event Filtering**: Subscribe to specific event types or lock key patterns
  - Pattern matching support for lock keys
  - Event priority levels for critical operations

### Changed

- **Breaking Change**: `ILockRepository.CreateLockAsync()` now throws `LockAcquisitionException` instead of returning null
  - Allows consistent error handling across backends
  - Migration guide: Catch exception instead of checking for null

- **Performance**: Optimized Redis backend with pipeline support for batch operations
  - Reduced round-trip time for multiple operations
  - Better connection reuse with connection multiplexing

- **Logging**: Improved structured logging with correlation IDs
  - Trace lock operations across service boundaries
  - JSON-formatted logs for better log aggregation

### Fixed

- Fixed race condition in SQLite backend during concurrent lock acquisition
- Fixed PostgreSQL trigger cleanup timing issues with high-frequency operations
- Fixed memory leak in `LockCacheManager` with long-lived applications

### Deprecated

- `GetMetricsAsync()` in favor of `GetMetrics()` (synchronous access)

## [1.1.0] - 2025-10-20

### Added

- **Fencing Tokens**: Monotonic token generation and validation to prevent zombie writes
  - `FencingTokenService` for token management
  - Token validation before operations: `ValidateToken(resourceId, token)`
  - Automatic token invalidation on lock expiration

- **Lock Monitoring Service**: Automatic lock renewal for long-running operations
  - `LockMonitor` for background renewal management
  - Register locks for auto-renewal: `RegisterLock(key, owner, interval, duration)`
  - Graceful startup/shutdown with cleanup

- **Event System**: Pub/sub system for lock lifecycle events
  - `LockEventBus` for event publishing
  - `LockEventSubscriber` for event subscription
  - Events: Acquired, Released, Renewed, Failed

- **Multiple Backend Support**: Support for Redis, PostgreSQL, and SQLite
  - `RedisLockRepository`: High-performance Redis backend
  - `PostgresLockRepository`: Strong consistency with PostgreSQL
  - `SqliteLockRepository`: Lightweight file-based backend

- **Configuration Options**: Comprehensive configuration support
  - Backend selection
  - Lock timing parameters (duration, timeout, renewal interval)
  - Acquisition strategies (Blocking, NonBlocking, ExponentialBackoff, LinearBackoff)
  - Feature toggles for auto-renewal, fencing tokens, metrics

### Changed

- Upgraded to .NET 10.0 (from .NET 9.0)
- Improved error messages with more context
- Enhanced API documentation with XML comments

### Fixed

- Fixed timeout handling in PostgreSQL backend

## [1.0.0] - 2025-08-01

### Added

- **Core Lock Service**: Basic lock acquisition, renewal, and release operations
  - `ILockService` interface with primary lock operations
  - `AcquireAsync()` for blocking lock acquisition with retry logic
  - `TryAcquireAsync()` for non-blocking attempts
  - `ReleaseAsync()` for lock release
  - `RenewAsync()` for lock duration extension
  - `GetLockAsync()` and `IsLockedAsync()` for lock inspection
  - `GetAllActiveLockAsync()` for listing all active locks

- **In-Memory Backend**: Simple in-memory lock storage
  - `InMemoryLockRepository` implementation
  - Uses `ReaderWriterLockSlim` for thread-safe access
  - Suitable for development and testing

- **Thread Safety**: Concurrent-safe implementation
  - All operations are atomic
  - Proper lock semantics with ownership validation
  - Support for multiple concurrent owners

- **Async/Await Support**: Fully asynchronous API
  - CancellationToken support on all operations
  - Compatible with async/await patterns

- **Structured Logging**: Microsoft.Extensions.Logging integration
  - Debug, Info, Warning, and Error level logs
  - Log messages for all lock lifecycle events

- **Exception Handling**: Custom exception types
  - `LockAcquisitionException`: Lock acquisition failed
  - `LockNotOwnedException`: Operation not permitted (ownership mismatch)
  - `LockExpiredException`: Operation on expired lock
  - `DistributedLockException`: Base exception type

- **Dependency Injection**: Integration with Microsoft.Extensions.DependencyInjection
  - `AddDistributedLocking()` extension method
  - Configuration via `DistributedLockOptions`
  - Easy service registration

- **Metrics Collection**: Performance tracking
  - `LockMetrics` with acquisition statistics
  - Success rate calculation
  - Average acquisition time
  - Active lock count

- **API Endpoints** (in Web API controller)
  - GET `/api/locks` - List active locks
  - POST `/api/locks/{key}/acquire` - Acquire lock
  - POST `/api/locks/{key}/release` - Release lock
  - GET `/api/locks/{key}` - Get lock details
  - GET `/api/health` - Health check
  - GET `/api/metrics` - Performance metrics

- **Comprehensive Documentation**
  - README with quick start guide
  - API reference documentation
  - Configuration examples
  - Troubleshooting guide

### Security

- Ownership-based access control
- Lock validation before operations
- Exception-based error reporting

---

## Upgrade Guide

### From 1.1.0 to 1.2.0

#### Breaking Changes

If you're calling `ILockRepository.CreateLockAsync()` directly:

```csharp
// Old code (1.1.0)
var newLock = await repository.CreateLockAsync(...);
if (newLock == null)
{
    // Handle failure
}

// New code (1.2.0)
try
{
    var newLock = await repository.CreateLockAsync(...);
}
catch (LockAcquisitionException)
{
    // Handle failure
}
```

#### Recommended Updates

1. Enable webhook integration for monitoring:
```csharp
options.WebhookEndpoint = "https://monitoring.example.com/locks";
```

2. Enable caching to improve performance:
```csharp
options.EnableCaching = true;
options.CacheDurationSeconds = 30;
```

3. Use new metrics methods:
```csharp
var metrics = lockService.GetMetrics(); // Synchronous
```

### From 1.0.0 to 1.1.0

1. Update service registration to include new services:
```csharp
services.AddDistributedLocking(options =>
{
    options.BackendType = BackendType.Redis;
    options.ConnectionString = "localhost:6379";
    
    // New in 1.1.0
    options.UseFencingTokens = true;
    options.EnableAutoRenewal = true;
});
```

2. If using auto-renewal:
```csharp
var monitor = serviceProvider.GetRequiredService<LockMonitor>();
monitor.RegisterLock(lockKey, ownerId, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
monitor.StartMonitoring(TimeSpan.FromSeconds(1));
```

3. Subscribe to events:
```csharp
var subscriber = serviceProvider.GetRequiredService<LockEventSubscriber>();
subscriber.SubscribeToAcquiredEvent(@event =>
{
    Console.WriteLine($"Lock acquired: {event.LockKey}");
});
```

---

## Known Issues

### v1.2.0

- Webhook delivery may retry indefinitely if endpoint is unreachable - fixed in development branch
- Cache invalidation latency up to TTL duration - by design

### v1.1.0

- PostgreSQL backend does not support connection pooling configuration - fixed in 1.2.0
- SQLite shared cache mode has concurrency limitations - documented in troubleshooting

---

## Migration from Other Libraries

### From Medallion

```csharp
// Old: Medallion
using (var lockHandle = await client.AcquireLockAsync(...))
{
    // Work
}

// New: SarmKadan.DistributedLock
using (await lockService.AcquireAsync(...))
{
    // Work
}
```

### From DistributedLock

```csharp
// Old: DistributedLock
await @lock.DeferAsync(...);

// New: SarmKadan.DistributedLock
monitor.RegisterLock(...); // Auto-renewal instead
```

---

## Roadmap

### Upcoming Features (v1.3.0)

- [ ] MongoDB backend support
- [ ] DynamoDB backend support
- [ ] Lock priority levels
- [ ] Deadlock detection and resolution
- [ ] Metrics export (Prometheus, StatsD)

### Future Considerations

- Read-write locks (shared vs. exclusive)
- Semaphore support (multiple holders)
- Distributed transactions support
- Integration with distributed tracing (OpenTelemetry)
