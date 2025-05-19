# ThroughputBenchmark

`ThroughputBenchmark` is a BenchmarkDotNet-based performance test class that measures the raw throughput and concurrency characteristics of distributed lock backends. It provides standardized benchmarks for acquiring locks in bulk, acquiring locks concurrently from multiple threads, acquiring and immediately releasing locks in a tight loop, and renewing existing locks repeatedly. The class targets the `dotnet-distributed-lock` library and supports configurable backend selection via `BackendType` and `ConnectionString`.

## API

### `public BackendType BackendType`

**Purpose:** Specifies which distributed lock backend implementation the benchmark should exercise. The value is set before any benchmark method runs, typically through BenchmarkDotNet parameters or manual assignment in setup.

**Type:** `BackendType` (enum defined in the project).

**Remarks:** Changing this property after `GlobalSetup` has executed has no effect until `GlobalSetup` is called again. The property is read by `GlobalSetup` to instantiate the correct lock provider.

### `public string ConnectionString`

**Purpose:** Holds the connection string or endpoint address used to reach the selected backend (e.g., Redis connection string, Azure Storage account connection string, or database connection string). Must be valid for the chosen `BackendType`.

**Type:** `string`.

**Remarks:** If the string is null, empty, or malformed for the selected backend, `GlobalSetup` will throw an exception. This property is intended to be populated by BenchmarkDotNet configuration or test infrastructure before the benchmark session begins.

### `public void GlobalSetup()`

**Purpose:** Initializes the distributed lock infrastructure once before any benchmark iteration runs. This method is decorated with BenchmarkDotNet’s `[GlobalSetup]` attribute and is called automatically by the benchmark runner.

**Parameters:** None.

**Return value:** `void`.

**Throws:** May throw if `ConnectionString` is invalid for the selected `BackendType`, if the backend is unreachable, or if backend-specific initialization fails (e.g., authentication errors, missing resources).

**Remarks:** This method creates the lock provider instance and performs any one-time resource allocation required by the backend. It is not thread-safe by itself; BenchmarkDotNet guarantees it runs exactly once per benchmark run, before any measured iterations.

### `public void GlobalCleanup()`

**Purpose:** Releases resources allocated during `GlobalSetup` after all benchmark iterations complete. Decorated with `[GlobalCleanup]`.

**Parameters:** None.

**Return value:** `void`.

**Throws:** Typically does not throw; exceptions during cleanup are usually logged or silently absorbed depending on the backend implementation.

**Remarks:** Ensures connections are closed and temporary resources are disposed. Called once per benchmark run.

### `public async Task Acquire_1000_Locks()`

**Purpose:** Benchmarks the raw throughput of acquiring 1000 distinct distributed locks sequentially. Each lock is acquired with a unique key and held for the duration of the benchmark method.

**Parameters:** None.

**Return value:** `Task` representing the asynchronous operation.

**Throws:** May throw if the backend is unavailable, if a lock acquisition times out, or if the maximum number of locks is exceeded for the backend.

**Remarks:** This method measures the cost of lock acquisition alone. Locks are not explicitly released within the method; they are typically released during cleanup or when the backend session expires. The method is executed multiple times by BenchmarkDotNet to produce statistically stable results.

### `public async Task Acquire_100_Locks_Concurrently()`

**Purpose:** Measures throughput when 100 locks are acquired concurrently from multiple tasks. Each task acquires a lock with a distinct key, and the method waits for all acquisitions to complete.

**Parameters:** None.

**Return value:** `Task` representing the asynchronous operation.

**Throws:** May throw aggregate exceptions if any concurrent acquisition fails due to backend contention, throttling, or timeout.

**Remarks:** This benchmark stresses the backend’s ability to handle parallel lock requests. The degree of parallelism is fixed at 100 tasks. Results reflect both backend concurrency limits and client-side task scheduling overhead.

### `public async Task Acquire_And_Release_1000_Times()`

**Purpose:** Benchmarks the complete acquire-release lifecycle by acquiring a lock and immediately releasing it, repeated 1000 times sequentially using the same lock key.

**Parameters:** None.

