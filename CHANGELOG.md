# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2025-09-20

### Added

- **Caching Layer**: In-memory caching layer to reduce backend round-trips
  - `LockCacheManager` with TTL-based expiry and LRU eviction
  - Configuration: `EnableCaching`, `CacheDurationSeconds`, `MaxCacheSize`
  - Automatic cache invalidation on lock updates and releases

- **Webhook Integration**: Publish lock events to external HTTP endpoints
  - `WebhookPublisher` with retry support and configurable timeout
  - Configuration: `WebhookEndpoint`, `WebhookTimeout`, `EnableWebhookRetry`, `MaxWebhookRetries`

- **Enhanced Metrics**: Extended metrics for production observability
  - Lock contention tracking per key
  - Renewal success rates and backend latency percentiles (p50, p95, p99)
  - `ContentionMetrics` model for contention indicators

- **API Controllers**: HTTP endpoints for lock management and health checks
  - `DistributedLockController`, `HealthCheckController`, `MetricsController`
  - Authentication, rate limiting, and request logging middleware

- **Background Workers**: Worker services for operational concerns
  - `LockRenewalWorker`: Background renewal processing
  - `LockCleanupWorker`: Expired lock cleanup
  - `HealthMonitoringWorker`: Periodic health checks
  - `MetricsCollectionWorker`: Periodic metrics aggregation

- **Formatters and Exporters**: Lock state serialization
  - `JsonLockSerializer`, `XmlLockSerializer`, `CsvLockExporter`

- **DeadlockDetector**: Background detection of potential circular lock waits

### Fixed

- Fixed race condition in SQLite backend during concurrent lock acquisition under high load
- Fixed memory growth in `LockCacheManager` when lock keys accumulate without eviction
- Fixed PostgreSQL advisory lock cleanup on abnormal connection termination

### Changed

- Marked `GetMetricsAsync()` deprecated; prefer synchronous `GetMetrics()`
- Improved structured logging with correlation IDs across service boundaries

## [0.8.0] - 2025-07-19

### Added

- **Event System**: Pub/sub system for lock lifecycle events
  - `LockEventBus` for internal event dispatch
  - `LockEventPublisher` for raising events from the lock service
  - `LockEventSubscriber` for consumer subscriptions
  - Events: `LockAcquired`, `LockReleased`, `LockRenewed`, `LockFailed`

- **SQLite Backend**: Lightweight file-based lock storage
  - `SqliteLockRepository` using `Microsoft.Data.Sqlite`
  - Suitable for single-machine and embedded deployments
  - Connection string options: file, shared-cache, and `:memory:` modes

- **Multiple Acquisition Modes**: Configurable retry and backoff strategies
  - `AcquisitionMode.Blocking`: spin-wait with configurable max retries
  - `AcquisitionMode.NonBlocking`: single attempt, returns null on failure
  - `AcquisitionMode.ExponentialBackoff`: doubling delay between attempts
  - `AcquisitionMode.LinearBackoff`: fixed delay between attempts
  - `RetryPolicyHelper` for retry orchestration

### Changed

- `ServiceCollectionExtensions.AddDistributedLocking()` now registers all backend repositories, resolving the active one via `BackendType` configuration
- Default `AcquisitionMode` changed from `NonBlocking` to `Blocking` to reduce accidental failure paths

### Fixed

- Fixed `LockExpiredException` not being thrown when renewing an already-expired lock
- Fixed missing `CancellationToken` propagation in `PostgresLockRepository`

## [0.5.0] - 2025-05-24

### Added

- **Fencing Tokens**: Monotonic token generation to prevent zombie writes
  - `FencingTokenService`: issue and validate tokens per resource
  - `FencingToken` model with monotonically increasing sequence counter
  - `InvalidFencingTokenException` thrown when a stale token is presented
  - Configuration toggle: `UseFencingTokens = true`

- **Lock Monitoring Service**: Automatic lock renewal for long-running operations
  - `LockMonitor` background service for renewal management
  - `RegisterLock(key, owner, renewalInterval, duration)` and `UnregisterLock()`
  - Configurable renewal interval via `DefaultRenewalInterval`

- **PostgreSQL Backend**: Strong-consistency lock storage
  - `PostgresLockRepository` using `Npgsql`
  - Schema auto-creation on first use
  - Row-level locking with `SELECT ... FOR UPDATE SKIP LOCKED`

- **Extension Utilities**: Helper extensions for common patterns
  - `StringExtensions`, `DateTimeExtensions`, `CollectionExtensions`, `ObjectExtensions`
  - `ValidationHelper` for guard clauses

### Changed

- `LockService.AcquireAsync()` now validates fencing token on each write when `UseFencingTokens` is enabled
- `DistributedLockOptions` extended with fencing token and renewal configuration

### Fixed

- Fixed `InMemoryLockRepository` not respecting `CancellationToken` during blocking acquisition
- Fixed timeout calculation rounding error causing locks to expire one tick early

## [0.2.0] - 2025-03-15

### Added

- **Redis Backend**: High-throughput distributed lock storage
  - `RedisLockRepository` using `StackExchange.Redis`
  - Atomic acquisition via Lua script (`SET NX PX`)
  - Automatic expiry managed by Redis TTL

