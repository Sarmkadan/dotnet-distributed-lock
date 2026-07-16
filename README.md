## Architecture

For the full picture - module layout, backend trade-offs, data flow of a blocking
acquire, DI wiring, extension points and known limitations - see
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md). Short version: `ILockService` orchestrates
lock semantics (retry with backoff, fencing tokens, metrics) on top of a single
`ILockRepository` seam with Redis, PostgreSQL, SQLite and in-memory implementations.

## IDeadlockDetector

The `IDeadlockDetector` interface provides mechanisms to track lock contention and detect potential circular wait chains (deadlocks) in distributed locking scenarios. By recording when owners start and stop waiting, or successfully acquire/release locks, it can identify potential circular dependencies and provide actionable metrics on contention.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Services;
using SarmKadan.DistributedLock.Models;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<DeadlockDetector>();

var detector = new DeadlockDetector(logger);

// Record a waiter for a lock
await detector.RecordWaitingAsync("owner-1", "lock-A");

// Check if this would cause a deadlock
bool wouldDeadlock = detector.WouldDeadlock("owner-1", "lock-A");
Console.WriteLine($"Would deadlock: {wouldDeadlock}");

// Record successful acquisition
detector.RecordAcquired("owner-1", "lock-A");

// Record waiting ended
await detector.RecordWaitEndedAsync("owner-1", "lock-A", 150.0);

// Record release
detector.RecordReleased("owner-1", "lock-A");

// Retrieve metrics
var metrics = detector.GetMetrics("lock-A");
if (metrics != null)
{
    Console.WriteLine($"Lock A contention: {metrics.WaitCount} waits");
}

// Get all metrics
var allMetrics = detector.GetAllMetrics();
Console.WriteLine($"Total tracked locks: {allMetrics.Count}");
```


## FencingTokenService

The `FencingTokenService` class implements the fencing token pattern to prevent zombie processes from writing to shared resources. It issues monotonically increasing tokens for locks, validates tokens to ensure they are current, and provides methods to revoke tokens when locks are released. This prevents split-brain scenarios in distributed systems where a failed process might regain access to a resource.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Services;
using SarmKadan.DistributedLock.Models;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<FencingTokenService>();

var fencingTokenService = new FencingTokenService(logger);

// Issue a new fencing token for a lock
var token1 = fencingTokenService.IssueToken("distributed-lock-1");
Console.WriteLine($"Issued token: {token1}");

// Issue another token for the same lock (sequence number increases)
var token2 = fencingTokenService.IssueToken("distributed-lock-1");
Console.WriteLine($"New token: {token2}");

// Validate a token
bool isValid = fencingTokenService.ValidateToken("distributed-lock-1", token2);
Console.WriteLine($"Token validation result: {isValid}");

// Get current token for a lock
var currentToken = fencingTokenService.GetToken("distributed-lock-1");
Console.WriteLine($"Current token: {currentToken}");

// Increment token sequence (creates new generation)
var newToken = fencingTokenService.IncrementToken("distributed-lock-1");
Console.WriteLine($"Incremented token: {newToken}");

// Check if resource is locked
bool isLocked = fencingTokenService.IsResourceLocked("distributed-lock-1");
Console.WriteLine($"Is resource locked: {isLocked}");

// Revoke token when lock is released
fencingTokenService.RevokeToken("distributed-lock-1");

// Check if resource is now unlocked
isLocked = fencingTokenService.IsResourceLocked("distributed-lock-1");
Console.WriteLine($"Is resource locked after revocation: {isLocked}");

// Clear all tokens (typically for testing)
fencingTokenService.ClearAllTokens();
```

## ILockRetryPolicy

The `ILockRetryPolicy` interface defines the contract for configuring retry behavior when acquiring a distributed lock. It exposes properties for the maximum number of retries, the initial and maximum delay between attempts, and a jitter factor to randomize delays, plus a method to compute the delay for a given attempt. The default implementation `DefaultLockRetryPolicy` provides exponential back‑off with optional jitter.

### Usage Example

```csharp
using System;
using SarmKadan.DistributedLock.Services;

// Create a retry policy using the default implementation
ILockRetryPolicy retryPolicy = new DefaultLockRetryPolicy(
    maxRetries: 5,
    initialDelay: TimeSpan.FromMilliseconds(200),
    maxDelay: TimeSpan.FromSeconds(2),
    jitterFactor: 0.2);

// Compute and display the delay for each retry attempt
for (int attempt = 0; attempt < retryPolicy.MaxRetries; attempt++)
{
    TimeSpan delay = retryPolicy.GetDelay(attempt);
    Console.WriteLine($"Attempt {attempt + 1}: wait {delay.TotalMilliseconds:F0} ms");
}
```

