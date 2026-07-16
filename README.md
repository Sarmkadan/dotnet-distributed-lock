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

## DistributedLockController
The `DistributedLockController` class provides HTTP endpoints for managing distributed locks. It allows clients to acquire, release, renew, and check the status of locks through RESTful API endpoints.

Example usage:

```csharp
using Microsoft.AspNetCore.Mvc;
using SarmKadan.DistributedLock.Api.Controllers;
using SarmKadan.DistributedLock.Models;

// Initialize with your lock service and logger
var lockService = new LockService(lockRepository, logger);
var distributedLockController = new DistributedLockController(lockService, logger);

// Acquire a lock
var acquireResult = await distributedLockController.AcquireLock(new LockAcquisitionRequest
{
    LockName = "critical-section-lock",
    DurationSeconds = 300, // 5 minutes
    AutoRenew = true,
    RenewalIntervalSeconds = 60 // Renew every minute
});

if (acquireResult.Value?.Success == true)
{
    Console.WriteLine($"Lock acquired: {acquireResult.Value.LockId}");
    Console.WriteLine($"Fencing token: {acquireResult.Value.FencingToken}");
    Console.WriteLine($"Expires at: {acquireResult.Value.ExpiresAt}");

    // Do work with the lock...

    // Release the lock when done
    var releaseResult = await distributedLockController.ReleaseLock(
        acquireResult.Value.LockId,
        acquireResult.Value.FencingToken
    );
    
    if (releaseResult.Value?.Success == true)
    {
        Console.WriteLine("Lock released successfully");
    }
}

// Get lock status
var statusResult = await distributedLockController.GetLockStatus("critical-section-lock");
if (statusResult.Value != null)
{
    Console.WriteLine($"Lock name: {statusResult.Value.Name}");
    Console.WriteLine($"Is active: {statusResult.Value.IsActive}");
    Console.WriteLine($"Remaining seconds: {statusResult.Value.RemainingSeconds}");
}

// Renew a lock
var renewResult = await distributedLockController.RenewLock(
    acquireResult.Value.LockId,
    acquireResult.Value.FencingToken,
    300 // Extend by 5 minutes
);

if (renewResult.Value?.Success == true)
{
    Console.WriteLine("Lock renewed successfully");
}
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

## DeadlockDetectorTests

The `DeadlockDetectorTests` class provides comprehensive unit tests for the `DeadlockDetector` class, which implements deadlock detection logic for distributed locking scenarios. It verifies that the deadlock detector correctly:
- Tracks lock ownership and waiter chains
- Detects circular wait dependencies (deadlocks)
- Maintains metrics on contention and deadlock events
- Handles concurrent operations safely
- Validates input parameters and throws appropriate exceptions

### What it does

This test suite validates that the deadlock detector correctly:
- Returns `false` when there are no existing ownerships (no deadlock possible)
- Returns `true` when detecting simple circular wait scenarios
- Returns `false` when there is no circular wait (contention without deadlock)
- Detects deadlocks in longer chains of waiting owners
- Validates null parameters and throws `ArgumentNullException`
- Tracks waiters and records wait times
- Maintains ownership state correctly
- Provides metrics for monitoring and observability
- Handles concurrent waiting and acquisition operations

### Usage Example

```csharp
using SarmKadan.DistributedLock.Services;
using Xunit;

// Create a deadlock detector with a logger
var detector = new DeadlockDetector(logger);

// Test basic deadlock detection
bool wouldDeadlock = detector.WouldDeadlock("owner-A", "lock:1");
Assert.False(wouldDeadlock); // No deadlock when no locks are held

// Simulate a deadlock scenario:
// owner-A holds lock:1
detector.RecordAcquired("owner-A", "lock:1");

// owner-B holds lock:2 and wants lock:1
detector.RecordAcquired("owner-B", "lock:2");

// owner-B starts waiting for lock:1
detector.RecordWaitingAsync("owner-B", "lock:1");

// owner-A tries to wait for lock:2 → circular dependency → deadlock!
bool isDeadlock = detector.WouldDeadlock("owner-A", "lock:2");
Assert.True(isDeadlock);

// Record wait completion with timing
await detector.RecordWaitEndedAsync("owner-B", "lock:1", 150.0);

// Check metrics
await detector.RecordWaitingAsync("owner-C", "lock:1");
var metrics = detector.GetMetrics("lock:1");
Assert.NotNull(metrics);
Assert.Equal(1, metrics.CurrentWaiters);
Assert.Equal(150.0, metrics.AverageWaitTimeMs);
```

## LockServiceAdditionalTests

The `LockServiceAdditionalTests` class provides additional unit tests for the `LockService` class, focusing on edge cases and error handling scenarios. It verifies that the lock service gracefully handles repository errors and missing locks by returning appropriate default values (false, null, or empty enumerable) instead of throwing exceptions.

### What it does

This test suite validates that the lock service correctly:
- Returns `false` when attempting to release a non-existent lock
- Returns `false` when the repository throws exceptions during release operations
- Returns `false` when attempting to renew a non-existent lock
- Returns `null` when attempting to get a non-existent lock
- Returns `false` when the repository throws exceptions during lock status checks
- Returns an empty enumerable when the repository throws exceptions during lock listing operations

### Usage Example

```csharp
using SarmKadan.DistributedLock.Services;
using Microsoft.Extensions.Logging;
using Moq;

// Create a mock repository that throws exceptions
var repositoryMock = new Mock<ILockRepository>();
repositoryMock
    .Setup(r => r.GetByKeyAsync("non-existent-lock", It.IsAny<CancellationToken>()))
    .ReturnsAsync((Lock?)null);

// Create the lock service with the mocked repository
var lockService = new LockService(repositoryMock.Object, NullLogger<LockService>.Instance);

// Test release of non-existent lock - should return false
var released = await lockService.ReleaseAsync("non-existent-lock", "owner-1");
Console.WriteLine($"Release non-existent lock: {released}"); // Output: false

// Test getting non-existent lock - should return null
var @lock = await lockService.GetLockAsync("non-existent-lock");
Console.WriteLine($"Get non-existent lock: {@lock}"); // Output: null

// Test renew of non-existent lock - should return false
var renewed = await lockService.RenewAsync("non-existent-lock", "owner-1");
Console.WriteLine($"Renew non-existent lock: {renewed}"); // Output: false

// Test is locked with repository error - should return false
repositoryMock
    .Setup(r => r.ExistsAsync("resource:1", It.IsAny<CancellationToken>()))
    .ThrowsAsync(new Exception("Database error"));
var isLocked = await lockService.IsLockedAsync("resource:1");
Console.WriteLine($"Is locked with error: {isLocked}"); // Output: false

// Test getting all active locks with repository error - should return empty
repositoryMock
    .Setup(r => r.GetAllActiveLockAsync(It.IsAny<CancellationToken>()))
    .ThrowsAsync(new Exception("Database error"));
var locks = await lockService.GetAllActiveLockAsync();
Console.WriteLine($"All active locks with error: {locks.Count()} locks"); // Output: 0 locks
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

## InMemoryLockCacheManagerTests

The `InMemoryLockCacheManagerTests` class contains comprehensive unit tests for the `InMemoryLockCacheManager` implementation. It verifies cache operations including Get, Set, Remove, GetAll, Clear, and statistics tracking for the in-memory lock cache that reduces backend storage access and improves performance in distributed locking scenarios.

### What it does

