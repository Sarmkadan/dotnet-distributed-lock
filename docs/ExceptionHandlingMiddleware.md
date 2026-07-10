# ExceptionHandlingMiddleware

Middleware that catches unhandled exceptions occurring downstream in the ASP.NET Core pipeline and writes a standardized JSON error response containing a message, an error code, and a timestamp.

## API

### ExceptionHandlingMiddleware
Represents the middleware component. No public constructor parameters are documented; the type is intended to be instantiated by the dependency injection container.

### InvokeAsync
```csharp
public Task InvokeAsync(HttpContext context, RequestDelegate next)
```
* **Parameters**  
  * `context` – The `HttpContext` for the current request.  
  * `next` – Delegate representing the remaining middleware in the pipeline.
* **Return value** – A `Task` that completes when the middleware has finished processing the request.
* **Behavior** – Invokes `next` to continue processing. If an exception is thrown by `next` or any later middleware, the exception is caught, the `Message`, `ErrorCode`, and `Timestamp` properties are populated with details from the exception, and a JSON response with status code 500 is written to `context.Response`. If no exception occurs, the method simply returns the task from `next`.
* **Throws** – Does not propagate exceptions from the pipeline; however, it may throw an `ObjectDisposedException` if the response has already been sent, or an `InvalidOperationException` if writing the response fails.

### Message
```csharp
public string Message { get; set; }
```
* **Purpose** – Holds the human‑readable error message that will be included in the error response.
* **Type** – `string`. Can be set to any value; typically set to the exception’s `Message` property.
* **Throws** – None.

### ErrorCode
```csharp
public string ErrorCode { get; set; }
```
* **Purpose** – Holds an application‑specific error identifier that will be included in the error response.
* **Type** – `string`. Can be set to any value; often set to a constant or the exception’s HResult.
* **Throws** – None.

### Timestamp
```csharp
public DateTime Timestamp { get; set; }
```
* **Purpose** – Holds the UTC date and time when the exception was captured, to be included in the error response.
* **Type** – `DateTime`. Expected to be set to `DateTime.UtcNow` at the point of catching the exception.
* **Throws** – None.

## Usage

### Registering the middleware
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTransient<ExceptionHandlingMiddleware>(); // or AddScoped/AddSingleton as appropriate
var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.MapGet("/", () => { throw new InvalidOperationException("Demo error"); });
app.Run();
```
When a request to `/` triggers the thrown exception, the middleware catches it and returns a JSON body similar to:
```json
{
  "message": "Demo error",
  "errorCode": "",
  "timestamp": "2025-11-02T14:30:00Z"
}
```

### Customizing error details
```csharp
app.UseMiddleware<ExceptionHandlingMiddleware>((ctx, next) =>
{
    var middleware = app.Services.GetRequiredService<ExceptionHandlingMiddleware>();
    middleware.Message = "A processing error occurred.";
    middleware.ErrorCode = "PROC-001";
    middleware.Timestamp = DateTime.UtcNow;
    return next(ctx);
});
```
In this example the middleware’s properties are set before invoking the pipeline, allowing the caller to dictate the exact content of the error response.

## Notes
* The middleware is designed to be stateless with respect to request processing; the `Message`, `ErrorCode`, and `Timestamp` properties are intended to be set per‑exception, not reused across multiple requests.
* If the middleware is registered as a **singleton**, care must be taken to avoid sharing property values between concurrent requests. Each request should either obtain its own instance (e.g., via scoped/transient registration) or reset the properties before use.
* The middleware swallows exceptions from downstream components; callers that need to observe the original exception should handle it upstream or rely on logging mechanisms external to this middleware.
* Setting `Timestamp` to a value other than `DateTime.UtcNow` will affect the reported time in the response but does not alter the middleware’s internal timing logic.
