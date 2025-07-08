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