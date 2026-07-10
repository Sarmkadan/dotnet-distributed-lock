# ContentionBenchmark

`ContentionBenchmark` is a performance benchmarking class designed to evaluate the behavior and throughput of distributed lock implementations under various contention scenarios. It measures the efficiency of acquiring, querying, and managing locks across different backend types (e.g., SQL Server, Redis) by simulating high-contention workloads, sequential acquisitions, and lock state inspections.

## API

### `public BackendType BackendType`
- **Purpose**: Specifies the distributed lock backend type under test (e.g., `SqlServer`, `Redis`).
- **Value**: A `BackendType` enum value representing the active backend.
- **Notes**: This property is typically set during benchmark configuration and influences connection string interpretation.

### `public string ConnectionString`
- **Purpose**: The connection string used to establish a connection to the distributed lock backend.
- **Value**: A non-null, non-empty string representing the backend-specific connection details.
- **Notes**: The format and requirements of this string depend on the `BackendType`. Invalid or malformed connection strings may cause benchmark failures.

### `public void GlobalSetup()`
- **Purpose**: Performs one-time initialization required before running any benchmarks, such as establishing backend connections or creating test resources.
- **Parameters**: None.
- **Return Value**: None.
- **Exceptions**: May throw `InvalidOperationException` if initialization fails (e.g., connection errors, missing dependencies).
- **Notes**: Called once per benchmark run, prior to any benchmark methods.

### `public void GlobalCleanup()`
- **Purpose**: Releases resources acquired during `GlobalSetup`, such as closing connections or cleaning up test artifacts.
- **Parameters**: None.
- **Return Value**: None.
- **Exceptions**: May throw exceptions if cleanup fails (e.g., backend errors), but exceptions are typically suppressed to avoid masking benchmark results.
- **Notes**: Called once after all benchmarks complete, regardless of success or failure.

### `public async Task HighContention_Acquire()`
- **Purpose**: Measures the performance of acquiring a distributed lock under high contention, where multiple threads/processes compete for the same lock.
- **Parameters**: None.
- **Return Value**: A `Task` representing the asynchronous operation.
- **Exceptions**: May throw `DistributedLockException` if lock acquisition fails due to backend errors or contention limits.
- **Notes**: Simulates a worst-case scenario for lock acquisition throughput. The lock key and contention parameters are typically hardcoded or configured elsewhere.

### `public async Task Sequential_Acquisitions_Same_Key()`
- **Purpose**: Evaluates the performance of acquiring and releasing the same lock sequentially, measuring overhead and latency for repeated operations.
- **Parameters**: None.
- **Return Value**: A `Task` representing the asynchronous operation.
- **Exceptions**: May throw `DistributedLockException` if sequential acquisitions fail (e.g., lock not released properly).
- **Notes**: Useful for assessing the baseline performance of a lock implementation without contention.

### `public async Task IsLocked_Operation()`
- **Purpose**: Tests the performance of querying whether a lock is currently held, simulating scenarios where lock state inspection is frequent.
- **Parameters**: None.
- **Return Value**: A `Task` representing the asynchronous operation.
- **Exceptions**: May throw `DistributedLockException` if the backend does not support lock state queries or encounters errors.
- **Notes**: Some backends may not support this operation efficiently, leading to higher latency.

### `public async Task GetLock_Information()`
- **Purpose**: Retrieves metadata about a held lock (e.g., owner, expiration, acquisition time) and measures the performance of this operation.
- **Parameters**: None.
- **Return Value**: A `Task` representing the asynchronous operation.
- **Exceptions**: May throw `DistributedLockException` if the backend does not support lock information retrieval or encounters errors.
- **Notes**: Useful for debugging or monitoring, but may incur significant overhead depending on the backend.

## Usage

### Example 1: Benchmarking SQL Server Distributed Locks
