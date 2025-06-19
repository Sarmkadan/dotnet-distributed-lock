# HealthCheckControllerExtensions

The `HealthCheckControllerExtensions` class provides a set of static helper methods designed to streamline the creation of standardized health check responses within ASP.NET Core controllers. By encapsulating the logic for generating `HealthCheckResponse` and `DetailedHealthResponse` objects along with status evaluation utilities, this extension ensures consistent formatting and status code handling across distributed lock monitoring endpoints without requiring repetitive boilerplate code in individual controller actions.

## API

### `CreateHealthResponse`
Generates a standard health check result wrapped in an `ActionResult`.
- **Purpose**: Constructs a basic health response indicating the overall system status.
- **Parameters**: This method accepts no parameters; it infers the current health status from the application's registered health checks or default context.
- **Return Value**: Returns an `ActionResult<HealthCheckResponse>` containing the serialized health status and an appropriate HTTP status code (typically 200 OK or 503 Service Unavailable).
- **Exceptions**: Throws no specific exceptions under normal operation; may throw standard ASP.NET Core runtime exceptions if the underlying health check service is misconfigured.

### `CreateDetailedResponse`
Generates a comprehensive health check result including granular component details.
- **Purpose**: Constructs a detailed response payload that includes individual status information for specific dependencies or subsystems managed by the distributed lock infrastructure.
- **Parameters**: This method accepts no parameters; it aggregates details from all registered detailed health providers.
- **Return Value**: Returns an `ActionResult<DetailedHealthResponse>` containing the full diagnostic data and the corresponding HTTP status code.
- **Exceptions**: Throws no specific exceptions; relies on the stability of the underlying health check registrations.

### `IsHealthy`
Evaluates the current health state of the application.
- **Purpose**: Provides a boolean assertion to determine if the application is currently considered healthy.
- **Parameters**: None.
- **Return Value**: Returns `true` if the system is healthy; otherwise, returns `false`.
- **Exceptions**: Throws no exceptions.

### `IsUnhealthy`
Evaluates the current health state of the application for negative conditions.
- **Purpose**: Provides a boolean assertion to determine if the application is currently considered unhealthy.
- **Parameters**: None.
- **Return Value**: Returns `true` if the system is unhealthy; otherwise, returns `false`.
- **Exceptions**: Throws no exceptions.

## Usage

### Generating a Standard Health Endpoint
The following example demonstrates how to expose a basic health check endpoint in a controller using the extension method.

```csharp
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public ActionResult<HealthCheckResponse> Get()
    {
        // Returns a standardized response with HTTP 200 or 503 based on status
        return HealthCheckControllerExtensions.CreateHealthResponse();
    }
}
```

### Conditional Logic Based on Health Status
This example illustrates using the boolean helpers to execute custom logic or logging before returning a detailed diagnostic report.

```csharp
[ApiController]
[Route("api/[controller]")]
public class DiagnosticController : ControllerBase
{
    [HttpGet("detailed")]
    public ActionResult<DetailedHealthResponse> GetDetailed()
    {
        if (HealthCheckControllerExtensions.IsUnhealthy())
        {
            // Insert custom alerting or logging logic here
            Logger.LogWarning("Distributed lock health check failed.");
        }

        // Returns the full detailed payload
        return HealthCheckControllerExtensions.CreateDetailedResponse();
    }
}
```

## Notes

- **Thread Safety**: As all members are static and operate on stateless logic or thread-safe underlying health check services, these methods are safe for concurrent calls from multiple threads without external synchronization.
- **State Dependency**: The boolean methods `IsHealthy` and `IsUnhealthy` are mutually exclusive reflections of the same underlying state at the moment of invocation. In highly volatile environments, the state could theoretically change between a call to `IsHealthy` and a subsequent call to `CreateHealthResponse`, though such race conditions are typically negligible within the lifecycle of a single HTTP request.
- **Response Consistency**: The `CreateHealthResponse` and `CreateDetailedResponse` methods automatically determine the HTTP status code based on the evaluated health state. Consumers should not manually set the status code on the returned `ActionResult` unless overriding the default behavior is explicitly required.
- **Null Handling**: These extensions assume a valid health check context is registered in the application service container. If the health check system is completely uninitialized, the behavior defaults to the standard ASP.NET Core health check failure modes.