## LockMonitor

The `LockMonitor` class is responsible for monitoring locks and handling automatic renewal based on configuration. It allows registering locks for monitoring, starting and stopping the monitoring loop, and retrieving the list of monitored locks.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Services;
using SarmKadan.DistributedLock.Models;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<LockMonitor>();

var lockService = new LockService(); // Initialize with your lock service
var lockMonitor = new LockMonitor(lockService, logger);

// Register a lock for monitoring
lockMonitor.RegisterLock("my-lock", "owner-123", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));

// Start monitoring
lockMonitor.StartMonitoring(TimeSpan.FromSeconds(10));

// Get monitored locks
var monitoredLocks = lockMonitor.GetMonitoredLocks();
Console.WriteLine($"Monitored locks: {string.Join(", ", monitoredLocks)}");

// Stop monitoring
await lockMonitor.StopMonitoringAsync();

// Dispose
lockMonitor.Dispose();
```

## DistributedLockOptions

The `DistributedLockOptions` class provides configuration for the distributed lock system, allowing fine-grained control over lock acquisition behavior, retry policies, backend selection, and monitoring. It supports various backends (Redis, SQL Server, PostgreSQL, Azure Blob Storage) and enables features like automatic renewal, fencing tokens, metrics collection, and deadlock detection.

### Usage Example

```csharp
using SarmKadan.DistributedLock;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Configure distributed lock options for Redis backend
var options = new DistributedLockOptions
{
    BackendType = BackendType.Redis,
    ConnectionString = "localhost:6379,password=yourpassword",
    DefaultLockDuration = TimeSpan.FromMinutes(5),
    DefaultAcquisitionTimeout = TimeSpan.FromSeconds(30),
    DefaultRenewalInterval = TimeSpan.FromMinutes(2),
    DefaultMaxRetries = 3,
    DefaultRetryDelayMs = 200,
    DefaultAcquisitionMode = AcquisitionMode.WaitUntilAvailable,
    EnableAutoRenewal = true,
    UseFencingTokens = true,
    MonitoringInterval = TimeSpan.FromSeconds(30),
    MaxConcurrentLocks = 1000,
    EnableMetrics = true,
    EnableLogging = true,
    RetryPolicyMaxRetries = 5,
    RetryPolicyInitialDelayMs = 100,
    RetryPolicyMaxDelayMs = 2000,
    RetryPolicyJitterFactor = 0.25,
};

// Register with DI container
var services = new ServiceCollection();
services.AddSingleton(options);
services.AddLogging(builder => builder.AddConsole());

// Example: Using options with LockService
var lockService = new LockService(
    new RedisLockRepository(options.ConnectionString),
    options,
    logger
);

// Validate configuration
var validationErrors = options.Validate();
if (validationErrors.Any())
{
    foreach (var error in validationErrors)
    {
        Console.WriteLine($"Validation error: {error}");
    }
}
```

## HealthMonitoringWorker

The `HealthMonitoringWorker` class is a background service that monitors the health of the distributed lock system. It periodically verifies backend connectivity, records health status including consecutive failures, and alerts when issues are detected with the lock service or backends. The worker runs on a configurable schedule and provides methods to retrieve the current health status and reset failure counters.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Workers;
using SarmKadan.DistributedLock.Repository;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<HealthMonitoringWorker>();

// Initialize lock repository
var lockRepository = new SqliteLockRepository(
    "/var/data/distributed-locks.db",
    logger
);

// Create health monitoring worker with default options
var healthWorker = new HealthMonitoringWorker(
    lockRepository,
    logger
);

// Start the background health monitoring
var cts = new CancellationTokenSource();
_ = healthWorker.RunAsync(cts.Token);

// Get current health status
var status = healthWorker.GetStatus();
Console.WriteLine($"Healthy: {status.IsHealthy}");
Console.WriteLine($"Backend connected: {status.BackendConnected}");
Console.WriteLine($"Last check: {status.LastCheckTime}");
Console.WriteLine($"Check duration: {status.CheckDurationMs}ms");
Console.WriteLine($"Consecutive failures: {status.ConsecutiveFailures}");
Console.WriteLine($"Last error: {status.LastErrorMessage ?? "None"}");

// Configure custom health monitoring options
var customOptions = new HealthMonitoringWorkerOptions
{
    CheckIntervalMs = 60000, // 1 minute between checks
    FailureThreshold = 5,     // Alert after 5 consecutive failures
    AlertOnUnhealthy = true,  // Send alerts when unhealthy
    CheckTimeout = TimeSpan.FromSeconds(15) // 15 second timeout
};

var customHealthWorker = new HealthMonitoringWorker(
    lockRepository,
    logger,
    customOptions
);

// Reset failure counter when health recovers
healthWorker.ResetFailureCounter();

// Stop the worker when done
await healthWorker.StopAsync(CancellationToken.None);
```