This test suite validates that the in-memory lock cache correctly:
- Manages lock storage and retrieval operations
- Handles null and empty keys appropriately
- Maintains cache consistency under concurrent operations
- Tracks cache statistics (hits, misses, hit rate)
- Respects cache configuration settings
- Handles edge cases like overwriting existing locks and removing non-existent locks

### Usage Example

```csharp
using SarmKadan.DistributedLock.Caching;
using SarmKadan.DistributedLock.Models;
using Microsoft.Extensions.Logging;

// Create an in-memory lock cache manager
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<InMemoryLockCacheManager>();
var cacheManager = new InMemoryLockCacheManager(logger: logger);

// Store a lock in cache
var newLock = new Lock(
    key: "critical-section-123",
    ownerId: "background-worker-42",
    duration: TimeSpan.FromMinutes(5),
    fencingToken: 12345
);

await cacheManager.SetAsync(newLock);

// Retrieve a lock from cache
var cachedLock = await cacheManager.GetAsync("critical-section-123");
if (cachedLock != null)
{
    Console.WriteLine($"Lock found in cache: {cachedLock.Key} owned by {cachedLock.OwnerId}");
    Console.WriteLine($"Cached at: {cachedLock.CachedAt}");
    Console.WriteLine($"Last accessed: {cachedLock.LastAccessTime}");
}

// Get all cached locks
var allLocks = await cacheManager.GetAllAsync();
Console.WriteLine($"Total cached locks: {allLocks.Count}");

// Check cache statistics
var stats = cacheManager.GetStatistics();
Console.WriteLine($"Cache hits: {stats.Hits}");
Console.WriteLine($"Cache misses: {stats.Misses}");
Console.WriteLine($"Hit rate: {stats.HitRate:F2}%");

// Remove a lock from cache
await cacheManager.RemoveAsync("critical-section-123");

// Clear all cached locks
await cacheManager.ClearAsync();
```

## InMemoryLockEventBusTests

The `InMemoryLockEventBusTests` class contains comprehensive unit tests for the `InMemoryLockEventBus` implementation. It verifies subscription management, event publishing, subscriber counting, correlation ID propagation, exception handling, and concurrency scenarios for the in-memory event bus that handles lock-related events in the distributed lock system.

### What it does

This test suite validates that the in-memory event bus correctly:
- Manages subscriptions for different event types
- Publishes events to all registered subscribers
- Tracks subscriber counts accurately
- Propagates correlation IDs for tracing
- Handles exceptions gracefully without stopping other subscribers
- Maintains consistency under concurrent publishing and subscription scenarios

### Usage Example

```csharp
using SarmKadan.DistributedLock.Events;
using SarmKadan.DistributedLock.Tests;
using Xunit;

// Create the event bus with a logger
var bus = new InMemoryLockEventBus(NullLogger<InMemoryLockEventBus>.Instance);

// Subscribe to lock acquired events (synchronous handler)
bus.Subscribe<LockAcquiredEvent>(acquiredEvent => 
{
    Console.WriteLine($"Lock acquired: {acquiredEvent.LockName}");
    Console.WriteLine($"Owner: {acquiredEvent.OwnerId}");
    Console.WriteLine($"Status: {acquiredEvent.Status}");
});

// Subscribe to lock released events (asynchronous handler)
bus.Subscribe<LockReleasedEvent>(async releasedEvent => 
{
    await Task.Delay(10);
    Console.WriteLine($"Lock released: {releasedEvent.LockName}");
    Console.WriteLine($"Released by: {releasedEvent.OwnerId}");
});

// Publish an event with correlation ID for tracing
var correlationId = Guid.NewGuid().ToString();
var lockAcquiredEvent = new LockAcquiredEvent("user-session-123", "auth-service-42", LockStatus.Held);

await bus.PublishAsync(lockAcquiredEvent, correlationId);

// Check subscriber count
int subscriberCount = bus.GetSubscriberCount<LockAcquiredEvent>();
Console.WriteLine($"Subscribers for LockAcquiredEvent: {subscriberCount}");

// Verify all subscribers were invoked
```

## DefaultLockRetryPolicyTests

The `DefaultLockRetryPolicyTests` class contains comprehensive unit tests for the `DefaultLockRetryPolicy` class, verifying constructor behavior, parameter validation, and delay calculation logic. It tests default values, custom configuration, boundary conditions, and the exponential backoff algorithm with jitter to ensure the retry policy behaves correctly under various scenarios.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Services;
using Xunit;

// Create a retry policy with default values
var defaultPolicy = new DefaultLockRetryPolicy();
Assert.Equal(Constants.LockConstants.DefaultMaxRetries, defaultPolicy.MaxRetries);

// Create a retry policy with custom values
var customPolicy = new DefaultLockRetryPolicy(
    maxRetries: 5,
    initialDelay: TimeSpan.FromMilliseconds(200),
    maxDelay: TimeSpan.FromSeconds(2),
    jitterFactor: 0.2
);
Assert.Equal(5, customPolicy.MaxRetries);
Assert.Equal(TimeSpan.FromMilliseconds(200), customPolicy.InitialDelay);

// Test delay calculation for exponential backoff with jitter
var delay = customPolicy.GetDelay(3); // 3rd retry attempt
Console.WriteLine($"Delay for attempt 3: {delay.TotalMilliseconds}ms");

// Verify parameter validation throws appropriate exceptions
Assert.Throws<ArgumentOutOfRangeException>(() => new DefaultLockRetryPolicy(maxRetries: -1));
Assert.Throws<ArgumentOutOfRangeException>(() => new DefaultLockRetryPolicy(jitterFactor: 1.5));
```

## RetryPolicyHelper

The `RetryPolicyHelper` class provides utility methods for implementing retry logic with exponential backoff and jitter. It's essential for handling transient failures in distributed lock operations where temporary unavailability or contention is expected. The helper supports both asynchronous and synchronous operations, with configurable retry counts, initial delays, and backoff multipliers.

### Public Members

```csharp
public static async Task<T> ExecuteWithRetryAsync<T>
public static T ExecuteWithRetry<T>
public static async Task<T> ExecuteWithLinearRetryAsync<T>
public static RetryPolicy CreatePolicy
public static RetryPolicy Aggressive { get; }
public static RetryPolicy Moderate { get; }
public static RetryPolicy Conservative { get; }
public int MaxRetries { get; set; }
public int InitialDelayMs { get; set; }
public double BackoffMultiplier { get; set; }
```

### Usage Example

```csharp
using SarmKadan.DistributedLock.Utilities.Helpers;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger("RetryPolicyHelper");

// Example 1: Retry an async operation with exponential backoff
var result = await RetryPolicyHelper.ExecuteWithRetryAsync(async () =>
{
    // Simulate a transient failure (e.g., network issue, lock contention)
    if (DateTime.UtcNow.Second % 3 == 0)
    {
        throw new InvalidOperationException("Temporary service unavailable");
    }
    
    return "Operation completed successfully";
}, 
maxRetries: 5,
initialDelayMs: 200,
backoffMultiplier: 1.8);

Console.WriteLine(result);

// Example 2: Retry a synchronous operation
var data = RetryPolicyHelper.ExecuteWithRetry(() =>
{
    // Simulate database query that might fail temporarily
    if (DateTime.UtcNow.Second % 2 == 0)
    {
        throw new TimeoutException("Database connection timed out");
    }
    
    return new { Id = 42, Name = "Test Data", Timestamp = DateTime.UtcNow };
},
maxRetries: 3,
initialDelayMs: 150);

Console.WriteLine($"Retrieved data: {data.Id} - {data.Name}");

