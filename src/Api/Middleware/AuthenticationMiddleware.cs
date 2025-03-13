#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Api.Middleware;

/// <summary>
/// Authentication middleware that validates API keys and tokens for protected endpoints.
/// Supports simple API key authentication via headers.
/// Can be extended to support JWT tokens, OAuth, or other schemes.
/// </summary>
public sealed class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthenticationMiddleware> _logger;
    private readonly HashSet<string> _publicEndpoints;

    public AuthenticationMiddleware(RequestDelegate next, ILogger<AuthenticationMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Define endpoints that don't require authentication
        _publicEndpoints = new HashSet<string>
        {
            "/api/health/live",
            "/api/health/ready",
            "/swagger",
            "/swagger/index.html"
        };
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var path = request.Path.Value ?? string.Empty;

        // Skip authentication for public endpoints
        if (_publicEndpoints.Any(ep => path.StartsWith(ep)))
        {
            await _next(context);
            return;
        }

        // Extract and validate API key from headers
        if (!request.Headers.TryGetValue("X-API-Key", out var apiKey))
        {
            _logger.LogWarning("Request missing API key: {Path}", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { message = "API key is required" });
            return;
        }

        // Validate API key format and existence
        if (!ValidateApiKey(apiKey.ToString()))
        {
            _logger.LogWarning("Invalid API key provided for: {Path}", path);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { message = "Invalid API key" });
            return;
        }

        // Add authenticated client info to context for downstream use
        context.Items["ApiKey"] = apiKey.ToString();
        context.Items["AuthenticatedAt"] = DateTime.UtcNow;

        await _next(context);
    }

    /// <summary>
    /// Validates the provided API key.
    /// In production, this should check against a secure store (database, Azure Key Vault, etc).
    /// For demonstration, checks against environment variable.
    /// </summary>
    private static bool ValidateApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Length < 20)
            return false;

        // In production environment, retrieve valid keys from secure store
        var validKeys = Environment.GetEnvironmentVariable("VALID_API_KEYS")?.Split(';') ?? []<string>();

        if (validKeys.Length == 0)
        {
            // Development mode: accept any properly formatted key
            return apiKey.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
        }

        return validKeys.Contains(apiKey);
    }
}
