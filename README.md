// existing content ...

## ILockEventBus

The `ILockEventBus` interface provides a centralized event bus for distributed lock events, enabling decoupled communication between components in a distributed lock system. It supports both synchronous and asynchronous event handlers, with built-in event history and subscriber tracking capabilities. The interface is implemented by `InMemoryLockEventBus`, which provides an in-memory event bus with optional event history replay.

### Usage Example

```csharp
// Register the event bus in DI
services.AddLockEventBus(maxHistorySize: 5000);

// Resolve the event bus
var eventBus = serviceProvider.GetRequiredService<ILockEventBus>();

// Define a custom event
public class LockAcquiredEvent : LockEvent
{
    public string ResourceId { get; set; }
    public string OwnerId { get; set; }
    public TimeSpan LockDuration { get; set; }
}

// Subscribe to events synchronously
var acquiredHandler = new Action<LockAcquiredEvent>(e => 
{
    Console.WriteLine($"Lock acquired: {e.ResourceId} by {e.OwnerId}");
});
eventBus.Subscribe(acquiredHandler);

// Subscribe to events asynchronously
var releasedHandler = new Func<LockReleasedEvent, Task>(async e => 
{
    await Task.Delay(100); // Simulate async work
    Console.WriteLine($"Lock released: {e.ResourceId}");
});
eventBus.Subscribe(releasedHandler);

// Publish an event
var lockAcquired = new LockAcquiredEvent
{
    ResourceId = "order-processing-123",
    OwnerId = "payment-service-01",
    LockDuration = TimeSpan.FromMinutes(5),
    CorrelationId = Guid.NewGuid().ToString()
};

// Synchronous publish
eventBus.Publish(lockAcquired);

// Asynchronous publish
await eventBus.PublishAsync(lockAcquired);

// Fire-and-forget publish
eventBus.PublishFireAndForget(lockAcquired);

// Get subscriber count
var subscriberCount = eventBus.GetSubscriberCount<LockAcquiredEvent>();
Console.WriteLine($"Subscribers for LockAcquiredEvent: {subscriberCount}");

// Get event history
var recentEvents = eventBus.GetEventHistory<LockAcquiredEvent>();
Console.WriteLine($"Total LockAcquiredEvent events: {recentEvents.Count}");
```

## LockEventSubscriber

The `LockEventSubscriber` class is an abstract base class that provides a common interface for subscribing to lock events. It allows developers to create custom event subscribers that can handle various lock events, such as lock acquisition, release, expiration, and contention.

### Usage Example

```csharp
var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<LoggingLockEventSubscriber>();
var subscriber = new LoggingLockEventSubscriber(logger);

await subscriber.RegisterAsync(publisher);

// Get metrics from the subscriber
var metrics = subscriber.GetMetrics();
Console.WriteLine($"Acquisitions: {metrics.Acquisitions}, Releases: {metrics.Releases}, Failures: {metrics.Failures}, ContentionEvents: {metrics.ContentionEvents}");

// Initialize all registered event subscribers
await InitializeLockEventSubscribersAsync(serviceProvider);
```

## BasicBenchmark

The `BasicBenchmark` class provides a set of basic benchmarks for measuring the performance of lock acquisition and release operations across different backends. It tests various scenarios such as acquiring a lock, trying to acquire a lock with success, trying to acquire a lock with failure, releasing a lock, renewing a lock, checking if a lock is held, and retrieving lock information.

### Usage Example

```csharp
var benchmark = new BasicBenchmark
{
    BackendType = BackendType.Redis,
    ConnectionString = "redis://localhost:6379,allowAdmin=true"
};

benchmark.GlobalSetup();

try
{
    await benchmark.AcquireAsync();
    await benchmark.TryAcquireAsync_Success();
    await benchmark.TryAcquireAsync_Failure();
    await benchmark.ReleaseAsync();
    await benchmark.RenewAsync();
    await benchmark.IsLockedAsync();
    await benchmark.GetLockAsync();
}
finally
{
    benchmark.GlobalCleanup();
}
```

## ContentionBenchmark

The `ContentionBenchmark` class provides performance benchmarks for measuring lock acquisition under contention scenarios. It tests scenarios such as single lock acquisition under high contention, sequential acquisitions with the same key, checking if a lock is held, and retrieving lock information.

### Usage Example

```csharp
var benchmark = new ContentionBenchmark
{
    BackendType = BackendType.Redis,
    ConnectionString = "redis://localhost:6379,allowAdmin=true"
};

benchmark.GlobalSetup();

try
{
    await benchmark.HighContention_Acquire();
    await benchmark.Sequential_Acquisitions_Same_Key();
    await benchmark.IsLocked_Operation();
    await benchmark.GetLock_Information();
}
finally
{
    benchmark.GlobalCleanup();
}
```

## ThroughputBenchmark

The `ThroughputBenchmark` class measures the performance of lock operations under various workloads. It provides methods to acquire and release locks sequentially, concurrently, and to renew locks repeatedly, allowing developers to evaluate throughput and scalability of their distributed lock implementation.

### Usage Example

```csharp
using System;
using System.Threading.Tasks;
using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Benchmarks.Benchmarks;

class Program
{
    static async Task Main()
    {
        var benchmark = new ThroughputBenchmark
        {
            BackendType = BackendType.Redis,
            ConnectionString = "redis://localhost:6379,allowAdmin=true"
        };

        benchmark.GlobalSetup();

        await benchmark.Acquire_1000_Locks();
        await benchmark.Acquire_100_Locks_Concurrently();
        await benchmark.Acquire_And_Release_1000_Times();
        await benchmark.Renew_Lock_100_Times();

        benchmark.GlobalCleanup();
    }
}
```

## FencingTokenBenchmark

The `FencingTokenBenchmark` class provides performance benchmarks for fencing token operations, which are used to prevent split-brain scenarios in distributed systems. It measures the performance of issuing tokens, validating tokens, and checking if resources are locked.

### Usage Example

```csharp
using System;
using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Benchmarks.Benchmarks;

class Program
{
    static void Main()
    {
        var benchmark = new FencingTokenBenchmark
        {
            BackendType = BackendType.Redis,
            ConnectionString = "redis://localhost:6379,allowAdmin=true"
        };

        benchmark.GlobalSetup();

        try
        {
            benchmark.IssueToken();
            benchmark.ValidateToken_Valid();
            benchmark.ValidateToken_Invalid();
            benchmark.IsResourceLocked();
        }
        finally
        {
            benchmark.GlobalCleanup();
        }
    }
}
```