// Example 3: Linear retry for predictable delays
var linearResult = await RetryPolicyHelper.ExecuteWithLinearRetryAsync(async () =>
{
    // Operation with linear backoff
    if (DateTime.UtcNow.Second % 4 == 0)
    {
        throw new Exception("Service temporarily unavailable");
    }
    
    return "Linear retry successful";
},
maxRetries: 4,
delayIncrementMs: 100);

Console.WriteLine(linearResult);

// Example 4: Using predefined retry policies
var policy = RetryPolicyHelper.Policies.Aggressive;

var aggressiveResult = await RetryPolicyHelper.ExecuteWithRetryAsync(async () =>
{
    // High contention scenario
    return await TryAcquireLockWithHighContentionAsync();
},
maxRetries: policy.MaxRetries,
initialDelayMs: policy.InitialDelayMs,
backoffMultiplier: policy.BackoffMultiplier);

// Example 5: Custom retry policy with specific conditions
var customPolicy = RetryPolicyHelper.CreatePolicy(
    maxRetries: 5,
    initialDelayMs: 100,
    backoffMultiplier: 2.0);

var customResult = await RetryPolicyHelper.ExecuteWithRetryAsync(async () =>
{
    var lockService = new LockService(lockRepository, logger);
    var (success, _, _) = await lockService.TryAcquireAsync(
        "critical-section-lock",
        "worker-service-1",
        TimeSpan.FromMinutes(1));
    
    if (!success) throw new LockAcquisitionException("Could not acquire lock");
    
    return "Lock acquired successfully";
},
maxRetries: customPolicy.MaxRetries,
initialDelayMs: customPolicy.InitialDelayMs,
backoffMultiplier: customPolicy.BackoffMultiplier);
```

## ValidationHelper

The `ValidationHelper` class provides utility methods for validating inputs and configurations in the distributed lock system. It includes validation methods for lock names, durations, renewal intervals, fencing tokens, API keys, and other critical parameters. The helper collects validation errors and provides methods to check overall validity, throw exceptions if any errors exist, and parse values with type safety.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Utilities.Helpers;
using SarmKadan.DistributedLock.Models;

// Validate a lock name
ValidationHelper.ValidateLockName("user-session-lock-123");

// Validate duration (must be positive)
ValidationHelper.ValidateDuration(TimeSpan.FromMinutes(5));

// Validate renewal interval
ValidationHelper.ValidateRenewalInterval(TimeSpan.FromMinutes(2));

// Validate fencing token
ValidationHelper.ValidateFencingToken(12345UL);

// Validate owner ID
ValidationHelper.ValidateOwnerId("worker-service-01");

// Validate API key format
ValidationHelper.ValidateApiKey("sk_live_abc123xyz789");

// Validate that a lock is not expired
var lockInfo = new Lock(
    key: "distributed-lock-1",
    ownerId: "worker-service-01",
    duration: TimeSpan.FromMinutes(5),
    fencingToken: 12345
);
ValidationHelper.ValidateLockNotExpired(lockInfo);

// Validate lock configuration with multiple checks
ValidationHelper.ValidateLockConfiguration(
    lockName: "critical-section-lock",
    duration: TimeSpan.FromMinutes(10),
    renewalInterval: TimeSpan.FromMinutes(2),
    fencingToken: 67890UL,
    ownerId: "background-task-worker"
);

// Check if any validation errors occurred
if (!ValidationHelper.IsValid)
{
    Console.WriteLine("Validation errors:");
    foreach (var error in ValidationHelper.Errors)
    {
        Console.WriteLine($"- {error}");
    }
}

// Throw exception if any errors exist
ValidationHelper.ThrowIfAnyErrors("Invalid lock configuration");

// Validate HTTP headers for required values
var headers = new Dictionary<string, string>
{
    ["X-API-Key"] = "sk_live_abc123xyz789",
    ["X-Request-Id"] = "req-12345"
};
ValidationHelper.ValidateHeaders(headers, ["X-API-Key", "X-Request-Id"]);

// Try to parse a value with type safety
if (ValidationHelper.TryParseAs<int>("42", out var parsedValue))
{
    Console.WriteLine($"Parsed value: {parsedValue}");
}

// Get validation result for programmatic checking
var validationResult = ValidationHelper.ValidateLockConfiguration(
    lockName: "api-rate-limit-lock",
    duration: TimeSpan.FromSeconds(30),
    renewalInterval: TimeSpan.FromSeconds(10),
    fencingToken: 11111UL,
    ownerId: "rate-limiter-service"
);

if (validationResult.IsValid)
{
    Console.WriteLine("Configuration is valid!");
}
else
{
    Console.WriteLine("Configuration errors:");
    foreach (var error in validationResult.Errors)
    {
        Console.WriteLine($"- {error}");
    }
}
```

## CollectionExtensions

The `CollectionExtensions` class provides extension methods for collections and enumerables that simplify common operations like batching, safe access, and dictionary manipulation. These utilities are particularly useful for processing lock collections, managing configuration dictionaries, and implementing batch operations in distributed scenarios.

### Public Members

```csharp
public static bool IsNullOrEmpty<T>
public static bool HasElements<T>
public static IEnumerable<IEnumerable<T>> Batch<T>
public static TValue? GetValueOrDefault<TKey, TValue>
public static void AddIfNotExists<TKey, TValue>
public static Dictionary<TKey, TValue> Merge<TKey, TValue>
public static IEnumerable<T> ForEach<T>
public static HashSet<T> ToHashSet<T>
public static (List<T> matching, List<T> nonMatching) Partition<T>
public static T? MostFrequent<T>
public static T? SafeGetAt<T>
```

### Usage Example

```csharp
using SarmKadan.DistributedLock.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

// Example 1: Check if collection is null or empty
var lockNames = new List<string> { "lock-1", "lock-2", "lock-3" };
bool isEmpty = lockNames.IsNullOrEmpty(); // false
bool isNull = ((List<string>)null).IsNullOrEmpty(); // true

// Example 2: Check if collection has elements
bool hasElements = lockNames.HasElements(); // true
bool hasNoElements = new List<string>().HasElements(); // false

// Example 3: Batch processing for lock operations
var allLocks = new List<string>();
for (int i = 0; i < 100; i++) allLocks.Add($"lock-{i}");

// Process locks in batches of 10 for better performance
foreach (var batch in allLocks.Batch(10))
{
    Console.WriteLine($"Processing batch of {batch.Count()} locks");
    // Batch operations like bulk acquisition, renewal, or release
    foreach (var lockName in batch)
    {
        // Process each lock in the batch
        Console.WriteLine($"  - {lockName}");
    }
}

// Example 4: Safe dictionary access with default values
var lockTimeouts = new Dictionary<string, TimeSpan>
{
    ["default-timeout"] = TimeSpan.FromMinutes(5),
    ["short-timeout"] = TimeSpan.FromSeconds(30)
};

// Get timeout with fallback to default
TimeSpan timeout = lockTimeouts.GetValueOrDefault("user-session-lock", TimeSpan.FromMinutes(2));
Console.WriteLine($"Timeout for user-session-lock: {timeout.TotalSeconds}s");

// Example 5: Add to dictionary only if key doesn't exist
var lockMetadata = new Dictionary<string, string>();
lockMetadata.AddIfNotExists("lock-1", "metadata-1");
lockMetadata.AddIfNotExists("lock-1", "metadata-overridden"); // Not added
Console.WriteLine($"Lock metadata count: {lockMetadata.Count}"); // 1

// Example 6: Merge multiple configuration dictionaries
var config1 = new Dictionary<string, string> { ["timeout"] = "30" };
var config2 = new Dictionary<string, string> { ["retries"] = "5", ["timeout"] = "60" };
var config3 = new Dictionary<string, string> { ["jitter"] = "0.2" };

var mergedConfig = new[] { config1, config2, config3 }.Merge();
Console.WriteLine($"Merged config count: {mergedConfig.Count}"); // 3
Console.WriteLine($"Timeout value: {mergedConfig.GetValueOrDefault("timeout")}"); // 60

// Example 7: ForEach extension for chainable operations
var lockIds = new List<string> { "lock-a", "lock-b", "lock-c" };
var processedLocks = lockIds
    .ForEach(id => Console.WriteLine($"Processing {id}"))
    .ToList();

// Example 8: Convert to HashSet for efficient lookups
var activeLocks = new List<string> { "lock-1", "lock-2", "lock-3", "lock-1" };
var uniqueActiveLocks = activeLocks.ToHashSet();
Console.WriteLine($"Unique active locks: {uniqueActiveLocks.Count}"); // 3

// Example 9: Partition locks by criteria
var locks = new List<string> { "user-lock", "system-lock", "user-lock", "admin-lock" };
var (userLocks, systemLocks) = locks.Partition(l => l.StartsWith("user"));
Console.WriteLine($"User locks: {userLocks.Count}, System locks: {systemLocks.Count}"); // 2, 2

// Example 10: Find most frequent lock owner
var lockOwners = new List<string> { "worker-1", "worker-2", "worker-1", "worker-3", "worker-1" };
var mostFrequentOwner = lockOwners.MostFrequent();
Console.WriteLine($"Most frequent owner: {mostFrequentOwner}"); // worker-1

// Example 11: Safe array/list access
var lockArray = new string[] { "lock-1", "lock-2", "lock-3" };
var firstLock = lockArray.SafeGetAt(0); // lock-1
var outOfBoundsLock = lockArray.SafeGetAt(10); // null
var negativeIndexLock = lockArray.SafeGetAt(-1); // null
```