## MetricsCollectionWorker

The `MetricsCollectionWorker` class is a background service that periodically collects and stores metrics about lock operations, cache performance, and custom application metrics. It tracks acquisition/renewal/release operations, maintains historical snapshots of metrics, and provides methods to retrieve current and historical metrics for monitoring and observability purposes.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Workers;
using SarmKadan.DistributedLock.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<MetricsCollectionWorker>();

// Create metrics collection worker with default options
var metricsWorker = new MetricsCollectionWorker(logger);

// Start the background metrics collection
var cts = new CancellationTokenSource();
_ = metricsWorker.RunAsync(cts.Token);

// Get current snapshot of metrics
var currentSnapshot = metricsWorker.GetCurrentSnapshot();
if (currentSnapshot != null)
{
    Console.WriteLine($"Current lock operations: {currentSnapshot.TotalLockOperations}");
    Console.WriteLine($"Cache hits: {currentSnapshot.CacheStatistics?.Hits}");
    Console.WriteLine($"Cache misses: {currentSnapshot.CacheStatistics?.Misses}");
}

// Get all historical snapshots
var allSnapshots = metricsWorker.GetSnapshots();
Console.WriteLine($"Total snapshots collected: {allSnapshots.Count}");

// Get average metrics over time
var averageMetrics = metricsWorker.GetAverageMetrics();
if (averageMetrics != null)
{
    Console.WriteLine($"Average lock operations per minute: {averageMetrics.LockOperationsPerMinute}");
}

// Configure custom metrics collection options
var customOptions = new MetricsCollectionWorkerOptions
{
    InitialDelayMs = 30000, // 30 seconds initial delay
    CollectionIntervalMs = 60000, // 1 minute between collections
    SnapshotRetentionSeconds = 3600, // Keep snapshots for 1 hour
    VerboseLogging = true,
    CustomMetrics = new Dictionary<string, object>
    {
        ["custom.metric1"] = "value1",
        ["custom.metric2"] = 42
    }
};

var customMetricsWorker = new MetricsCollectionWorker(
    logger,
    customOptions
);

// Stop the worker when done
await metricsWorker.StopAsync(CancellationToken.None);
```

## LockRenewalWorker

The `LockRenewalWorker` class is a background service that automatically renews distributed locks before they expire. It monitors locks registered for renewal and extends their duration by the specified renewal interval, preventing accidental lock expiration during long-running operations. The worker runs on a configurable schedule and handles renewal failures gracefully with retry logic.

### Public Members

```csharp
public LockRenewalWorker(ILockService lockService, ILogger<LockRenewalWorker> logger, LockRenewalWorkerOptions? options = null)
public void RegisterForRenewal(string lockId, ulong fencingToken, TimeSpan renewalInterval)
public bool IsRegisteredForRenewal(string lockId)
public bool TryGetRenewalSchedule(string lockId, [NotNullWhen(true)] out RenewalSchedule? schedule)
public TimeSpan? GetTimeUntilNextRenewal(string lockId)
public void UnregisterFromRenewal(string lockId)
public override async Task StopAsync(CancellationToken cancellationToken)

// Properties from RenewalSchedule (inner class)
public required string LockId { get; init; }
public required ulong FencingToken { get; init; }
public required TimeSpan RenewalInterval { get; init; }
public DateTime NextRenewalTime { get; set; }

