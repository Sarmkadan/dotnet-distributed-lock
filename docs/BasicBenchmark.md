# BasicBenchmark

BasicBenchmark is a helper type used in the dotnet‑distributed‑lock benchmark suite to exercise lock operations against a chosen backend. It exposes configuration properties for the backend type and connection string, lifecycle methods for global setup and cleanup, and a set of asynchronous methods that represent the core lock actions measured by the benchmarks.

## API

### BackendType Property
**Purpose:** Gets or sets the backend technology (e.g., Redis, ZooKeeper) that the lock implementation will use.  
**Parameters:** none  
**Return value:** `BackendType` – the selected backend.  
**Exceptions:** None thrown by the property accessor.

### ConnectionString Property
**Purpose:** Gets or sets the connection string required to reach the lock backend.  
**Parameters:** none  
**Return value:** `string` – the connection string.  
**Exceptions:** None thrown by the property accessor.

### GlobalSetup Method
**Purpose:** Performs one‑time initialization before any benchmark iterations run (e.g., creating a client, ensuring the backend is reachable).  
**Parameters:** none  
**Return value:** `void`  
**Exceptions:** May throw exceptions if setup fails, such as an invalid connection string or backend unavailability.

### GlobalCleanup Method
**Purpose:** Releases resources allocated during `GlobalSetup` after all benchmark iterations have completed.  
**Parameters:** none  
**Return value:** `void`  
**Exceptions:** May throw exceptions if cleanup encounters an error (e.g., failure to close connections).

### AcquireAsync Method
**Purpose:** Asynchronously attempts to acquire a lock.  
**Parameters:** none  
**Return value:** `Task` – completes when the acquire operation finishes.  
**Exceptions:** May throw `OperationCanceledException` if the operation is cancelled, or backend‑specific exceptions (e.g., `TimeoutException`, `InvalidOperationException`) on failure to acquire the lock.

### TryAcquireAsync_Success Method
**Purpose:** Asynchronously attempts to acquire a lock where success is expected; used to measure latency of a successful try‑acquire.  
**Parameters:** none  
**Return value:** `Task` – completes when the try‑acquire operation finishes.  
**Exceptions:** May throw exceptions if the attempt unexpectedly fails (indicating a benchmark mis‑configuration).

### TryAcquireAsync_Failure Method
**Purpose:** Asynchronously attempts to acquire a lock where failure is expected; used to measure latency of a failed try‑acquire.  
**Parameters:** none  
**Return value:** `Task` – completes when the try‑acquire operation finishes.  
**Exceptions:** May throw exceptions:** May throw exceptions if the attempt unexpectedly succeeds (indicating a benchmark mis‑configuration).

### ReleaseAsync Method
**Purpose:** Asynchronously releases a lock that was previously acquired.  
**Parameters:** none  
**Return value:** `Task` – completes when the release operation finishes.  
**Exceptions:** May throw exceptions if the lock is not held by the caller or if the backend reports an error.

### RenewAsync Method
**Purpose:** Asynchronously renews the lease of an existing lock.  
**Parameters:** none  
**Return value:** `Task` – completes when the renew operation finishes.  
**Exceptions:** May throw exceptions if the lock cannot be renewed (e.g., lease expired, backend error).

### IsLockedAsync Method
**Purpose:** Asynchronously checks whether a lock is currently held.  
**Parameters:** none  
**Return value:** `Task` – completes when the check finishes.  
**Exceptions:** May throw exceptions on backend communication failures.

### GetLockAsync Method
**Purpose:** Asynchronously retrieves lock metadata or the lock object itself from the backend.  
**Parameters:** none  
**Return value:** `Task` – completes when the retrieval finishes.  
**Exceptions:** May throw if the lock cannot be retrieved.  
**Exceptions:** May throw exceptions if the lock cannot be retrieved (e.g., not found, backend error).

## Usage

```csharp
var bench = new BasicBenchmark
{
    BackendType = BackendType.Redis,
    ConnectionString = "localhost:6379"
};

bench.GlobalSetup
{
    BackendType = BackendType.Redis,
    ConnectionString = "localhost:6379"
};

bench.GlobalSetup();
// Example benchmark iteration: acquire then release
await bench.AcquireAsync();
await bench.ReleaseAsync();
bench.GlobalCleanup();
```

```csharp
var bench = new BasicBenchmark
{
    BackendType = BackendType.ZooKeeper,
    ConnectionString = "zk1:2181:zk2:2181"
};

bench.GlobalSetup();

// Measure successful try‑acquire that should succeed
await bench.TryAcquireAsync_Success();
await bench.ReleaseAsync();

// Measure try‑acquire that is expected to fail (lock held elsewhere)
await bench.TryAcquireAsync_Failure();

bench.GlobalCleanup();
```

## Notes

- The type does not implement its own thread‑safety; callers must synchronize concurrent invocations if the underlying lock backend is not thread‑safe.
- `BackendType` and `ConnectionString` should be configured before calling `GlobalSetup`; changing them after setup may lead to undefined behavior.
- All asynchronous `Task` that signals completion; they do not return a result value. Success or failure is conveyed by whether the method throws an exception.
- `TryAcquireAsync_Success` and `TryAcquireAsync_Failure` are named to reflect the expected benchmark outcome; they do not guarantee that outcome. Using them incorrectly will result in an exception, which the benchmark harness should treat as a failed iteration.
- If any method throws, the benchmark iteration should be considered failed, and the harness should proceed to `GlobalCleanup`.
- No implicit disposal of resources is performed; `GlobalCleanup` is responsible for releasing any backend connections or temporary state created during `GlobalSetup`.