## StringExtensions

The `StringExtensions` class provides extension methods for string manipulation and validation commonly used in distributed locking scenarios. It includes methods for validating lock names, sanitizing strings for lock identifiers, and converting strings to various byte encodings. These utilities help ensure consistent string handling across the lock system and prevent common issues with invalid characters or encoding mismatches.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Utilities.Extensions;
using System;

// Validate a lock name before attempting to acquire a lock
string lockName = "critical-section-123";
bool isValid = lockName.IsValidLockName();
Console.WriteLine($"Is valid lock name: {isValid}"); // Output: True

// Sanitize a user-provided string for use as a lock name
string userInput = "My Critical Section!@#"
string sanitizedLockName = userInput.SanitizeForLockName();
Console.WriteLine($"Sanitized lock name: {sanitizedLockName}"); // Output: My_Critical_Section____

// Convert strings to byte arrays for hashing or storage
string data = "Hello, Distributed Lock System"
byte[] utf8Bytes = data.ToUtf8Bytes();
byte[] asciiBytes = data.ToAsciiBytes();
Console.WriteLine($"UTF-8 bytes length: {utf8Bytes.Length}, ASCII bytes length: {asciiBytes.Length}");

// Parse hex strings (useful for fencing tokens)
string hexToken = "deadbeefcafebabe"
byte[] tokenBytes = hexToken.FromHexString();
Console.WriteLine($"Hex token parsed to {tokenBytes.Length} bytes");

// Validate GUID format
string guidString = "550e8400-e29b-41d4-a716-446655440000"
bool isGuidValid = guidString.IsValidGuid();
Console.WriteLine($"Is valid GUID: {isGuidValid}"); // Output: True

// Truncate long strings for display
string longString = "This is a very long lock identifier that needs to be shortened"
string truncated = longString.TruncateWithEllipsis(20);
Console.WriteLine($"Truncated: {truncated}"); // Output: This is a very lon...

// Split delimited strings into lists
string lockList = "lock-1, lock-2, lock-3, lock-4"
var lockNames = lockList.ToTrimmedList();
Console.WriteLine($"Parsed {lockNames.Count} lock names: {string.Join(", ", lockNames)}");
```

## DateTimeExtensions

The `DateTimeExtensions` class provides extension methods for DateTime operations commonly used in distributed locking scenarios. It includes methods for checking lock expiration status, calculating remaining time until expiration, formatting dates, and adding jitter to time intervals. These utilities help ensure consistent time handling across the lock system and prevent common issues with time synchronization.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Utilities.Extensions;
using System;

// Example lock expiration time
var lockExpiration = DateTime.UtcNow.AddMinutes(5);

// Check if lock has expired
bool isExpired = lockExpiration.IsExpired();
Console.WriteLine($"Lock expired: {isExpired}");

// Check if lock is still valid
bool isValid = lockExpiration.IsValid();
Console.WriteLine($"Lock valid: {isValid}");

// Get remaining time until lock expires
TimeSpan remainingTime = lockExpiration.GetRemainingTime();
Console.WriteLine($"Remaining time: {remainingTime.TotalSeconds:F0} seconds");

// Get remaining time in different units
long remainingSeconds = lockExpiration.GetRemainingSeconds();
long remainingMilliseconds = lockExpiration.GetRemainingMilliseconds();
Console.WriteLine($"Remaining: {remainingSeconds}s / {remainingMilliseconds}ms");

// Check if lock expires within a grace period (e.g., 1 minute)
bool needsRenewal = lockExpiration.ExpiresWithin(TimeSpan.FromMinutes(1));
Console.WriteLine($"Needs renewal: {needsRenewal}");

// Format dates for logging and storage
string iso8601 = lockExpiration.ToIso8601String();
Console.WriteLine($"ISO 8601: {iso8601}");

string humanReadable = lockExpiration.ToHumanReadableFormat();
Console.WriteLine($"Human readable: {humanReadable}");

// Round to nearest time interval (useful for metrics aggregation)
var rounded = DateTime.UtcNow.RoundToNearest(TimeSpan.FromMinutes(5));
Console.WriteLine($"Rounded to nearest 5 minutes: {rounded}");

// Add random jitter to renewal intervals to avoid thundering herd
TimeSpan renewalInterval = TimeSpan.FromMinutes(2);
TimeSpan jitteredInterval = renewalInterval.AddRandomJitter(maxJitterPercentage: 15);
Console.WriteLine($"Renewal interval with jitter: {jitteredInterval.TotalSeconds:F0}s");

// Convert between DateTime and Unix timestamp
DateTime now = DateTime.UtcNow;
long unixTimestamp = now.ToUnixTimestamp();
DateTime fromTimestamp = DateTimeExtensions.FromUnixTimestamp(unixTimestamp);
Console.WriteLine($"Unix timestamp: {unixTimestamp}");
Console.WriteLine($"Round-trip successful: {Math.Abs((now - fromTimestamp).TotalMilliseconds) < 1}");
```

## ObjectExtensions

The `ObjectExtensions` class provides extension methods for general object operations including serialization, cloning, type conversion, and validation. These utilities simplify common patterns like deep cloning objects, JSON serialization, safe type casting, and fluent API operations. The extensions work with any serializable object and provide robust error handling to prevent exceptions during common operations.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Utilities.Extensions;
using System;
using System.Text.Json;

// Example object to work with
var lockConfiguration = new LockConfiguration
{
    LockName = "critical-section-1",
    Duration = TimeSpan.FromMinutes(5),
    MaxRetries = 3,
    RenewalInterval = TimeSpan.FromMinutes(1)
};

