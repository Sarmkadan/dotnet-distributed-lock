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