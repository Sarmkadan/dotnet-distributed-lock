// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Api.Middleware;

/// <summary>
/// Rate limiting middleware that prevents abuse by limiting requests per client IP address.
/// Uses a sliding window counter to track request frequency and enforce limits.
/// Particularly important for protecting the lock acquisition endpoints from DOS attacks.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitingOptions _options;
    private static readonly Dictionary<string, RequestWindow> _requestWindows = new();
    private static readonly object _lockObject = new();

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        RateLimitingOptions? options = null)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new RateLimitingOptions();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientIp = GetClientIp(context);

        if (IsRateLimited(clientIp))
        {
            _logger.LogWarning("Rate limit exceeded for client: {ClientIp}", clientIp);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                message = "Rate limit exceeded. Please try again later.",
                retryAfterSeconds = _options.WindowSizeSeconds
            });
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// Checks if a client IP has exceeded the rate limit.
    /// Maintains a sliding window of request timestamps per IP address.
    /// </summary>
    private bool IsRateLimited(string clientIp)
    {
        lock (_lockObject)
        {
            var now = DateTime.UtcNow;

            if (!_requestWindows.TryGetValue(clientIp, out var window))
            {
                window = new RequestWindow();
                _requestWindows[clientIp] = window;
            }

            // Remove timestamps outside the current window
            window.Timestamps.RemoveAll(ts =>
                (now - ts).TotalSeconds > _options.WindowSizeSeconds);

            // Check if limit exceeded
            if (window.Timestamps.Count >= _options.MaxRequestsPerWindow)
            {
                return true;
            }

            // Record this request
            window.Timestamps.Add(now);

            // Clean up old entries periodically
            if (_requestWindows.Count > 10000)
            {
                var oldEntries = _requestWindows
                    .Where(x => x.Value.Timestamps.Count == 0)
                    .Select(x => x.Key)
                    .ToList();

                foreach (var key in oldEntries)
                    _requestWindows.Remove(key);
            }

            return false;
        }
    }

    private static string GetClientIp(HttpContext context)
    {
        // Check for IP from forwarded header (when behind proxy)
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded))
        {
            return forwarded.ToString().Split(',').First().Trim();
        }

        // Fall back to remote IP
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private class RequestWindow
    {
        public List<DateTime> Timestamps { get; } = new();
    }
}

/// <summary>
/// Configuration options for rate limiting behavior.
/// Allows customization of request limits and time windows.
/// </summary>
public class RateLimitingOptions
{
    /// <summary>
    /// Maximum number of requests allowed per client within the window.
    /// Default: 100 requests
    /// </summary>
    public int MaxRequestsPerWindow { get; set; } = 100;

    /// <summary>
    /// Size of the sliding time window in seconds.
    /// Default: 60 seconds (1 minute)
    /// </summary>
    public int WindowSizeSeconds { get; set; } = 60;
}