// Example 1: Deep clone an object
var clonedConfig = lockConfiguration.DeepClone();
Console.WriteLine($"Cloned configuration: {clonedConfig?.LockName}");

// Example 2: Serialize to pretty-printed JSON
string jsonPretty = lockConfiguration.ToJsonString();
Console.WriteLine("Pretty JSON:");
Console.WriteLine(jsonPretty);

// Example 3: Serialize to compact JSON
string jsonCompact = lockConfiguration.ToCompactJsonString();
Console.WriteLine($"\nCompact JSON: {jsonCompact}");

// Example 4: Check if object is null or default
bool isNullOrDefault = lockConfiguration.IsNullOrDefault();
Console.WriteLine($"\nIs null or default: {isNullOrDefault}");

// Example 5: Safe type casting
object obj = "test-string";
if (obj.TryCast(out string? castedString))
{
    Console.WriteLine($"Cast successful: {castedString}");
}

// Example 6: Compute hash from object properties
int hashCode = lockConfiguration.ComputeHash(lockConfiguration.LockName, lockConfiguration.Duration);
Console.WriteLine($"\nComputed hash: {hashCode}");

// Example 7: Fluent API with Tap
lockConfiguration
    .Tap(config => Console.WriteLine($"Processing: {config.LockName}"))
    .Tap(config => Console.WriteLine($"Duration: {config.Duration.TotalMinutes} minutes"));

// Example 8: Map object to another type
var lockName = lockConfiguration.MapTo(config => config.LockName);
Console.WriteLine($"\nMapped lock name: {lockName}");

// Example 9: Get simple type name
string typeName = lockConfiguration.GetSimpleTypeName();
Console.WriteLine($"Simple type name: {typeName}");

// Example 10: Validate object properties
bool isValid = lockConfiguration.Validate(config => 
    !string.IsNullOrEmpty(config.LockName) && 
    config.Duration > TimeSpan.Zero);
Console.WriteLine($"\nIs valid configuration: {isValid}");

// Define a simple record for examples
public record LockConfiguration(string LockName, TimeSpan Duration, int MaxRetries, TimeSpan RenewalInterval);
```

## XmlLockSerializer

The `XmlLockSerializer` class provides XML serialization and deserialization capabilities for lock data structures in the distributed lock system. It handles serialization of `Lock` objects, collections of locks, and lock metrics with proper XML formatting, namespace management, and error handling. The serializer uses XML 1.0 compliant output with UTF-8 encoding and includes comprehensive validation for enterprise integration scenarios.

### Public Members

```csharp
public static string SerializeLock(Lock @lock)
public static string SerializeLocks(IEnumerable<Lock> locks)
public static Lock? DeserializeLock(string xml)
public static List<Lock> DeserializeLocks(string xml)
public static string ExportMetrics(IEnumerable<LockMetrics> metrics)
```

### Usage Example

```csharp
using SarmKadan.DistributedLock.Formatters;
using SarmKadan.DistributedLock.Models;
using System;

// Create a lock instance
var newLock = new Lock(
    key: "critical-section-123",
    ownerId: "background-worker-42",
    duration: TimeSpan.FromMinutes(5),
    fencingToken: 12345
);

// Serialize a single lock to XML
string lockXml = XmlLockSerializer.SerializeLock(newLock);
Console.WriteLine("Serialized lock:");
Console.WriteLine(lockXml);

// Serialize multiple locks to XML
var locks = new List<Lock>
{
    newLock,
    new Lock(
        key: "user-session-456",
        ownerId: "auth-service",
        duration: TimeSpan.FromMinutes(2),
        fencingToken: 67890
    )
};
string locksXml = XmlLockSerializer.SerializeLocks(locks);
Console.WriteLine("\nSerialized locks collection:");
Console.WriteLine(locksXml);

// Deserialize a lock from XML
string lockXmlToDeserialize = @"<?xml version=\"1.0\" encoding=\"utf-8\"?>
<Lock xmlns=\"http://sarmkadan.com/distributedlock/2026\">
  <Key>critical-section-123</Key>
  <OwnerId>background-worker-42</OwnerId>
  <Duration>PT5M</Duration>
  <FencingToken>12345</FencingToken>
  <CreatedAt>2024-01-15T10:30:00Z</CreatedAt>
  <ExpiresAt>2024-01-15T10:35:00Z</ExpiresAt>
</Lock>";

Lock? deserializedLock = XmlLockSerializer.DeserializeLock(lockXmlToDeserialize);
if (deserializedLock != null)
{
    Console.WriteLine($"\nDeserialized lock: {deserializedLock.Key} owned by {deserializedLock.OwnerId}");
}

// Deserialize multiple locks from XML
string locksXmlToDeserialize = @"<?xml version=\"1.0\" encoding=\"utf-8\"?>
<Locks xmlns=\"http://sarmkadan.com/distributedlock/2026\" Count=\"2\" ExportTime=\"2024-01-15T10:30:00.0000000Z\">
  <Lock>
    <Key>critical-section-123</Key>
    <OwnerId>background-worker-42</OwnerId>
    <Duration>PT5M</Duration>
    <FencingToken>12345</FencingToken>
  </Lock>
  <Lock>
    <Key>user-session-456</Key>
    <OwnerId>auth-service</OwnerId>
    <Duration>PT2M</Duration>
    <FencingToken>67890</FencingToken>
  </Lock>
</Locks>";

var deserializedLocks = XmlLockSerializer.DeserializeLocks(locksXmlToDeserialize);
Console.WriteLine($"\nDeserialized {deserializedLocks.Count} locks from collection");

// Export metrics in XML format
var metrics = new List<LockMetrics>
{
    new LockMetrics
    {
        Id = "critical-section-123",
        AcquisitionAttempts = 150,
        SuccessfulAcquisitions = 145,
        FailedAcquisitions = 5,
        AverageHoldTimeMs = 1250.5,
        MaxHoldTimeMs = 5000,
        ContentionCount = 2,
        LastAcquisitionTime = DateTime.UtcNow
    }
};
string metricsXml = XmlLockSerializer.ExportMetrics(metrics);
Console.WriteLine("\nExported metrics:");
Console.WriteLine(metricsXml);
```

## JsonLockSerializer

The `JsonLockSerializer` class provides JSON serialization and deserialization capabilities for lock data structures in the distributed lock system. It handles serialization of `Lock` objects, collections of locks, and lock metrics with consistent formatting and UTC timezone support. The serializer uses camelCase property naming, ignores null values, and includes proper error handling for robust JSON operations.

### Public Members

```csharp
public static string SerializeLock(Lock @lock)
public static string SerializeLocks(IEnumerable<Lock> locks)
public static Lock? DeserializeLock(string json)
public static List<Lock> DeserializeLocks(string json)
public static string SerializeMetrics(LockMetrics metrics)
public static string SerializeLockPretty(Lock @lock)
```

### Usage Example

```csharp
using SarmKadan.DistributedLock.Formatters;
using SarmKadan.DistributedLock.Models;
using System;

// Create a lock instance
var newLock = new Lock(
    key: "critical-section-123",
    ownerId: "background-worker-42",
    duration: TimeSpan.FromMinutes(5),
    fencingToken: 12345
);

// Serialize a single lock to JSON
string lockJson = JsonLockSerializer.SerializeLock(newLock);
Console.WriteLine("Serialized lock:");
Console.WriteLine(lockJson);

// Serialize multiple locks to JSON array
var locks = new List<Lock>
{
    newLock,
    new Lock(
        key: "user-session-456",
        ownerId: "auth-service",
        duration: TimeSpan.FromMinutes(2),
        fencingToken: 67890
    )
};
string locksJson = JsonLockSerializer.SerializeLocks(locks);
Console.WriteLine("\nSerialized locks collection:");
Console.WriteLine(locksJson);

