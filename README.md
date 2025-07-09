// existing content ...

## ContentionMetrics

The `ContentionMetrics` class tracks contention statistics for a single lock key. It records how many waiters have attempted to acquire the lock, the peak number of simultaneous waiters, the number of deadlock cycles detected, and the average wait time for each waiter. These metrics are updated in a thread‑safe manner and can be inspected at any time to diagnose lock contention issues.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Models;

// Create metrics for a specific lock key
var metrics = new ContentionMetrics("my-lock-key");

// Record a waiter entering the queue
metrics.RecordWaiterAdded();

// Simulate a waiter leaving after waiting 120.5 ms
metrics.RecordWaiterRemoved(120.5);

// Record a deadlock detection
metrics.RecordDeadlock();

// Inspect the metrics
Console.WriteLine($"Lock key: {metrics.LockKey}");
Console.WriteLine($"Created at: {metrics.CreatedAt:O}");
Console.WriteLine($"Last updated at: {metrics.LastUpdatedAt:O}");
Console.WriteLine(metrics); // Uses overridden ToString()
```

This example demonstrates how to instantiate the metrics, record waiter activity, detect a deadlock, and output the collected statistics.


## LockRequestContext

The `LockRequestContext` class represents the context and metadata for a lock acquisition request. It tracks request details such as the lock key, requester information, acquisition mode, timing metrics, and outcome status. This context is useful for audit trails, debugging, and monitoring lock acquisition attempts in distributed systems.

### Usage Example

```csharp
using System;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Enums;

// Create a lock request context for a specific resource
var requestContext = new LockRequestContext("user-profile-lock", "user-12345")
{
    RequestorName = "Alice Johnson",
    Mode = AcquisitionMode.Blocking,
    RequestedDuration = TimeSpan.FromMinutes(2),
    RequestedAt = DateTime.UtcNow
};

// Add custom properties for tracking
requestContext.AddProperty("operation", "profile-update");
requestContext.AddProperty("priority", 1);
requestContext.SetCorrelationId(Guid.NewGuid().ToString());
requestContext.SetUserContext("alice.johnson@company.com", "session-789");

// Simulate successful lock acquisition
requestContext.MarkCompleted(true);

// Log the request outcome
Console.WriteLine($"Lock request completed: {requestContext}");
Console.WriteLine($"Duration: {requestContext.Duration.TotalSeconds:F2}s");
Console.WriteLine($"Custom properties: {requestContext.CustomProperties.Count} items");

// For a failed attempt
var failedContext = new LockRequestContext("order-processing-lock", "service-worker")
{
    Mode = AcquisitionMode.NonBlocking,
    RequestedDuration = TimeSpan.FromSeconds(5)
};
failedContext.MarkCompleted(false, "timeout-after-3-retries");
failedContext.IncrementRetryCount();
failedContext.IncrementRetryCount();
```

This example demonstrates how to create a lock request context, track custom properties, set user context, mark completion status, and log the request outcome for both successful and failed lock acquisition attempts.



## LockAcquisition

The `LockAcquisition` class represents a lock acquisition attempt with timing and retry information. It tracks the lock key, requester, acquisition mode, timing metrics, retry behavior, and outcome status. This class is useful for audit trails, debugging lock acquisition attempts, and monitoring retry patterns in distributed systems.

### Usage Example

```csharp
using System;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Enums;

// Create a lock acquisition for a specific resource
var acquisition = new LockAcquisition("user-profile-lock", "user-12345", AcquisitionMode.Blocking, TimeSpan.FromSeconds(10), 3)
{
    Timeout = TimeSpan.FromSeconds(15)
};

// Record the first attempt (failed)
acquisition.RecordAttempt(false, "lock-unavailable", TimeSpan.FromMilliseconds(150));

// Record the second attempt (still failed)
acquisition.RecordAttempt(false, "lock-unavailable", TimeSpan.FromMilliseconds(200));

// Record the third attempt (successful)
acquisition.RecordAttempt(true, null, TimeSpan.FromMilliseconds(250));

// Log acquisition details
Console.WriteLine($"Lock acquisition: {acquisition}");
Console.WriteLine($"Total attempts: {acquisition.AttemptCount}");
Console.WriteLine($"Successful: {acquisition.IsSuccessful}");
Console.WriteLine($"Total elapsed time: {acquisition.TotalElapsedTime.TotalSeconds:F2}s");
Console.WriteLine($"Average attempt time: {acquisition.AverageAttemptTime.TotalMilliseconds:F2}ms");

// For a failed acquisition
var failedAcquisition = new LockAcquisition("order-processing-lock", "service-worker", AcquisitionMode.NonBlocking, TimeSpan.FromSeconds(5), 2)
{
    Timeout = TimeSpan.FromSeconds(8)
};

failedAcquisition.RecordAttempt(false, "timeout-after-3-retries");
failedAcquisition.RecordAttempt(false, "timeout-after-3-retries");
Console.WriteLine($"Failed acquisition: {failedAcquisition}");
```

This example demonstrates how to create a lock acquisition, record multiple attempts with different outcomes, and inspect the acquisition statistics including timing metrics and retry information.


## FencingToken

The `FencingToken` class represents a fencing token that prevents zombie processes from writing to shared resources. Fencing tokens are monotonically increasing and ensure that only the current lock holder can proceed by comparing sequence numbers. Each token contains a unique identifier, a sequence number, and an issuance timestamp.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Models;

// Create a new fencing token
var token = new FencingToken("abc123", 1, DateTime.UtcNow);

// Create a new token with an incremented sequence number
var nextToken = token.IncrementSequence();

// Compare tokens to determine which is newer
if (nextToken.IsGreaterThan(token))
{
    Console.WriteLine($"Newer token: {nextToken}");
}

// Validate token freshness
if (token.IsValid(TimeSpan.FromMinutes(5)))
{
    Console.WriteLine("Token is still valid");
}

// Compare tokens
if (token.CompareTo(nextToken) < 0)
{
    Console.WriteLine("Token has lower sequence number");
}

// Token equality
var sameToken = new FencingToken("abc123", 1, DateTime.UtcNow);
Console.WriteLine($"Tokens are equal: {token.Equals(sameToken)}");
```

This example demonstrates how to create, increment, compare, and validate fencing tokens in a distributed lock scenario.

## LockConfiguration

`LockConfiguration` encapsulates all settings required to acquire and manage a distributed lock. It defines the lock key, duration, acquisition strategy, retry behavior, renewal policy, and optional fencing token usage. The configuration can be validated before use to ensure that all values are within acceptable ranges.

### Usage Example

```csharp
using System;
using System.Linq;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Enums;

var config = new LockConfiguration("my-lock")
{
    LockDuration = TimeSpan.FromSeconds(30),
    AcquisitionTimeout = TimeSpan.FromSeconds(10),
    AcquisitionMode = AcquisitionMode.Blocking,
    MaxRetries = 5,
    RetryInterval = TimeSpan.FromMilliseconds(200),
    RenewalInterval = TimeSpan.FromSeconds(15),
    AutoRenewal = true,
    UseFencingToken = true,
    Metadata = "example metadata"
};

var errors = config.Validate();
if (errors.Any())
{
    Console.WriteLine("Configuration errors:");
    foreach (var e in errors) Console.WriteLine($"- {e}");
}
else
{
    Console.WriteLine("Configuration is valid:");
    Console.WriteLine(config);
}
```

This example shows how to instantiate a `LockConfiguration`, set its properties, validate it, and output the resulting configuration string.