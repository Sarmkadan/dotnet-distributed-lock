// existing content ...

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