// Properties from LockRenewalWorkerOptions (configuration)
public int CheckIntervalMs { get; set; } = 5000;
public int RetryDelaySeconds { get; set; } = 10;
public double JitterPercentage { get; set; } = 10;
```

### Usage Example

```csharp
using SarmKadan.DistributedLock.Workers;
using SarmKadan.DistributedLock.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<LockRenewalWorker>();

// Initialize lock service with your repository
var lockService = new LockService(lockRepository, logger);

// Create renewal worker with default options
var renewalWorker = new LockRenewalWorker(lockService, logger);

// Start the background renewal worker
var cts = new CancellationTokenSource();
_ = renewalWorker.RunAsync(cts.Token);

// Register a lock for automatic renewal
// This would typically be done after successfully acquiring the lock
renewalWorker.RegisterForRenewal(
    lockId: "critical-section-lock",
    fencingToken: 12345,
    renewalInterval: TimeSpan.FromMinutes(2)
);

// Check if a lock is registered for renewal
bool isRegistered = renewalWorker.IsRegisteredForRenewal("critical-section-lock");
Console.WriteLine($"Lock registered for renewal: {isRegistered}");

// Get time until next renewal
var timeUntilRenewal = renewalWorker.GetTimeUntilNextRenewal("critical-section-lock");
if (timeUntilRenewal.HasValue)
{
    Console.WriteLine($"Next renewal in: {timeUntilRenewal.Value.TotalSeconds:F0} seconds");
}

// Configure custom renewal options
var customOptions = new LockRenewalWorkerOptions
{
    CheckIntervalMs = 3000,      // Check every 3 seconds
    RetryDelaySeconds = 5,        // Retry failed renewals after 5 seconds
    JitterPercentage = 15         // Add 15% random jitter to renewal timing
};

var customRenewalWorker = new LockRenewalWorker(lockService, logger, customOptions);

// Unregister a lock when done (e.g., after releasing it)
renewalWorker.UnregisterFromRenewal("critical-section-lock");

// Stop the worker when shutting down
await renewalWorker.StopAsync(CancellationToken.None);
```

## LockCleanupWorker

The `LockCleanupWorker` class is a background service that periodically cleans up expired locks from the backend storage (Redis, PostgreSQL, SQLite, etc.). It prevents database/Redis bloat by removing locks that are no longer needed, runs on a configurable schedule, and logs cleanup statistics. The worker can also be triggered manually for testing or benchmarking purposes.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Workers;
using SarmKadan.DistributedLock.Backends.SQLite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<LockCleanupWorker>();

// Initialize lock repository
var lockRepository = new SqliteLockRepository(
    "/var/data/distributed-locks.db",
    logger
);

// Create cleanup worker with default options
var cleanupWorker = new LockCleanupWorker(
    lockRepository,
    logger
);

// Start the background cleanup worker
var cts = new CancellationTokenSource();
_ = cleanupWorker.RunAsync(cts.Token);

// Optionally: trigger manual cleanup
await cleanupWorker.RunCleanupOnceAsync();

// Configure custom cleanup options
var customOptions = new LockCleanupWorkerOptions
{
    InitialDelayMs = 60000, // 1 minute initial delay
    CleanupIntervalMs = 7200000, // 2 hours between cleanups
    BatchSize = 500,
    VerboseLogging = true,
    MinimumExpiredDuration = TimeSpan.FromMinutes(10)
};

var customCleanupWorker = new LockCleanupWorker(
    lockRepository,
    logger,
    customOptions
);

// Stop the worker when done
await cleanupWorker.StopAsync(CancellationToken.None);
```

## SqliteLockRepository

The `SqliteLockRepository` class is a SQLite-based implementation of the lock repository for distributed locking scenarios. It provides atomic operations for acquiring, renewing, releasing, and querying locks using SQLite as the distributed data store. This implementation supports automatic cleanup of expired locks, fencing token validation, and comprehensive monitoring capabilities.

The repository uses SQLite for storage with proper indexing for performance. It implements `IAsyncDisposable` for resource cleanup and provides methods for managing lock lifecycle including acquisition, renewal, validation, and cleanup of expired locks.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Backends.SQLite;
using SarmKadan.DistributedLock.Models;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<SqliteLockRepository>();

// Initialize SQLite lock repository
var sqliteLockRepository = new SqliteLockRepository(
    "/var/data/distributed-locks.db",
    logger
);

// Create a lock instance
var newLock = new Lock(
    key: "distributed-lock-1",
    ownerId: "worker-service-01",
    duration: TimeSpan.FromMinutes(5),
    fencingToken: 12345
);

