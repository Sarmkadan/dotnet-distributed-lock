# Architecture

This document describes the actual structure of the codebase as it exists today - what the
pieces are, why they are shaped this way, and where the sharp edges are.

## High-level view

The solution is a distributed lock library for .NET with pluggable storage backends,
plus optional infrastructure around the core (HTTP API surface, background workers,
an event bus, caching helpers and serializers).

```
        ILockService (src/Core/Services/ILockService.cs)
              │
        LockService  ── LockMetrics (in-process counters)
              │
        ILockRepository (src/Core/Repository/ILockRepository.cs)
              │
   ┌──────────┼──────────────┬──────────────────┐
InMemory    Redis        PostgreSQL          SQLite
(Core/      (Backends/   (Backends/          (Backends/
Repository) Redis)       PostgreSQL)         SQLite)
```

Everything above `ILockRepository` is backend-agnostic. The repository interface is the
single seam between lock semantics and storage.

## Module breakdown

### src/Core - the library proper

- **Models** - `Lock` (key, owner, expiry, `FencingToken`, `LockStatus`), `LockAcquisition`
  (records attempts during a blocking acquire), `LockConfiguration`, `LockMetrics`,
  `ContentionMetrics`, `LockRequestContext`.
- **Services**
  - `LockService` (`ILockService`) - the core orchestrator. `TryAcquireAsync` is a single
    non-blocking attempt returning `(bool Success, Lock?, string? ErrorMessage)`;
    `AcquireAsync` wraps it in a retry loop with exponential backoff and an overall
    acquisition timeout, throwing `LockAcquisitionException` on exhaustion. It also exposes
    renewal (both owner-based `RenewAsync` and fencing-token-based `RenewLockAsync`),
    release, and query operations, and records timings into an in-process `LockMetrics`
    instance reachable via `LockService.GetMetrics()`.
  - `ILockRetryPolicy` / `DefaultLockRetryPolicy` - encapsulates max retries, initial/max
    delay and jitter factor; `GetDelay(attempt)` computes the backoff. The default policy is
    built from `DistributedLockOptions` in DI, and `LockService` consumes it for its
    blocking-acquire loop (falling back to `DefaultLockRetryPolicy` when constructed
    without one, so plain `new LockService(repo, logger)` still works).
  - `FencingTokenService` - issues monotonically increasing tokens per resource so a client
    holding a stale lock cannot perform a "zombie write". Validation also happens at the
    repository level via `ILockRepository.ValidateFencingTokenAsync`.
  - `LockMonitor` - tracks registered locks and drives periodic renewal checks.
  - `DeadlockDetector` (`IDeadlockDetector`) - builds a wait-for graph from lock
    requests/holders and detects cycles.
- **Repository** - `ILockRepository` contract plus the `InMemoryLockRepository` reference
  implementation. The contract is deliberately storage-flavored (`AcquireAsync`,
  `RenewAsync`, `ReleaseAsync(key, ownerId)`, `GetByOwnerAsync`, `DeleteExpiredLockAsync`,
  `ValidateFencingTokenAsync`, ...) rather than CRUD, so each backend can map an operation
  to its most atomic native primitive instead of emulating generic update semantics.
- **Configuration** - `DistributedLockOptions` (backend type, connection string, default
  durations/timeouts, retry policy knobs, `UseFencingTokens`, `EnableAutoRenewal`,
  `MaxConcurrentLocks`, ...) with `Validate()` returning human-readable errors, and
  `ServiceCollectionExtensions.AddDistributedLocking(...)` which fails fast on invalid
  options at registration time instead of at first use.
- **Exceptions** - one type per failure mode (`LockAcquisitionException`,
  `LockNotOwnedException`, `LockExpiredException`, `LockRenewalFailedException`,
  `InvalidFencingTokenException`), all deriving from `DistributedLockException` so callers
  can catch broadly or narrowly.

### src/Backends - storage implementations

Each backend implements `ILockRepository`:

- **Redis** (`RedisLockRepository`) - atomic key operations with TTL; expiry is delegated
  to Redis itself.
- **PostgreSQL** (`PostgresLockRepository`) - relational storage; acquisition relies on
  conflict handling on the lock key, expiry is checked against `expires_at`.
- **SQLite** (`SqliteLockRepository`) - file-based, single-node; useful for tests and
  single-machine coordination without extra infrastructure.
- **InMemory** (in Core) - dictionary-backed, for tests and in-process scenarios only.

Trade-off recorded here on purpose: Redis gives the lowest latency and free TTL cleanup but
weaker durability guarantees; PostgreSQL gives ACID and auditability at higher latency;
SQLite trades multi-node capability for zero setup. The library does not attempt
Redlock-style multi-node quorum - one backend instance is the source of truth.

### src/Events - lifecycle events

- `LockEvent` hierarchy (`LockAcquiredEvent`, `LockReleasedEvent`, `LockRenewedEvent`,
  `LockExpiredEvent`, `LockFailedEvent`, `LockContentionEvent`, `LockErrorEvent`, ...).
