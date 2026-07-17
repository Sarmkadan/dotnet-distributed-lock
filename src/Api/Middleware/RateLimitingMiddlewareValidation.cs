#nullable enable

// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Api.Middleware;

/// <summary>
/// Provides validation helpers for <see cref="RateLimitingMiddleware"/> instances.
/// Validates configuration values and runtime state to ensure rate limiting works correctly.
/// </summary>
public static class RateLimitingMiddlewareValidation
{
    /// <summary>
    /// Validates a <see cref="RateLimitingMiddleware"/> instance and returns a list of human-readable validation problems.
    /// </summary>
    /// <param name="value">The middleware instance to validate.</param>
    /// <returns>An enumerable of validation problems; empty if the instance is valid.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    public static IReadOnlyList<string> Validate(this RateLimitingMiddleware? value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = new List<string>();

        var options = GetOptions(value);

        // Validate MaxRequestsPerWindow
        if (options.MaxRequestsPerWindow <= 0)
        {
            problems.Add($"MaxRequestsPerWindow must be a positive integer, but was {options.MaxRequestsPerWindow}.");
        }

        // Validate WindowSizeSeconds
        if (options.WindowSizeSeconds <= 0)
        {
            problems.Add($"WindowSizeSeconds must be a positive integer, but was {options.WindowSizeSeconds}.");
        }

        return problems.AsReadOnly();
    }

    /// <summary>
    /// Determines whether a <see cref="RateLimitingMiddleware"/> instance is valid.
    /// </summary>
    /// <param name="value">The middleware instance to check.</param>
    /// <returns>True if the instance is valid; otherwise, false.</returns>
    public static bool IsValid(this RateLimitingMiddleware? value) => value?.Validate().Count == 0;

    /// <summary>
    /// Ensures that a <see cref="RateLimitingMiddleware"/> instance is valid.
    /// Throws an <see cref="ArgumentException"/> with a detailed message listing all validation problems if the instance is invalid.
    /// </summary>
    /// <param name="value">The middleware instance to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="value"/> is invalid, containing a list of all validation problems.</exception>
    public static void EnsureValid(this RateLimitingMiddleware? value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = value.Validate();

        if (problems.Count > 0)
        {
            throw new ArgumentException(
                $"RateLimitingMiddleware is invalid. Problems:\n- {
                    string.Join("\n- ", problems)
                }",
                nameof(value));
        }
    }

    private static RateLimitingOptions GetOptions(RateLimitingMiddleware middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);

        const string fieldName = "_options";
        var field = typeof(RateLimitingMiddleware).GetField(
            fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field is null)
        {
            throw new InvalidOperationException(
                $"Failed to find private field '{fieldName}' on type {typeof(RateLimitingMiddleware).FullName}.");
        }

        return field.GetValue(middleware) as RateLimitingOptions
            ?? throw new InvalidOperationException(
                $"Private field '{fieldName}' on {typeof(RateLimitingMiddleware).FullName} is null.");
    }
}