- **Dependency Injection Integration**: Full DI support
  - `ServiceCollectionExtensions.AddDistributedLocking()` extension method
  - `DistributedLockOptions` configuration via `IOptions<T>`
  - Backend registration by `BackendType` enum

- **Comprehensive Exceptions**: Typed exception hierarchy
  - `DistributedLockException` — base type
  - `LockAcquisitionException` — acquisition failure
  - `LockNotOwnedException` — ownership mismatch on release or renew
  - `LockExpiredException` — operation on an expired lock

- **Metrics Collection**: Performance counters on the lock service
  - `LockMetrics` model: acquisition attempts, successes, failures, active count
  - `AcquisitionSuccessRate` and `AverageAcquisitionTimeMs` calculations

- **Structured Logging**: `Microsoft.Extensions.Logging` integration
  - Debug/Info/Warning/Error log points at each lock lifecycle transition

### Changed

- `ILockRepository` interface updated with explicit `CreateLockAsync`, `DeleteLockAsync`, `GetLockAsync`, `RenewLockAsync`
- `BackendType` enum added: `InMemory`, `Redis`, `PostgreSQL`, `SQLite`

### Fixed

- Fixed `InMemoryLockRepository` allowing acquisition of locks that had logically expired but not yet been cleaned up

## [0.1.0] - 2025-02-01

### Added

- **Core Lock Service**: Initial implementation of `ILockService`
  - `AcquireAsync()`: blocking acquisition with retry loop
  - `TryAcquireAsync()`: non-blocking single attempt
  - `ReleaseAsync()`: lock release with ownership validation
  - `RenewAsync()`: extend lock expiry
  - `GetLockAsync()` and `IsLockedAsync()` for inspection
  - `GetAllActiveLockAsync()` for listing all held locks

- **In-Memory Backend**: Thread-safe in-memory lock repository
  - `InMemoryLockRepository` using `ReaderWriterLockSlim`
  - Suitable for development, testing, and single-process deployments

- **Core Models**
  - `Lock`: lock state with key, owner, expiry, and status
  - `LockAcquisition`: result wrapper for acquisition operations
  - `LockConfiguration`: per-lock configuration overrides
  - `LockRequestContext`: contextual metadata for lock operations

- **Constants and Enumerations**
  - `LockConstants`: default durations and limits
  - `LockStatus`: `Active`, `Expired`, `Released`
  - `AcquisitionMode`: `Blocking`, `NonBlocking`

- **Solution Structure**: initial repository layout
  - `src/Core/` for interfaces, models, services, and exceptions
  - `tests/` for unit tests using xUnit, Moq, and FluentAssertions
  - `.editorconfig`, `.gitignore`, `Makefile`, `docker-compose.yml`

---

## Upgrade Guide

### From 0.8.0 to 1.0.0

Enable caching for read-heavy workloads:

```csharp
options.EnableCaching = true;
options.CacheDurationSeconds = 30;
```

Add webhook notifications for monitoring:

```csharp
options.WebhookEndpoint = "https://monitoring.example.com/locks";
options.EnableWebhookRetry = true;
```

Replace any calls to `GetMetricsAsync()` with synchronous `GetMetrics()`.

### From 0.5.0 to 0.8.0

Update service registration to include the desired `AcquisitionMode`:

```csharp
services.AddDistributedLocking(options =>
{
    options.BackendType = BackendType.Redis;
    options.DefaultAcquisitionMode = AcquisitionMode.ExponentialBackoff;
    options.DefaultMaxRetries = 5;
});
```

Subscribe to lock lifecycle events:

```csharp
var subscriber = serviceProvider.GetRequiredService<LockEventSubscriber>();
subscriber.SubscribeToAcquiredEvent(e => Console.WriteLine($"Acquired: {e.LockKey}"));
```

### From 0.2.0 to 0.5.0

Enable fencing tokens if your workload requires zombie-write protection:

```csharp
options.UseFencingTokens = true;
```

Register `LockMonitor` for long-running operations that need auto-renewal:

```csharp
var monitor = serviceProvider.GetRequiredService<LockMonitor>();
monitor.RegisterLock(lockKey, ownerId, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
monitor.StartMonitoring(TimeSpan.FromSeconds(1));
```

### From 0.1.0 to 0.2.0

Add the backend package and update service registration:

```csharp
// Redis
services.AddDistributedLocking(options =>
{
    options.BackendType = BackendType.Redis;
    options.ConnectionString = "localhost:6379";
});
```

---

## Known Issues

### v1.0.0

- Webhook delivery retries indefinitely if the endpoint is unreachable and `EnableWebhookRetry = true` — set `MaxWebhookRetries` to bound retries
- Cache invalidation is eventually consistent up to `CacheDurationSeconds`

### v0.8.0

- SQLite shared-cache mode has write-concurrency limitations — use file mode with WAL for higher concurrency

---

## Roadmap

### Upcoming (v1.1.0)

- [ ] MongoDB backend support
- [ ] DynamoDB backend support
- [ ] Semaphore support (multiple concurrent holders)
- [ ] OpenTelemetry tracing integration
- [ ] Prometheus metrics export
