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

        // Validate MaxRequestsPerWindow
        if (value.MaxRequestsPerWindow <= 0)
        {
            problems.Add($"MaxRequestsPerWindow must be a positive integer, but was {value.MaxRequestsPerWindow}.");
        }

        // Validate WindowSizeSeconds
        if (value.WindowSizeSeconds <= 0)
        {
            problems.Add($"WindowSizeSeconds must be a positive integer, but was {value.WindowSizeSeconds}.");
        }

        // Validate Timestamps (should not be null and should not contain default/min values)
        if (value.Timestamps is null)
        {
            problems.Add("Timestamps collection cannot be null.");
        }
        else
        {
            // Check for default DateTime values in timestamps
            foreach (var timestamp in value.Timestamps)
            {
                if (timestamp == default)
                {
                    problems.Add("Timestamps collection contains a default DateTime value (DateTime.MinValue).");
                    break;
                }
            }

            // Check for timestamps that are in the future (relative to now)
            var now = DateTime.UtcNow;
            foreach (var timestamp in value.Timestamps)
            {
                if (timestamp > now.AddMinutes(1))
                {
                    problems.Add($"Timestamps collection contains a timestamp in the future: {timestamp:O}.");
                    break;
                }
            }
        }

        return problems.AsReadOnly();
    }

    /// <summary>
    /// Determines whether a <see cref="RateLimitingMiddleware"/> instance is valid.
    /// </summary>
    /// <param name="value">The middleware instance to check.</param>
    /// <returns>True if the instance is valid; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    public static bool IsValid(this RateLimitingMiddleware? value)
    {
        return value?.Validate().Count == 0;
    }

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
                }");
        }
    }
}