- `ILockEventBus` / `InMemoryLockEventBus` - typed pub/sub with a bounded in-memory event
  history (default 10k entries, trimmed on publish).
- `ILockEventPublisher` with `InMemoryLockEventPublisher` and a `NoOpLockEventPublisher`
  (null-object so callers never need `if (publisher != null)`).
- `LockEventSubscriber` base class with `LoggingLockEventSubscriber` and
  `MetricsTrackingEventSubscriber` implementations.

### src/Workers - background services

All are standard `BackgroundService` implementations with their own `*Options` classes:

- `LockCleanupWorker` - periodically calls `DeleteExpiredLockAsync` for backends without
  native TTL (PostgreSQL, SQLite, InMemory).
- `LockRenewalWorker` - auto-renews registered locks on a schedule (`RenewalSchedule`).
- `MetricsCollectionWorker` - snapshots `LockMetrics` into `MetricsSnapshot`s.
- `HealthMonitoringWorker` - probes backend health, exposes `HealthStatus`.

They are intentionally *not* auto-registered by `AddDistributedLocking` - hosting them is
an application decision (a console consumer of the library may not want four hosted
services), so each has its own registration extension.

### src/Api - optional HTTP surface

`DistributedLockController`, `HealthCheckController`, `MetricsController` plus middleware
(`AuthenticationMiddleware`, `ExceptionHandlingMiddleware`, `RateLimitingMiddleware`,
`RequestLoggingMiddleware`). This exists for deployments that expose locking as a service
rather than a library; a library-only consumer never touches this namespace.

### src/Caching, src/Formatters, src/Utilities, src/Integration

- `ILockCacheManager` / `InMemoryLockCacheManager` with `CacheKeyGenerator` - read-side
  caching helpers to reduce backend round-trips for existence checks.
- `JsonLockSerializer`, `XmlLockSerializer`, `CsvLockExporter` - export/serialization of
  lock state for tooling and diagnostics.
- `WebhookPublisher`, `ApiClient`, `HttpClientFactory` - outbound integration (pushing
  lock events to external systems).
- Utilities: extension methods and `RetryPolicyHelper`/`ValidationHelper`.

## Dependency injection

`AddDistributedLocking(options => ...)`:

1. Builds and validates `DistributedLockOptions` (throws `InvalidOperationException`
   listing every validation error - fail-fast, no partially configured container).
2. Constructs a `DefaultLockRetryPolicy` from the retry knobs and registers it as
   `ILockRetryPolicy` (a second overload accepts a caller-supplied policy, e.g.
   Polly-based).
3. Registers the repository singleton chosen by `options.BackendType` via a `switch`
   (unknown values throw `NotSupportedException`).
4. Registers `ILockService` (scoped), `FencingTokenService`, `LockMonitor`,
   `ILockEventBus` and `IDeadlockDetector` (singletons).

Repositories are singletons because they own connections; `LockService` is scoped and
stateless apart from its metrics object.

## Data flow: blocking acquire

1. Caller invokes `ILockService.AcquireAsync(key, owner, duration, ct)`.
2. `LockService` starts a `LockAcquisition` record and loops up to the retry policy's
   `MaxRetries`, checking the overall acquisition timeout each pass.
3. Each attempt calls `TryAcquireAsync`, which builds a `Lock` model and delegates to
   `ILockRepository.AcquireAsync` - the backend performs its atomic insert-if-absent.
4. Success: status set to `LockStatus.Held`, acquisition latency recorded in
   `LockMetrics`, lock returned.
5. Failure: failed attempt recorded, delay from `ILockRetryPolicy.GetDelay(attempt)`
   (exponential backoff with jitter, capped at the policy's max delay), retry.
6. Exhaustion or timeout: `LockAcquisitionException` with attempt count.

Renewal via fencing token (`RenewLockAsync`) validates the token against the repository
first and throws `InvalidFencingTokenException` on mismatch before touching the lock.

## Extension points

- **New backend**: implement `ILockRepository`, register it before/instead of
  `AddDistributedLocking`'s choice. The interface is the only contract; nothing else in
  Core knows about concrete backends.
- **Custom retry behavior**: implement `ILockRetryPolicy` and pass it to the
  `AddDistributedLocking(ILockRetryPolicy, ...)` overload.
- **Event consumers**: subclass `LockEventSubscriber` or subscribe on `ILockEventBus`.
- **External notification**: `WebhookPublisher` for HTTP push of events.

## Known limitations

- Clock-based expiry: correctness of expiration depends on reasonably synchronized clocks
  between the app nodes and the backend; fencing tokens are the mitigation for the
  pathological case, not a replacement for sane clocks.
- No multi-node quorum (Redlock) - a single backend is a single point of truth; use the
  backend's own HA story (Redis Sentinel, Postgres replication).
- `LockMetrics` is per-process, not aggregated across nodes; cross-node visibility needs
  the metrics worker plus an external sink.
- The in-memory backend and event bus are process-local by definition - fine for tests,
  meaningless for distribution.
- Blocking acquire uses polling with backoff, not backend push notification, so worst-case
  acquisition latency after a release is one backoff interval.
