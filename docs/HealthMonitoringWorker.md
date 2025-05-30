# HealthMonitoringWorker

A background worker that periodically checks the health of a distributed lock backend and exposes its status through public properties. It is designed to run asynchronously within a hosted service context, allowing applications to monitor connectivity and failure conditions to distributed resources.

## API

### `HealthMonitoringWorker`
A background service component that performs periodic health checks against a distributed lock backend. The worker starts checking immediately upon initialization and continues at the configured interval until stopped.

### `HealthStatus GetStatus()`
Returns the current health status of the backend.
- **Returns**: `HealthStatus` indicating whether the backend is healthy, degraded, or failed.
- **Throws**: `InvalidOperationException` if the worker has not been started or has been disposed.

### `void ResetFailureCounter()`
Resets the consecutive failure counter to zero and clears the last error message. Useful for recovering from transient failures without waiting for the next check cycle.

### `public override async Task StopAsync(CancellationToken cancellationToken)`
Stops the health monitoring worker and releases any resources.
- **Parameters**:
  - `cancellationToken`: A token to observe for cancellation requests.
- **Returns**: A `Task` representing the asynchronous operation.

### `public bool IsHealthy`
Gets a value indicating whether the backend is currently considered healthy based on the failure threshold and last check result.
- **Returns**: `true` if the backend is healthy; otherwise, `false`.

### `public bool BackendConnected`
Gets a value indicating whether the backend is currently reachable and responsive.
- **Returns**: `true` if the backend responded to the last health check; otherwise, `false`.

### `public DateTime LastCheckTime`
Gets the timestamp of the most recent health check.
- **Returns**: A `DateTime` representing when the last check was completed.

### `public long CheckDurationMs`
Gets the duration of the last health check in milliseconds.
- **Returns**: A `long` value indicating how long the last check took to complete.

### `public int ConsecutiveFailures`
Gets the number of consecutive failed health checks.
- **Returns**: An `int` representing the current count of consecutive failures.

### `public string? LastErrorMessage`
Gets the error message from the last failed health check, if any.
- **Returns**: A `string` containing the last error message, or `null` if no error occurred.

### `public int CheckIntervalMs`
Gets or sets the interval between health checks in milliseconds.
- **Value**: An `int` representing the interval in milliseconds.
- **Throws**: `ArgumentOutOfRangeException` if the value is less than or equal to zero.

### `public int FailureThreshold`
Gets or sets the number of consecutive failures that must occur before the backend is considered unhealthy.
- **Value**: An `int` representing the threshold.
- **Throws**: `ArgumentOutOfRangeException` if the value is less than zero.

### `public bool AlertOnUnhealthy`
Gets or sets a value indicating whether to trigger alerts when the backend becomes unhealthy.
- **Value**: `true` to enable alerts; otherwise, `false`.

### `public TimeSpan CheckTimeout`
Gets or sets the maximum duration allowed for a single health check operation.
- **Value**: A `TimeSpan` representing the timeout.
- **Throws**: `ArgumentOutOfRangeException` if the value is less than or equal to `TimeSpan.Zero`.

## Usage

### Example 1: Basic Monitoring Setup
