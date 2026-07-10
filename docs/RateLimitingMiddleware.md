# RateLimitingMiddleware
The `RateLimitingMiddleware` class is designed to limit the number of requests that can be made within a specified time window, preventing excessive usage and potential denial-of-service attacks. This middleware is particularly useful in distributed systems where multiple clients may be accessing shared resources, helping to maintain system stability and prevent abuse.

## API
* `public RateLimitingMiddleware`: The constructor for the `RateLimitingMiddleware` class, used to create a new instance.
* `public async Task InvokeAsync`: This method is the core of the middleware, responsible for handling incoming requests and enforcing the rate limit. It is an asynchronous method that returns a `Task`, indicating its completion.
* `public List<DateTime> Timestamps`: A list of timestamps representing the times at which requests were made. This property can be used to track and analyze request patterns.
* `public int MaxRequestsPerWindow`: An integer specifying the maximum number of requests allowed within the time window defined by `WindowSizeSeconds`.
* `public int WindowSizeSeconds`: An integer defining the size of the time window (in seconds) during which the rate limit is enforced.

## Usage
The following examples demonstrate how to use the `RateLimitingMiddleware` in a C# application:
```csharp
// Example 1: Basic Usage
var middleware = new RateLimitingMiddleware();
middleware.MaxRequestsPerWindow = 10;
middleware.WindowSizeSeconds = 60;

// Simulate requests
for (int i = 0; i < 15; i++)
{
    try
    {
        await middleware.InvokeAsync();
        Console.WriteLine("Request allowed");
    }
    catch (Exception ex)
    {
        Console.WriteLine("Request denied: " + ex.Message);
    }
}

// Example 2: Advanced Usage with Custom Settings
var customMiddleware = new RateLimitingMiddleware();
customMiddleware.MaxRequestsPerWindow = 5;
customMiddleware.WindowSizeSeconds = 30;

// Use the middleware in a loop to demonstrate rate limiting
for (int i = 0; i < 10; i++)
{
    try
    {
        await customMiddleware.InvokeAsync();
        Console.WriteLine("Request allowed at " + DateTime.Now);
    }
    catch (Exception ex)
    {
        Console.WriteLine("Request denied at " + DateTime.Now + ": " + ex.Message);
    }
    // Simulate a short delay between requests
    await Task.Delay(1000);
}
```

## Notes
The `RateLimitingMiddleware` class is designed to be thread-safe, allowing it to be safely used in concurrent environments. However, the `Timestamps` list is not thread-safe for modification; if multiple threads need to access or modify this list, appropriate synchronization mechanisms should be employed. Additionally, the rate limiting logic is based on a simple sliding window approach, which may not be suitable for all use cases, especially those requiring more complex rate limiting strategies. Edge cases, such as when the `MaxRequestsPerWindow` is set to 0 or a negative value, or when `WindowSizeSeconds` is set to 0, should be handled with care, as these settings could lead to unexpected behavior or errors.
