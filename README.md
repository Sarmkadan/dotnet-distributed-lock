# Architecture

For the full picture - module layout, backend trade-offs, data flow of a blocking
acquire, DI wiring, extension points and known limitations - see
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md). Short version: `ILockService` orchestrates
lock semantics (retry with backoff, fencing tokens, metrics) on top of a single
`ILockRepository` seam with Redis, PostgreSQL, SQLite and in-memory implementations.

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

## LockEventExtensions

The `LockEventExtensions` class provides extension methods for `LockEvent` types to enable common operations such as formatting, validation, and conversion between event types. These utilities simplify working with lock events by providing a consistent API for extracting information, checking event properties, and converting events to other types.


## InvalidFencingTokenExceptionExtensions

The `InvalidFencingTokenExceptionExtensions` class provides utility methods for analyzing and working with `InvalidFencingTokenException` instances. These extensions help determine the relationship between provided and current fencing tokens, create new exception instances with updated tokens, and extract detailed token information for logging and debugging purposes.

### What it does

This extension class provides methods to:

- Check if the provided token differs from the current token (`IsTokenMismatch`)
- Determine if the provided token is older than the current token (`IsTokenSuperseded`)
- Identify if the provided token is newer than the current token (`IsTokenFromFuture`)
- Create a new exception with updated tokens (`WithTokens`)
- Get a formatted string containing both token values for logging (`GetTokenDetails`)

### Usage Example

```csharp
using SarmKadan.DistributedLock.Exceptions;
using System;

try
{
    // Attempt to acquire a distributed lock with an outdated fencing token
    await lockService.AcquireAsync("resource-123", "stale-token-456", TimeSpan.FromSeconds(30));
}
catch (InvalidFencingTokenException ex)
{
    // Check the relationship between tokens
    bool isMismatch = ex.IsTokenMismatch();
    bool isSuperseded = ex.IsTokenSuperseded(); // true if provided token is older
    bool isFromFuture = ex.IsTokenFromFuture(); // true if provided token is newer
    
    Console.WriteLine($"Token mismatch: {isMismatch}");
    Console.WriteLine($"Token superseded: {isSuperseded}");
    Console.WriteLine($"Token from future: {isFromFuture}");
    
    // Get detailed token information for logging
    string tokenDetails = ex.GetTokenDetails();
    Console.WriteLine($"Token details: {tokenDetails}");
    
    // Create a new exception with updated tokens
    var updatedException = ex.WithTokens("new-provided-token-789", "new-current-token-789");
    Console.WriteLine($"Updated exception: {updatedException.GetTokenDetails()}");
}
```

### What it does

This extension class provides methods to:

- Check if an event represents a successful acquisition or failure (`IsAcquisitionSuccessful`, `IsFailure`)
- Extract lock identifiers and owner information from various event types (`GetLockId`, `GetOwnerId`)
- Format events for logging purposes (`ToLogString`)
- Convert events to failure notifications (`ToFailureEvent`)
- Check if events occurred within specific time ranges (`IsWithinTimeRange`)
- Calculate durations and expiration times (`GetDuration`, `GetExpirationTime`)
- Determine if events are related to specific locks (`IsRelatedToLock`)
- Extract fencing tokens from acquisition events (`GetFencingToken`)

## LockRequestContextExtensions

The `LockRequestContextExtensions` class provides extension methods for `LockRequestContext` to enhance functionality for audit trails, diagnostics, and distributed tracing scenarios. These utilities help determine lock request expiration status, calculate remaining time, generate diagnostic reports, check successful completion within duration, and collect standard metrics for monitoring and alerting.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Models;
using System;
using System.Collections.Generic;

// Create a lock request context
var requestContext = new LockRequestContext(
    requestId: Guid.NewGuid().ToString(),
    lockKey: "user-session-lock-123",
    requesterId: "auth-service-42",
    mode: LockMode.Exclusive,
    requestedDuration: TimeSpan.FromSeconds(30),
    requestedAt: DateTime.UtcNow
)
{
    RequestorName = "Authentication Service",
    UserId = "user-789",
    SessionId = "session-abc-123",
    CustomProperties = new Dictionary<string, object>
    {
        ["priority"] = "high",
        ["operation"] = "user_login"
    }
};

// Check if the lock request has expired
bool hasExpired = requestContext.HasExpired();
Console.WriteLine($"Has expired: {hasExpired}");