// Deserialize a lock from JSON
string lockJsonToDeserialize = @"{
    ""key"": ""critical-section-123"",
    ""ownerId"": ""background-worker-42"",
    ""duration"": ""00:05:00"",
    ""fencingToken"": 12345,
    ""createdAt"": ""2024-01-15T10:30:00Z"",
    ""expiresAt"": ""2024-01-15T10:35:00Z""
}";
Lock? deserializedLock = JsonLockSerializer.DeserializeLock(lockJsonToDeserialize);
if (deserializedLock != null)
{
    Console.WriteLine($"\nDeserialized lock: {deserializedLock.Key} owned by {deserializedLock.OwnerId}");
}

// Deserialize multiple locks from JSON array
string locksJsonToDeserialize = @"[
    {
        ""key"": ""critical-section-123"",
        ""ownerId"": ""background-worker-42"",
        ""duration"": ""00:05:00"",
        ""fencingToken"": 12345
    },
    {
        ""key"": ""user-session-456"",
        ""ownerId"": ""auth-service"",
        ""duration"": ""00:02:00"",
        ""fencingToken"": 67890
    }
]";
var deserializedLocks = JsonLockSerializer.DeserializeLocks(locksJsonToDeserialize);
Console.WriteLine($"\nDeserialized {deserializedLocks.Count} locks from collection");

// Serialize metrics for reporting
var metrics = new LockMetrics
{
    TotalAcquisitions = 150,
    TotalReleases = 120,
    TotalRenewals = 30,
    ActiveLocks = 5,
    AverageHoldTimeMs = 1250.5
};
string metricsJson = JsonLockSerializer.SerializeMetrics(metrics);
Console.WriteLine("\nSerialized metrics:");
Console.WriteLine(metricsJson);

// Create pretty-printed JSON for debugging
string prettyJson = JsonLockSerializer.SerializeLockPretty(newLock);
Console.WriteLine("\nPretty-printed lock for debugging:");
Console.WriteLine(prettyJson);
```

## CsvLockExporter

The `CsvLockExporter` class provides CSV export functionality for lock data, enabling integration with reporting tools, audit systems, and data analysis pipelines. It exports individual locks, collections of locks, and lock metrics to CSV format with proper escaping, headers, and configurable formatting options. The exporter supports both in-memory string generation and streaming to files or network streams for handling large datasets efficiently.

### Public Members

```csharp
public static string ExportLock
public static string ExportLocks
public static async Task ExportLocksToStreamAsync
public static string ExportMetrics
public bool IncludeHeader { get; set; }
public char Delimiter { get; set; }
public bool IncludeMetadata { get; set; }
public Encoding Encoding { get; set; }
```

### Usage Example

```csharp
using SarmKadan.DistributedLock.Formatters;
using SarmKadan.DistributedLock.Models;
using System;
using System.IO;

// Create sample lock data
var locks = new List<Lock>
{
    new Lock(
        key: "user-session-lock-123",
        name: "User Session Lock",
        ownerId: "auth-service-42",
        duration: TimeSpan.FromMinutes(5),
        fencingToken: 12345,
        autoRenew: true
    ),
    new Lock(
        key: "api-rate-limit-lock",
        name: "API Rate Limit Lock",
        ownerId: "rate-limiter-service",
        duration: TimeSpan.FromSeconds(30),
        fencingToken: 67890,
        autoRenew: false
    )
};

// Export a single lock to CSV
string singleLockCsv = CsvLockExporter.ExportLock(locks[0]);
Console.WriteLine("Single lock CSV:");
Console.WriteLine(singleLockCsv);

// Export multiple locks to CSV
string multipleLocksCsv = CsvLockExporter.ExportLocks(locks);
Console.WriteLine("\nMultiple locks CSV:");
Console.WriteLine(multipleLocksCsv);

// Export to a file
await File.WriteAllTextAsync("locks-export.csv", multipleLocksCsv);

// Export to a stream (memory-efficient for large datasets)
using (var memoryStream = new MemoryStream())
{
    await CsvLockExporter.ExportLocksToStreamAsync(locks, memoryStream);
    
    // Reset stream position to read
    memoryStream.Position = 0;
    using (var reader = new StreamReader(memoryStream))
    {
        string streamCsv = await reader.ReadToEndAsync();
        Console.WriteLine("\nStream export CSV:");
        Console.WriteLine(streamCsv);
    }
}

// Export lock metrics
var metrics = new List<LockMetrics>
{
    new LockMetrics
    {
        Id = "user-session-lock-123",
        AcquisitionAttempts = 150,
        SuccessfulAcquisitions = 145,
        FailedAcquisitions = 5,
        AverageHoldTimeMs = 1250.5,
        MaxHoldTimeMs = 5000,
        ContentionCount = 2,
        LastAcquisitionTime = DateTime.UtcNow
    }
};

string metricsCsv = CsvLockExporter.ExportMetrics(metrics);
Console.WriteLine("\nMetrics CSV:");
Console.WriteLine(metricsCsv);
```

## CacheKeyGenerator

The `CacheKeyGenerator` class provides utility methods for generating consistent, predictable cache keys used throughout the distributed lock system. It ensures consistent key formats across all components for cache coordination, supports pattern matching for bulk operations, and provides methods for extracting information from keys. The generator creates keys for individual locks, lock families, metrics, status, owners, queries, configurations, and tags, with helper methods to identify key types and extract lock IDs.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

// Initialize a distributed cache (e.g., Redis, MemoryCache, etc.)
var cache = new MemoryDistributedCache(new OptionsWrapper<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions()));

// Generate keys for various cache operations
string lockKey = CacheKeyGenerator.GenerateLockKey("user-session-lock-123");
Console.WriteLine($"Lock key: {lockKey}"); // Output: lock:user-session-lock-123

string metricsKey = CacheKeyGenerator.GenerateMetricsKey("user-session-lock-123");
Console.WriteLine($"Metrics key: {metricsKey}"); // Output: metrics:user-session-lock-123

string systemMetricsKey = CacheKeyGenerator.GenerateSystemMetricsKey();
Console.WriteLine($"System metrics key: {systemMetricsKey}"); // Output: metrics:system

string ownerLocksKey = CacheKeyGenerator.GenerateOwnerLocksKey("user-service-42");
Console.WriteLine($"Owner locks key: {ownerLocksKey}"); // Output: lock:owner:user-service-42

string statusKey = CacheKeyGenerator.GenerateStatusKey("user-session-lock-123");
Console.WriteLine($"Status key: {statusKey}"); // Output: status:user-session-lock-123

string configurationKey = CacheKeyGenerator.GenerateConfigurationKey("default-lock-timeout");
Console.WriteLine($"Configuration key: {configurationKey}"); // Output: config:default-lock-timeout

string tagKey = CacheKeyGenerator.GenerateTagKey("session-management", "user-locks");
Console.WriteLine($"Tag key: {tagKey}"); // Output: tag:session-management:user-locks

// Check if a key is a lock key
bool isLockKey = CacheKeyGenerator.IsLockKey(lockKey);
Console.WriteLine($"Is lock key: {isLockKey}"); // Output: True

// Check if a key is a metrics key
bool isMetricsKey = CacheKeyGenerator.IsMetricsKey(metricsKey);
Console.WriteLine($"Is metrics key: {isMetricsKey}"); // Output: True

// Extract lock ID from a cache key
string? extractedLockId = CacheKeyGenerator.ExtractLockIdFromKey(lockKey);
Console.WriteLine($"Extracted lock ID: {extractedLockId}"); // Output: user-session-lock-123

// Get keys to invalidate on lock acquisition
string[] acquisitionKeys = CacheKeySets.GetKeysByAcquisition("user-session-lock-123", "user-service-42");
Console.WriteLine("Keys to invalidate on acquisition:");
foreach (var key in acquisitionKeys)
{
    Console.WriteLine($"  - {key}");
}

// Get keys to invalidate on lock release
string[] releaseKeys = CacheKeySets.GetKeysByRelease("user-session-lock-123", "user-service-42");
Console.WriteLine("Keys to invalidate on release:");
foreach (var key in releaseKeys)
{
    Console.WriteLine($"  - {key}");
}

// Generate a query key for parameterized queries
string queryKey = CacheKeyGenerator.GenerateQueryKey("get-active-locks", "user-service-42", "active");
Console.WriteLine($"Query key: {queryKey}");

// Generate a pattern for finding all active locks
string activeLocksPattern = CacheKeyGenerator.GenerateActiveLockKeysPattern();
Console.WriteLine($"Active locks pattern: {activeLocksPattern}"); // Output: lock:active:*
```

