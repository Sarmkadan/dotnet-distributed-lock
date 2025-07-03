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

// existing content ...