// Get the remaining time before expiration
TimeSpan remainingTime = requestContext.RemainingTime();
Console.WriteLine($"Remaining time: {remainingTime.TotalSeconds:F2} seconds");

// Generate a diagnostic report for logging and debugging
string diagnosticReport = requestContext.ToDiagnosticString();
Console.WriteLine(diagnosticReport);

// Determine if the request was completed successfully within the requested duration
bool isSuccessfulWithinDuration = requestContext.IsSuccessfulWithinDuration();
Console.WriteLine($"Successful within duration: {isSuccessfulWithinDuration}");

// Get standard metrics for monitoring and alerting
var metrics = requestContext.GetStandardMetrics();
foreach (var metric in metrics)
{
    Console.WriteLine($"{metric.Key}: {metric.Value}");
}
```

### What it does

This extension class provides methods to:

- Check if a lock request has expired based on the requested duration (`HasExpired`)
- Calculate the remaining time before expiration (`RemainingTime`)
- Generate a detailed diagnostic report containing request information (`ToDiagnosticString`)
- Determine if a request was completed successfully within the requested duration (`IsSuccessfulWithinDuration`)
- Collect standard metrics for monitoring and alerting (`GetStandardMetrics`)


### Usage Example

```csharp
using SarmKadan.DistributedLock.Models;
using System;
using System.Collections.Generic;

// Create a lock acquired event
var acquiredEvent = new LockAcquiredEvent(
    lockId: "user-session-lock-123",
    ownerId: "auth-service-42",
    status: LockStatus.Acquired,
    duration: TimeSpan.FromMinutes(5),
    expiresAt: DateTime.UtcNow.AddMinutes(5),
    fencingToken: 12345UL
)
{
    SourceSystem = "authentication-service",
    CorrelationId = Guid.NewGuid().ToString()
};

// Check if acquisition was successful
bool isSuccessful = acquiredEvent.IsAcquisitionSuccessful();
logger.LogInformation("Lock acquisition successful: {IsSuccessful}", isSuccessful);

// Get lock and owner information
string? lockId = acquiredEvent.GetLockId();
string? ownerId = acquiredEvent.GetOwnerId();
string? fencingToken = acquiredEvent.GetFencingToken();

logger.LogInformation("Lock ID: {LockId}, Owner: {OwnerId}, Fencing Token: {FencingToken}",
    lockId, ownerId, fencingToken);

// Get duration and expiration time
TimeSpan duration = acquiredEvent.GetDuration();
DateTime? expiresAt = acquiredEvent.GetExpirationTime();

logger.LogInformation("Lock held for: {Duration}, Expires at: {ExpiresAt}",
    duration, expiresAt?.ToString("O"));

// Format event for logging
string logString = acquiredEvent.ToLogString(includeTimestamp: true);
logger.LogInformation("Event log: {LogString}", logString);

// Convert any event to a failure notification
var failureEvent = acquiredEvent.ToFailureEvent("Lock acquisition timeout after 30 seconds");
logger.LogWarning("Converted to failure event: {Reason}", failureEvent.Reason);

// Check if event occurred within a time range
bool isWithinRange = acquiredEvent.IsWithinTimeRange(
    DateTime.UtcNow.AddMinutes(-10),
    DateTime.UtcNow.AddMinutes(10)
);
logger.LogInformation("Event within time range: {IsWithinRange}", isWithinRange);

// Check if event is related to a specific lock
bool isRelatedToSessionLock = acquiredEvent.IsRelatedToLock("user-session-lock-123");
logger.LogInformation("Event related to 'user-session-lock-123': {IsRelated}", isRelatedToSessionLock);

// Example with a failure event
var failedEvent = new LockFailedEvent(
    lockId: "payment-processing-lock",
    ownerId: "payment-service-1",
    reason: "Database connection timeout"
)
{
    SourceSystem = "payment-service",
    CorrelationId = Guid.NewGuid().ToString()
};

// Check if event represents a failure
bool isFailure = failedEvent.IsFailure();
logger.LogInformation("Event is failure: {IsFailure}", isFailure);

// Get lock information from failure event
string? failureLockId = failedEvent.GetLockId();
string? failureOwnerId = failedEvent.GetOwnerId();

logger.LogInformation("Failure - Lock ID: {LockId}, Owner: {OwnerId}, Reason: {Reason}",
    failureLockId, failureOwnerId, failedEvent.Reason);
```