## DistributedCacheExtensions

The `DistributedCacheExtensions` class provides extension methods for `IDistributedCache` that simplify working with distributed caches in distributed lock scenarios. It offers type-safe JSON serialization/deserialization, common caching patterns, and robust error handling to ensure cache operations never throw exceptions. The extensions support absolute expiration, sliding expiration, bulk operations, and pattern-based invalidation (where supported by the cache provider).

### Usage Example

```csharp
using SarmKadan.DistributedLock.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System;

// Initialize a distributed cache (e.g., Redis, MemoryCache, etc.)
var cache = new MemoryDistributedCache(new OptionsWrapper<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions()));

// Cache a complex object as JSON
var userSession = new UserSession
{
    UserId = "user-123",
    LockCount = 5,
    LastActivity = DateTime.UtcNow,
    ActiveLocks = new List<string> { "lock-1", "lock-2", "lock-3" }
};

// Set a value with absolute expiration
await cache.SetAsJsonAsync(
    "user-session:user-123",
    userSession,
    expiration: TimeSpan.FromMinutes(30)
);

// Get a cached value with type safety
var cachedSession = await cache.GetAsJsonAsync<UserSession>("user-session:user-123");
if (cachedSession != null)
{
    Console.WriteLine($"Found cached session for user {cachedSession.UserId} with {cachedSession.ActiveLocks.Count} active locks");
}

// Use GetOrCreateAsync for cache-aside pattern
var lockInfo = await cache.GetOrCreateAsync(
    "lock-info:critical-section-1",
    async () =>
    {
        // Expensive computation or database lookup
        return await FetchLockInfoFromDatabaseAsync("critical-section-1");
    },
    expiration: TimeSpan.FromMinutes(15)
);

// Check if a key exists
bool exists = await cache.ExistsAsync("user-session:user-123");
Console.WriteLine($"Session exists: {exists}");

// Remove a single key
await cache.RemoveAsync("temp-cache-key");

// Remove multiple keys
await cache.RemoveAsync("lock:temp-1", "lock:temp-2", "lock:temp-3");

// Set expiration to a specific time
await cache.SetExpirationAsync(
    "user-session:user-123",
    DateTime.UtcNow.AddHours(1)
);

// Cache with sliding expiration (resets on access)
await cache.SetWithSlidingExpirationAsync(
    "rate-limit:api-endpoint-1",
    new RateLimitInfo
    {
        RequestCount = 100,
        WindowStart = DateTime.UtcNow
    },
    slidingExpiration: TimeSpan.FromMinutes(5)
);

// Pattern-based invalidation (conceptual - provider-specific implementation required)
// For Redis: use SCAN + DEL pattern
// For other providers: implement provider-specific pattern matching
await cache.InvalidatePatternAsync("lock:temp-*");

// Helper method for cache-aside pattern with lock coordination
async Task<UserSession?> GetUserSessionWithLockAsync(IDistributedCache cache, string userId)
{
    // Try cache first
    var cached = await cache.GetAsJsonAsync<UserSession>($"user-session:{userId}");
    if (cached != null) return cached;
    
    // Cache miss - acquire distributed lock for this user
    var lockService = new LockService(...); // Your lock service
    var (success, _, _) = await lockService.TryAcquireAsync(
        $"user-session-lock:{userId}",
        "session-manager",
        TimeSpan.FromSeconds(10)
    );
    
    if (success)
    {
        try
        {
            // Double-check cache after acquiring lock
            cached = await cache.GetAsJsonAsync<UserSession>($"user-session:{userId}");
            if (cached != null) return cached;
            
            // Fetch from database
            var session = await FetchUserSessionFromDatabaseAsync(userId);
            
            // Cache for future requests
            await cache.SetAsJsonAsync(
                $"user-session:{userId}",
                session,
                expiration: TimeSpan.FromMinutes(30)
            );
            
            return session;
        }
        finally
        {
            await lockService.ReleaseAsync($"user-session-lock:{userId}", "session-manager");
        }
    }
    
    return null; // Could not acquire lock
}

// Example types used above
public record UserSession(string UserId, int LockCount, DateTime LastActivity, List<string> ActiveLocks);
public record RateLimitInfo(int RequestCount, DateTime WindowStart);
```

## MetricsController

The `MetricsController` class provides HTTP endpoints for monitoring and analyzing distributed lock operations. It exposes system-wide, lock-specific, and performance metrics that help track lock usage patterns, success rates, contention events, and acquisition times. The controller maintains an in-memory cache of lock metrics and provides endpoints to retrieve aggregated statistics, individual lock metrics, and performance percentiles.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<MetricsController>();

// Initialize with your lock service
var lockService = new LockService(lockRepository, logger);
var metricsController = new MetricsController(lockService, logger);

// Get system-wide metrics
var systemMetrics = metricsController.GetSystemMetrics();
if (systemMetrics.Value != null)
{
    Console.WriteLine($"Total lock operations: {systemMetrics.Value.TotalLockOperations}");
    Console.WriteLine($"Success rate: {systemMetrics.Value.SuccessRate:F2}%");
    Console.WriteLine($"Active locks: {systemMetrics.Value.ActiveLocks}");
}

// Get metrics for a specific lock
var lockMetrics = metricsController.GetLockMetrics("critical-section-lock");
if (lockMetrics.Value != null)
{
    Console.WriteLine($"Lock critical-section-lock metrics:");
    Console.WriteLine($"  Attempts: {lockMetrics.Value.AcquisitionAttempts}");
    Console.WriteLine($"  Successes: {lockMetrics.Value.SuccessfulAcquisitions}");
    Console.WriteLine($"  Failures: {lockMetrics.Value.FailedAcquisitions}");
    Console.WriteLine($"  Avg hold time: {lockMetrics.Value.AverageHoldTimeMs:F2}ms");
}

// Get performance metrics
var performanceMetrics = metricsController.GetPerformanceMetrics();
if (performanceMetrics.Value != null)
{
    Console.WriteLine($"Performance metrics:");
    Console.WriteLine($"  Median acquisition time: {performanceMetrics.Value.MedianAcquisitionTimeMs:F2}ms");
    Console.WriteLine($"  P95 acquisition time: {performanceMetrics.Value.P95AcquisitionTimeMs:F2}ms");
    Console.WriteLine($"  P99 acquisition time: {performanceMetrics.Value.P99AcquisitionTimeMs:F2}ms");
}