**Return value:** `Task` representing the asynchronous operation.

**Throws:** May throw if any single acquire or release operation fails, including network interruptions or backend-specific consistency violations.

**Remarks:** This method measures end-to-end latency for a single lock/unlock cycle. Because the same key is reused, the backend must handle repeated lock ownership transitions efficiently. The lock is released before the next acquisition begins.

### `public async Task Renew_Lock_100_Times()`

**Purpose:** Benchmarks the cost of extending an existing lock’s lease. A single lock is acquired first, then its lease is renewed 100 times sequentially without releasing the lock between renewals.

**Parameters:** None.

**Return value:** `Task` representing the asynchronous operation.

**Throws:** May throw if the initial acquisition fails, if any renewal fails (e.g., the lock was lost due to expiration or backend eviction), or if the backend does not support renewal.

**Remarks:** This benchmark isolates the renewal path. The lock is held continuously throughout the method. If the backend’s lock TTL is shorter than the total benchmark duration, renewals must succeed fast enough to prevent expiration.

## Usage

### Example 1: Running benchmarks programmatically with BenchmarkDotNet

```csharp
using BenchmarkDotNet.Running;
using dotnet_distributed_lock;

public static class Program
{
    public static void Main(string[] args)
    {
        // Configure the benchmark class via BenchmarkDotNet parameters
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator)
            .AddJob(Job.Default
                .WithWarmupCount(3)
                .WithIterationCount(10));

        // Parameters are set through BenchmarkDotNet's [Params] or [Arguments]
        // If ThroughputBenchmark exposes them as fields/properties with [Params],
        // BenchmarkDotNet will populate them automatically.
        BenchmarkRunner.Run<ThroughputBenchmark>(config);
    }
}
```

### Example 2: Manual invocation for ad-hoc testing

```csharp
using dotnet_distributed_lock;

public async Task RunManualThroughputTest()
{
    var benchmark = new ThroughputBenchmark
    {
        BackendType = BackendType.Redis,
        ConnectionString = "localhost:6379,ssl=false"
    };

    benchmark.GlobalSetup();

    try
    {
        // Measure acquire-only throughput
        await benchmark.Acquire_1000_Locks();

        // Measure concurrent acquisition
        await benchmark.Acquire_100_Locks_Concurrently();

        // Measure acquire-release cycle
        await benchmark.Acquire_And_Release_1000_Times();

        // Measure renewal throughput
        await benchmark.Renew_Lock_100_Times();
    }
    finally
    {
        benchmark.GlobalCleanup();
    }
}
```

## Notes

- **Thread safety:** The benchmark methods are designed to be called by BenchmarkDotNet’s harness, which guarantees sequential execution of iterations for a given benchmark method. They are not safe for concurrent invocation from user code outside that harness. `GlobalSetup` and `GlobalCleanup` are called once per run and are not thread-safe relative to each other or to the benchmark methods.
- **State leakage:** `Acquire_1000_Locks` does not release locks within the method. If the backend enforces strict per-client lock limits, subsequent benchmarks in the same run may fail unless `GlobalCleanup` or backend-side expiration clears them. Ensure the backend’s lock TTL is short enough or that `GlobalCleanup` properly disposes all held locks.
- **Connection string validation:** `GlobalSetup` performs no client-side validation of the connection string format before passing it to the backend provider. Invalid strings cause exceptions during provider initialization, not during property assignment.
- **Backend compatibility:** Not all backends support lock renewal. If `Renew_Lock_100_Times` is invoked against a backend that lacks renewal semantics, it will throw at the first renewal attempt. Always verify backend capabilities before including this benchmark in a comparison suite.
- **Concurrency and throttling:** `Acquire_100_Locks_Concurrently` issues 100 parallel tasks. Backends with connection pooling limits or rate throttling may exhibit queueing delays that skew results. Interpret concurrent benchmark results in the context of the backend’s documented concurrency limits.
- **Resource cleanup:** `GlobalCleanup` is best-effort. Network failures during cleanup may leave orphaned locks or connections on the backend. Production-grade runners should supplement with backend-side TTLs and monitoring.
