// existing content ...

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
