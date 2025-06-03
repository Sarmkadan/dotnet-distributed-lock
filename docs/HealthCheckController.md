# HealthCheckController

Provides endpoints for application health monitoring, including liveness and readiness checks, as well as detailed health diagnostics. Designed for integration with orchestration systems and load balancers to determine service availability and operational state.

## API

### `public HealthCheckController`

Constructor for the health check controller. Initializes a new instance of the `HealthCheckController` class.

### `public ActionResult<HealthCheckResponse> Liveness()`

Performs a basic liveness check to determine if the application is running. This endpoint should always respond quickly and does not perform any external dependencies checks.

- **Returns**: `ActionResult<HealthCheckResponse>`
  - `200 OK` with a `HealthCheckResponse` containing `Status = "Healthy"` if the application is running.
  - `503 Service Unavailable` with a `HealthCheckResponse` containing `Status = "Unhealthy"` if the application is not running.
- **Throws**: No exceptions are thrown by this method.

### `public async Task<ActionResult<HealthCheckResponse>> Readiness()`

Performs a readiness check to determine if the application is ready to serve traffic. This endpoint verifies that all critical dependencies (e.g., databases, caches) are available.

- **Returns**: `Task<ActionResult<HealthCheckResponse>>`
  - `200 OK` with a `HealthCheckResponse` containing `Status = "Healthy"` if all dependencies are available.
  - `503 Service Unavailable` with a `HealthCheckResponse` containing `Status = "Unhealthy"` if any dependency is unavailable.
- **Throws**: No exceptions are thrown by this method. Any dependency failures are reflected in the response status.

### `public async Task<ActionResult<DetailedHealthResponse>> DetailedHealth()`

Performs a comprehensive health check, including runtime metrics, dependency status, and system information. Useful for diagnostics and monitoring.

- **Returns**: `Task<ActionResult<DetailedHealthResponse>>`
  - `200 OK` with a `DetailedHealthResponse` containing system metrics and dependency status if all checks pass.
  - `503 Service Unavailable` with a `DetailedHealthResponse` containing error details if any check fails.
- **Throws**: No exceptions are thrown by this method. Failures are reported in the response.

### `public required string Status`

Gets the health status of the application or check. Possible values are `"Healthy"` or `"Unhealthy"`.

- **Type**: `string`
- **Access**: Read-only

### `public DateTime Timestamp`

Gets the timestamp when the health check was performed.

- **Type**: `DateTime`
- **Access**: Read-only

### `public string Version`

Gets the application version associated with the health check response.

- **Type**: `string`
- **Access**: Read-only

### `public long ResponseTimeMs`

Gets the response time of the health check in milliseconds.

- **Type**: `long`
- **Access**: Read-only

### `public RuntimeInfo? Runtime`

Gets runtime information such as framework and uptime.

- **Type**: `RuntimeInfo?`
- **Access**: Read-only

### `public string Framework`

Gets the .NET runtime framework version.

- **Type**: `string`
- **Access**: Read-only

### `public TimeSpan Uptime`

Gets the application uptime.

- **Type**: `TimeSpan`
- **Access**: Read-only

### `public HealthDetails? Details`

Gets detailed health information, including backend connectivity and error messages.

- **Type**: `HealthDetails?`
- **Access**: Read-only

### `public bool BackendConnected`

Gets a value indicating whether the backend (e.g., database) is connected.

- **Type**: `bool`
- **Access**: Read-only

### `public string? ErrorMessage`

Gets an error message if the health check failed.

- **Type**: `string?`
- **Access**: Read-only