// Acquire the lock atomically
bool acquired = await sqliteLockRepository.AcquireAsync(newLock);
Console.WriteLine($"Lock acquired: {acquired}");

// Check if lock exists
bool exists = await sqliteLockRepository.ExistsAsync("distributed-lock-1");
Console.WriteLine($"Lock exists: {exists}");

// Get lock by key
var existingLock = await sqliteLockRepository.GetByKeyAsync("distributed-lock-1");
if (existingLock != null)
{
    Console.WriteLine($"Lock found: {existingLock.Key} owned by {existingLock.OwnerId}");
}

// Get lock by key and owner (for validation)
var lockByOwner = await sqliteLockRepository.GetByKeyAndOwnerAsync(
    "distributed-lock-1",
    "worker-service-01"
);
if (lockByOwner != null)
{
    Console.WriteLine($"Lock found for owner: {lockByOwner.Key}");
}

// Update lock metadata
bool updated = await sqliteLockRepository.UpdateAsync(newLock);
Console.WriteLine($"Lock updated: {updated}");

// Renew the lock
bool renewed = await sqliteLockRepository.RenewAsync(
    "distributed-lock-1",
    "worker-service-01",
    TimeSpan.FromMinutes(5)
);
Console.WriteLine($"Lock renewed: {renewed}");

// Get all active locks
var allActiveLocks = await sqliteLockRepository.GetAllActiveLockAsync();
Console.WriteLine($"Total active locks: {allActiveLocks.Count()}");

// Get locks by owner
var ownerLocks = await sqliteLockRepository.GetByOwnerAsync("worker-service-01");
Console.WriteLine($"Locks owned by worker-service-01: {ownerLocks.Count()}");

// Validate fencing token
bool tokenValid = await sqliteLockRepository.ValidateFencingTokenAsync(
    "distributed-lock-1",
    12345
);
Console.WriteLine($"Fencing token valid: {tokenValid}");

// Release the lock when done
bool released = await sqliteLockRepository.ReleaseAsync(
    "distributed-lock-1",
    "worker-service-01"
);
Console.WriteLine($"Lock released: {released}");

// Clean up expired locks
int expiredDeleted = await sqliteLockRepository.DeleteExpiredLockAsync();
Console.WriteLine($"Deleted {expiredDeleted} expired locks");

// Clear all locks (use with caution in production)
int allCleared = await sqliteLockRepository.ClearAllAsync();
Console.WriteLine($"Cleared {allCleared} locks");

// Dispose the repository when done (implements IAsyncDisposable)
await sqliteLockRepository.DisposeAsync();
```

## PostgresLockRepository

The `PostgresLockRepository` class is a PostgreSQL-based implementation of the lock repository for distributed locking scenarios. It provides atomic operations for acquiring, renewing, releasing, and querying locks using PostgreSQL as the distributed data store. This implementation supports automatic cleanup of expired locks, fencing token validation, and comprehensive monitoring capabilities.

The repository uses PostgreSQL advisory locks for session-level locking to ensure atomic operations, and stores lock metadata in a dedicated `distributed_locks` table with proper indexing for performance. It implements `IAsyncDisposable` for resource cleanup and provides methods for managing lock lifecycle including acquisition, renewal, validation, and cleanup of expired locks.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Backends.PostgreSQL;
using SarmKadan.DistributedLock.Models;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<PostgresLockRepository>();

// Initialize PostgreSQL lock repository
var postgresLockRepository = new PostgresLockRepository(
    "Host=localhost;Port=5432;Database=distributed_locks;Username=postgres;Password=yourpassword",
    logger
);

// Create a lock instance
var newLock = new Lock(
    key: "distributed-lock-1",
    ownerId: "worker-service-01",
    duration: TimeSpan.FromMinutes(5),
    fencingToken: 12345
);

// Acquire the lock atomically
bool acquired = await postgresLockRepository.AcquireAsync(newLock);
Console.WriteLine($"Lock acquired: {acquired}");

// Check if lock exists
bool exists = await postgresLockRepository.ExistsAsync("distributed-lock-1");
Console.WriteLine($"Lock exists: {exists}");

// Get lock by key
var existingLock = await postgresLockRepository.GetByKeyAsync("distributed-lock-1");
if (existingLock != null)
{
    Console.WriteLine($"Lock found: {existingLock.Key} owned by {existingLock.OwnerId}");
}

// Renew the lock
bool renewed = await postgresLockRepository.RenewAsync(
    "distributed-lock-1",
    "worker-service-01",
    TimeSpan.FromMinutes(5)
);
Console.WriteLine($"Lock renewed: {renewed}");

// Get all active locks
var allActiveLocks = await postgresLockRepository.GetAllActiveLockAsync();
Console.WriteLine($"Total active locks: {allActiveLocks.Count()}");

// Get locks by owner
var ownerLocks = await postgresLockRepository.GetByOwnerAsync("worker-service-01");
Console.WriteLine($"Locks owned by worker-service-01: {ownerLocks.Count()}");

// Validate fencing token
bool tokenValid = await postgresLockRepository.ValidateFencingTokenAsync(
    "distributed-lock-1",
    12345
);
Console.WriteLine($"Fencing token valid: {tokenValid}");

// Release the lock when done
bool released = await postgresLockRepository.ReleaseAsync(
    "distributed-lock-1",
    "worker-service-01"
);
Console.WriteLine($"Lock released: {released}");

// Clean up expired locks
int expiredDeleted = await postgresLockRepository.DeleteExpiredLockAsync();
Console.WriteLine($"Deleted {expiredDeleted} expired locks");

// Clear all locks (use with caution in production)
int allCleared = await postgresLockRepository.ClearAllAsync();
Console.WriteLine($"Cleared {allCleared} locks");

// Dispose the repository when done (implements IAsyncDisposable)
await postgresLockRepository.DisposeAsync();
```

