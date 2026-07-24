# MetricsController

Provides endpoints and metrics tracking for distributed lock operations, including system-level, lock-specific, and performance-related telemetry. The controller aggregates operational data such as acquisition attempts, success rates, hold times, and active lock counts to support monitoring and diagnostics in distributed environments.

## API

### `MetricsController`
Public constructor. Initializes a new instance of the `MetricsController` with default or injected dependencies for tracking distributed lock metrics.

### `ActionResult<SystemMetricsResponse> GetSystemMetrics()`
Returns high-level system metrics aggregated across all lock operations.

- **Returns**: `ActionResult<SystemMetricsResponse>` – A response containing aggregated system-wide metrics such as total operations, success rates, average hold times, and active lock counts.
- **Throws**: May throw if internal metric aggregation fails due to concurrent access or data corruption.

### `ActionResult<LockMetricsResponse> GetLockMetrics(string lockName)`
Returns metrics specific to a single lock identified by `lockName`.

- **Parameters**:
  - `lockName` (`string`, required) – The name of the lock to query.
- **Returns**: `ActionResult<LockMetricsResponse>` – A response containing per-lock metrics including acquisition attempts, success counts, failure counts, success rate, average and maximum hold times.
- **Throws**: Throws if `lockName` is null or empty, or if the lock has no recorded metrics.

### `ActionResult<PerformanceMetricsResponse> GetPerformanceMetrics()`
Returns performance-related telemetry across all locks, such as throughput and latency distributions.

- **Returns**: `ActionResult<PerformanceMetricsResponse>` – A response containing performance indicators such as operations per second, latency percentiles, and resource utilization.
- **Throws**: May throw under high contention or if performance counters are unavailable.

Metrics are written exclusively by `MetricsTrackingEventSubscriber` as lock events occur, through the
shared `IMetricsStore`. There is no HTTP endpoint for submitting metrics; the controller only reads
from the store, so it cannot be used to inject arbitrary figures.

### `ActionResult ResetMetrics()`
Resets all tracked metrics to zero, clearing historical data.

- **Returns**: `ActionResult` – HTTP 200 on success; 403 if not authorized; 500 if reset fails.
- **Throws**: No expected exceptions under normal operation.

### `TotalLockOperations` (property)
Gets the total number of lock acquisition attempts across all locks.

- **Type**: `long`
- **Access**: Read-only
- **Thread Safety**: Safe for concurrent reads; writes are synchronized internally.

### `SuccessfulAcquisitions` (property)
Gets the total number of successful lock acquisitions.

- **Type**: `long`
- **Access**: Read-only
- **Thread Safety**: Safe for concurrent reads; writes are synchronized internally.

### `FailedAcquisitions` (property)
Gets the total number of failed lock acquisition attempts.

- **Type**: `long`
- **Access**: Read-only
- **Thread Safety**: Safe for concurrent reads; writes are synchronized internally.

### `SuccessRate` (property)
Gets the ratio of successful acquisitions to total attempts, expressed as a value between 0.0 and 1.0.

- **Type**: `double`
- **Access**: Read-only
- **Thread Safety**: Safe for concurrent reads; writes are synchronized internally.

### `AverageHoldTimeMs` (property)
Gets the average time (in milliseconds) that locks are held before release.

- **Type**: `double`
- **Access**: Read-only
- **Thread Safety**: Safe for concurrent reads; writes are synchronized internally.

### `ActiveLocks` (property)
Gets the current number of locks that are actively held (i.e., acquired and not yet released).

- **Type**: `int`
- **Access**: Read-only
- **Thread Safety**: Safe for concurrent reads; writes are synchronized internally.

### `Timestamp` (property)
Gets the UTC timestamp when the current metrics snapshot was recorded.

- **Type**: `DateTime`
- **Access**: Read-only
- **Thread Safety**: Safe for concurrent reads; writes are synchronized internally.

### `LockName` (property)
Gets or sets the name of the lock this instance is tracking. Must be non-null and non-empty.

- **Type**: `string`, required
- **Access**: Read-write
- **Thread Safety**: Safe for concurrent reads; writes are synchronized internally.

### `AcquisitionAttempts` (property)
Gets the total number of acquisition attempts for this specific lock.

- **Type**: `long`
- **Access**: Read-only
- **Thread Safety**: Safe for concurrent reads; writes are synchronized internally.

### `SuccessfulAcquisitions` (property)
Gets the total number of successful acquisitions for this specific lock.

- **Type**: `long`
- **Access**: Read-only
- **Thread Safety**: Safe for concurrent reads; writes are synchronized internally.

### `FailedAcquisitions` (property)
Gets the total number of failed acquisition attempts for this specific lock.

- **Type**: `long`
- **Access**: Read-only
- **Thread Safety**: Safe for concurrent reads; writes are synchronized internally.

### `SuccessRate` (property)
Gets the success rate (0.0 to 1.0) for this specific lock.

- **Type**: `double`
- **Access**: Read-only
- **Thread Safety**: Safe for concurrent reads; writes are synchronized internally.

### `AverageHoldTimeMs` (property)
Gets the average time (in milliseconds) this lock is held before release.

- **Type**: `double`
- **Access**: Read-only
- **Thread Safety**: Safe for concurrent reads; writes are synchronized internally.

### `MaxHoldTimeMs` (property)
Gets the maximum time (in milliseconds) this lock has ever been held.

- **Type**: `long`
- **Access**: Read-only
- **Thread Safety**: Safe for concurrent reads; writes are synchronized internally.

## Usage