// Record metrics (typically called internally by LockService)
var recordRequest = new RecordMetricsRequest
{
    LockName = "api-rate-limit-lock",
    Successful = true,
    HoldTimeMs = 150,
    ContentionDetected = false
};
var recordResult = metricsController.RecordMetrics(recordRequest);

// Reset all metrics (for testing/debugging)
var resetResult = metricsController.ResetMetrics();
Console.WriteLine(resetResult.Value?.Message);
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

## HealthCheckController

The `HealthCheckController` class provides HTTP endpoints for monitoring the health of the distributed lock API service. It implements three standard health check patterns used by orchestration platforms and load balancers:

- **Liveness**: Indicates whether the service is running and responding to requests
- **Readiness**: Indicates whether the service can accept requests and connect to its backend dependencies  
- **Detailed Health**: Provides comprehensive health status including response times, runtime information, and backend connectivity

### Usage Example

```csharp
using Microsoft.AspNetCore.Mvc;
using SarmKadan.DistributedLock.Api.Controllers;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<HealthCheckController>();

// Initialize with your lock repository and logger
var lockRepository = new SqliteLockRepository("/var/data/distributed-locks.db", logger);
var healthCheckController = new HealthCheckController(lockRepository, logger);

// Liveness check - basic service status
var livenessResult = healthCheckController.Liveness();
if (livenessResult.Result is OkObjectResult okResult)
{
    var response = okResult.Value as HealthCheckResponse;
    Console.WriteLine($"Liveness Status: {response?.Status}");
    Console.WriteLine($"Version: {response?.Version}");
    Console.WriteLine($"Timestamp: {response?.Timestamp}");
}

// Readiness check - backend connectivity
var readinessResult = await healthCheckController.Readiness();
if (readinessResult.Result is OkObjectResult readyResult)
{
    var response = readyResult.Value as HealthCheckResponse;
    Console.WriteLine($"Readiness Status: {response?.Status}");
    Console.WriteLine($"Backend Connected: {response?.Details?.BackendConnected}");
    if (!response?.Details?.BackendConnected == true)
    {
        Console.WriteLine($"Error: {response?.Details?.ErrorMessage}");
    }
}

// Detailed health check - comprehensive metrics
var detailedResult = await healthCheckController.DetailedHealth();
if (detailedResult.Result is OkObjectResult detailedOkResult)
{
    var response = detailedOkResult.Value as DetailedHealthResponse;
    Console.WriteLine($"Health Status: {response?.Status}");
    Console.WriteLine($"Response Time: {response?.ResponseTimeMs}ms");
    Console.WriteLine($"Framework: {response?.Runtime?.Framework}");
    Console.WriteLine($"Uptime: {response?.Runtime?.Uptime}");
    Console.WriteLine($"Version: {response?.Version}");
}
```

## ExceptionHandlingMiddleware

The `ExceptionHandlingMiddleware` class is a global exception handling middleware that catches all unhandled exceptions during HTTP request processing and converts them to appropriate HTTP responses with meaningful error messages. It prevents sensitive stack traces from being exposed to clients while providing structured error responses that include the error message, error code, and timestamp. The middleware maps domain-specific exceptions to their corresponding HTTP status codes for improved client clarity.

## RateLimitingMiddleware

The `RateLimitingMiddleware` class is an HTTP middleware that protects API endpoints from abuse by limiting the number of requests a client can make within a sliding time window. It uses an in-memory sliding window counter to track request timestamps per client IP address, preventing denial-of-service attacks on lock acquisition endpoints. When a client exceeds the configured limit, the middleware returns a 429 Too Many Requests response with a Retry-After header indicating when the client can try again.

### Public Members

```csharp
public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger, RateLimitingOptions? options = null)
public async Task InvokeAsync(HttpContext context)

// Properties from RequestWindow (inner class)
public List<DateTime> Timestamps { get; }

// Properties from RateLimitingOptions (configuration)
public int MaxRequestsPerWindow { get; set; } = 100;
public int WindowSizeSeconds { get; set; } = 60;
```

### Usage Example

```csharp
using SarmKadan.DistributedLock.Api.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Configure services
var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole());
services.AddSingleton<ILogger<RateLimitingMiddleware>>(provider =>
    provider.GetRequiredService<ILoggerFactory>().CreateLogger<RateLimitingMiddleware>());

// Build service provider
var serviceProvider = services.BuildServiceProvider();

// Create WebApplication builder
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ILogger<RateLimitingMiddleware>>(serviceProvider.GetRequiredService<ILogger<RateLimitingMiddleware>>());

// Create the application
var app = builder.Build();

// Configure custom rate limiting options (optional)
var rateLimitingOptions = new RateLimitingOptions
{
    MaxRequestsPerWindow = 200,  // Allow 200 requests per minute
    WindowSizeSeconds = 60       // 1 minute window
};

// Register the middleware with custom options
app.UseMiddleware<RateLimitingMiddleware>(rateLimitingOptions);

// The middleware will now protect all subsequent middleware in the pipeline
app.MapGet("/api/locks", () => "Lock endpoints protected by rate limiting");

app.Run();
```

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

## ILockCacheManager

The `ILockCacheManager` interface provides an in-memory cache for storing and retrieving `Lock` objects to reduce backend storage access and improve performance. It tracks cache statistics including hits, misses, and hit rate, and supports configurable cache size, TTL (time-to-live), and compression. The cache automatically manages lock expiration and provides methods for CRUD operations on cached locks.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Caching;
using SarmKadan.DistributedLock.Models;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<InMemoryLockCacheManager>();

// Create cache with custom configuration
var cacheManager = new InMemoryLockCacheManager(
    maxCacheSize: 1000,
    ttlSeconds: 300,
    enableCompression: true,
    logger: logger
);

// Store a lock in cache
var newLock = new Lock(
    key: "distributed-lock-1",
    ownerId: "worker-service-01",
    duration: TimeSpan.FromMinutes(5),
    fencingToken: 12345
);

await cacheManager.SetAsync(newLock);

// Retrieve a lock from cache
var cachedLock = await cacheManager.GetAsync("distributed-lock-1");
if (cachedLock != null)
{
    Console.WriteLine($"Lock found in cache: {cachedLock.Key} owned by {cachedLock.OwnerId}");
    Console.WriteLine($"Cached at: {cachedLock.CachedAt}");
    Console.WriteLine($"Last accessed: {cachedLock.LastAccessTime}");
    Console.WriteLine($"Is expired: {cachedLock.IsExpired}");
}

// Get all cached locks
var allLocks = await cacheManager.GetAllAsync();
Console.WriteLine($"Total cached locks: {allLocks.Count}");

// Check cache statistics
var stats = cacheManager.GetStatistics();
Console.WriteLine($"Cache hits: {stats.Hits}");
Console.WriteLine($"Cache misses: {stats.Misses}");
Console.WriteLine($"Hit rate: {stats.HitRate:F2}%");
Console.WriteLine($"Cached items: {stats.CachedItems}");
Console.WriteLine($"Cache size: {stats.CacheSize}");

// Remove a lock from cache
await cacheManager.RemoveAsync("distributed-lock-1");

// Clear all cached locks
await cacheManager.ClearAsync();

// Get cache statistics (alternative method)
Console.WriteLine($"Current cache stats - Items: {cacheManager.CachedItems}, Hits: {cacheManager.Hits}, Misses: {cacheManager.Misses}, HitRate: {cacheManager.HitRate:F2}%");
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