### Usage Example

```csharp
using SarmKadan.DistributedLock.Backends.Redis;
using SarmKadan.DistributedLock.Models;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<RedisLockRepository>();

// Initialize Redis lock repository
var redisLockRepository = new RedisLockRepository(
    "localhost:6379,password=yourpassword",
    logger
);

// Create a lock instance
var newLock = new Lock(
    key: "distributed-lock-1",
    ownerId: "worker-service-01",
    duration: TimeSpan.FromMinutes(5),
    fencingToken: 12345
);

// Acquire the lock atomically
bool acquired = await redisLockRepository.AcquireAsync(newLock);
Console.WriteLine($"Lock acquired: {acquired}");

// Check if lock exists
bool exists = await redisLockRepository.ExistsAsync("distributed-lock-1");
Console.WriteLine($"Lock exists: {exists}");

// Get lock by key
var existingLock = await redisLockRepository.GetByKeyAsync("distributed-lock-1");
if (existingLock != null)
{
    Console.WriteLine($"Lock found: {existingLock.Key} owned by {existingLock.OwnerId}");
}

// Renew the lock
bool renewed = await redisLockRepository.RenewAsync(
    "distributed-lock-1",
    "worker-service-01",
    TimeSpan.FromMinutes(5)
);
Console.WriteLine($"Lock renewed: {renewed}");

// Get all active locks
var allActiveLocks = await redisLockRepository.GetAllActiveLockAsync();
Console.WriteLine($"Total active locks: {allActiveLocks.Count()}");

// Get locks by owner
var ownerLocks = await redisLockRepository.GetByOwnerAsync("worker-service-01");
Console.WriteLine($"Locks owned by worker-service-01: {ownerLocks.Count()}");

// Validate fencing token
bool tokenValid = await redisLockRepository.ValidateFencingTokenAsync(
    "distributed-lock-1",
    12345
);
Console.WriteLine($"Fencing token valid: {tokenValid}");

// Release the lock when done
bool released = await redisLockRepository.ReleaseAsync(
    "distributed-lock-1",
    "worker-service-01"
);
Console.WriteLine($"Lock released: {released}");

// Clean up expired locks
int expiredDeleted = await redisLockRepository.DeleteExpiredLockAsync();
Console.WriteLine($"Deleted {expiredDeleted} expired locks");

// Clear all locks (use with caution in production)
int allCleared = await redisLockRepository.ClearAllAsync();
Console.WriteLine($"Cleared {allCleared} locks");

// Dispose the repository when done (implements IAsyncDisposable)
await redisLockRepository.DisposeAsync();
```

## ExceptionHandlingMiddleware

