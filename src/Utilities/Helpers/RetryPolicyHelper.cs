#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Utilities.Helpers;

/// <summary>
/// Helper class for implementing retry logic with exponential backoff and jitter.
/// Essential for handling transient failures in distributed lock operations.
/// </summary>
public static class RetryPolicyHelper
{
    private static readonly Random _random = new();

    /// <summary>
    /// Executes an operation with exponential backoff retry policy.
    /// Retries on any exception that matches the provided predicate.
    /// </summary>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries = 3,
        int initialDelayMs = 100,
        double backoffMultiplier = 2.0,
        Func<Exception, bool>? shouldRetry = null)
    {
        shouldRetry ??= _ => true;
        var currentDelayMs = initialDelayMs;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                if (attempt == maxRetries || !shouldRetry(ex))
                    throw;

                // Add jitter to prevent thundering herd
                var jitterMs = (int)(currentDelayMs * 0.1 * _random.NextDouble());
                await Task.Delay(currentDelayMs + jitterMs);

                currentDelayMs = (int)(currentDelayMs * backoffMultiplier);
            }
        }

        throw new InvalidOperationException("Retry logic failed unexpectedly");
    }

    /// <summary>
    /// Executes a synchronous operation with exponential backoff retry policy.
    /// </summary>
    public static T ExecuteWithRetry<T>(
        Func<T> operation,
        int maxRetries = 3,
        int initialDelayMs = 100,
        double backoffMultiplier = 2.0,
        Func<Exception, bool>? shouldRetry = null)
    {
        shouldRetry ??= _ => true;
        var currentDelayMs = initialDelayMs;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return operation();
            }
            catch (Exception ex)
            {
                if (attempt == maxRetries || !shouldRetry(ex))
                    throw;

                var jitterMs = (int)(currentDelayMs * 0.1 * _random.NextDouble());
                Thread.Sleep(currentDelayMs + jitterMs);
                currentDelayMs = (int)(currentDelayMs * backoffMultiplier);
            }
        }

        throw new InvalidOperationException("Retry logic failed unexpectedly");
    }

    /// <summary>
    /// Executes an operation with linear backoff retry policy.
    /// Increases delay by fixed amount on each retry rather than exponentially.
    /// </summary>
    public static async Task<T> ExecuteWithLinearRetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries = 3,
        int delayIncrementMs = 100)
    {
        var currentDelayMs = delayIncrementMs;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                if (attempt == maxRetries)
                    throw;

                var jitterMs = (int)(currentDelayMs * 0.1 * _random.NextDouble());
                await Task.Delay(currentDelayMs + jitterMs);
                currentDelayMs += delayIncrementMs;
            }
        }

        throw new InvalidOperationException("Retry logic failed unexpectedly");
    }

    /// <summary>
    /// Creates a retry policy configuration for consistent retry behavior across the application.
    /// </summary>
    public static RetryPolicy CreatePolicy(
        int maxRetries = 3,
        int initialDelayMs = 100,
        double backoffMultiplier = 2.0)
    {
        return new RetryPolicy
        {
            MaxRetries = maxRetries,
            InitialDelayMs = initialDelayMs,
            BackoffMultiplier = backoffMultiplier
        };
    }

    /// <summary>
    /// Predefined retry policies for common scenarios.
    /// </summary>
    public static class Policies
    {
        /// <summary>
        /// Aggressive retry policy: tries many times with short delays.
        /// Useful for lock acquisitions where contention is expected.
        /// </summary>
        public static RetryPolicy Aggressive => new()
        {
            MaxRetries = 10,
            InitialDelayMs = 50,
            BackoffMultiplier = 1.5
        };

        /// <summary>
        /// Moderate retry policy: standard 3 retries with exponential backoff.
        /// </summary>
        public static RetryPolicy Moderate => new()
        {
            MaxRetries = 3,
            InitialDelayMs = 100,
            BackoffMultiplier = 2.0
        };

        /// <summary>
        /// Conservative retry policy: minimal retries for quick failure detection.
        /// Useful when immediate feedback is more important than transient recovery.
        /// </summary>
        public static RetryPolicy Conservative => new()
        {
            MaxRetries = 1,
            InitialDelayMs = 200,
            BackoffMultiplier = 2.0
        };
    }
}

/// <summary>
/// Encapsulates retry policy configuration.
/// </summary>
public class RetryPolicy
{
    public int MaxRetries { get; set; } = 3;
    public int InitialDelayMs { get; set; } = 100;
    public double BackoffMultiplier { get; set; } = 2.0;
}