The `ExceptionHandlingMiddleware` class is a global exception handling middleware that catches all unhandled exceptions during HTTP request processing and converts them to appropriate HTTP responses with meaningful error messages. It prevents sensitive stack traces from being exposed to clients while providing structured error responses that include the error message, error code, and timestamp. The middleware maps domain-specific exceptions to their corresponding HTTP status codes for improved client clarity.

### Public Members

```csharp
public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
public async Task InvokeAsync(HttpContext context)

// Properties from ErrorResponseBody (returned to clients)
public string Message { get; set; }
public string ErrorCode { get; set; }
public DateTime Timestamp { get; set; }
```

### Usage Example

```csharp
using SarmKadan.DistributedLock.Api.Middleware;
using SarmKadan.DistributedLock.Core.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<ExceptionHandlingMiddleware>();

// Create middleware instance
var middleware = new ExceptionHandlingMiddleware(
    next: async (innerContext) => 
    {
        // Your request handling logic here
        if (shouldFail)
        {
            throw new LockAcquisitionException("Could not acquire lock after 5 attempts");
        }
    },
    logger: logger
);

// Create a test HTTP context
var context = new DefaultHttpContext();
context.Response.Body = new MemoryStream();

// Invoke the middleware
try
{
    await middleware.InvokeAsync(context);
}
catch (Exception ex)
{
    // Exception is caught and handled by the middleware itself
}

// Read the response
context.Response.Body.Seek(0, SeekOrigin.Begin);
var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
Console.WriteLine($"Response status: {context.Response.StatusCode}");
Console.WriteLine($"Response body: {responseBody}");

// Example with ASP.NET Core pipeline
var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole());
services.AddSingleton<ILogger<ExceptionHandlingMiddleware>>(logger);

var app = new WebApplication(services.BuildServiceProvider());
app.UseMiddleware<ExceptionHandlingMiddleware>();

// The middleware will automatically handle exceptions thrown by subsequent middleware
```

## LockService

The `LockService` class is the core service for managing distributed locks in the system. It provides methods to acquire, renew, release, and query locks with comprehensive retry logic, metrics tracking, and logging. The service supports both blocking and non-blocking acquisition patterns, automatic renewal for long-running operations, and fencing token validation to prevent split-brain scenarios.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Services;
using SarmKadan.DistributedLock.Models;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<LockService>();

// Initialize the lock service with a repository and logger
var lockService = new LockService(lockRepository, logger);

// Try to acquire a lock (non-blocking)
var (success, acquiredLock, errorMessage) = await lockService.TryAcquireAsync(
    "distributed-lock-1",
    "worker-service-01",
    TimeSpan.FromMinutes(5)
);

if (success && acquiredLock != null)
{
    Console.WriteLine($"Lock acquired: {acquiredLock.Key} by {acquiredLock.OwnerId}");
    
    // Renew the lock before it expires
    bool renewed = await lockService.RenewAsync(
        "distributed-lock-1",
        "worker-service-01",
        TimeSpan.FromMinutes(5)
    );
    
    if (renewed)
    {
        Console.WriteLine("Lock renewed successfully");
    }
    
    // Get lock information
    var existingLock = await lockService.GetLockAsync("distributed-lock-1");
    if (existingLock != null)
    {
        Console.WriteLine($"Lock is held by: {existingLock.OwnerId}");
    }
    
    // Check if lock is currently held
    bool isLocked = await lockService.IsLockedAsync("distributed-lock-1");
    Console.WriteLine($"Is locked: {isLocked}");
    
    // Release the lock when done
    bool released = await lockService.ReleaseAsync(
        "distributed-lock-1",
        "worker-service-01"
    );
    
    if (released)
    {
        Console.WriteLine("Lock released successfully");
    }
}

// Acquire a lock with blocking retry (waits until lock is available)
var lockWithRetry = await lockService.AcquireAsync(
    "critical-section-lock",
    "background-job-42",
    TimeSpan.FromMinutes(2)
);

Console.WriteLine($"Lock acquired with retry: {lockWithRetry.Key}");

// Get all active locks in the system
var allActiveLocks = await lockService.GetAllActiveLockAsync();
Console.WriteLine($"Total active locks: {allActiveLocks.Count()}");

// Get metrics for monitoring and observability
var metrics = lockService.GetMetrics();
Console.WriteLine($"Total acquisitions: {metrics.TotalAcquisitions}");
Console.WriteLine($"Total renewals: {metrics.TotalRenewals}");
Console.WriteLine($"Total releases: {metrics.TotalReleases}");
```
