#nullable enable

// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using System.Text;

namespace SarmKadan.DistributedLock.Exceptions;

/// <summary>
/// Provides extension methods for <see cref="LockAcquisitionException"/> to enhance error handling and diagnostics.
/// </summary>
public static class LockAcquisitionExceptionExtensions
{
    private const int DefaultTimeoutThresholdMs = 5000;

    /// <summary>
    /// Creates a detailed error message that includes retry suggestions based on the current retry count.
    /// </summary>
    /// <param name="exception">The lock acquisition exception.</param>
    /// <param name="maxRetries">The maximum number of retries configured for the operation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxRetries"/> is less than 0.</exception>
    /// <returns>A formatted error message with retry analysis.</returns>
    public static string ToDetailedErrorMessage(this LockAcquisitionException exception, int maxRetries)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetries);

        var builder = new StringBuilder();
        builder.AppendLine($"Failed to acquire distributed lock '{exception.LockKey}'");
        builder.AppendLine($"Timeout: {exception.Timeout.TotalSeconds}s");
        builder.AppendLine($"Actual retries: {exception.RetryCount}");
        builder.AppendLine($"Max configured retries: {maxRetries}");

        if (exception.RetryCount >= maxRetries)
        {
            builder.AppendLine();
            builder.AppendLine("RECOMMENDATION: Consider increasing the timeout or reducing the lock contention.");
            builder.AppendLine("Check if other operations are holding the lock for too long.");
        }
        else
        {
            builder.AppendLine();
            builder.AppendLine("RECOMMENDATION: Verify that the lock is being released properly by all callers.");
            builder.AppendLine("Check for deadlocks or operations that fail to release locks.");
        }

        builder.AppendLine();
        builder.AppendLine("Original message:");
        builder.AppendLine(exception.Message);

        return builder.ToString();
    }

    /// <summary>
    /// Determines whether the lock acquisition failure was due to a timeout vs. other reasons.
    /// </summary>
    /// <param name="exception">The lock acquisition exception.</param>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is <see langword="null"/>.</exception>
    /// <returns>True if the failure was timeout-related; otherwise false.</returns>
    public static bool IsTimeoutRelated(this LockAcquisitionException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // Timeout-related failures typically have very short timeouts
        return exception.Timeout.TotalMilliseconds < DefaultTimeoutThresholdMs;
    }

    /// <summary>
    /// Creates a simplified error message suitable for logging without sensitive information.
    /// </summary>
    /// <param name="exception">The lock acquisition exception.</param>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is <see langword="null"/>.</exception>
    /// <returns>A sanitized error message for logging purposes.</returns>
    public static string ToLoggableMessage(this LockAcquisitionException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return $"Lock acquisition failed for key '{exception.LockKey}' after {exception.RetryCount} retries within {exception.Timeout.TotalSeconds}s";
    }

    /// <summary>
    /// Creates a retry suggestion based on the current retry count and timeout.
    /// </summary>
    /// <param name="exception">The lock acquisition exception.</param>
    /// <param name="baseDelayMs">The base delay in milliseconds to use for calculations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="baseDelayMs"/> is less than 0.</exception>
    /// <returns>A suggested retry delay in milliseconds.</returns>
    public static int CalculateSuggestedRetryDelay(this LockAcquisitionException exception, int baseDelayMs = 100)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentOutOfRangeException.ThrowIfNegative(baseDelayMs);

        // Exponential backoff with jitter
        // Base delay * 2^retryCount, with some randomness to avoid thundering herd
        var exponentialDelay = baseDelayMs * (int)Math.Pow(2, Math.Min(exception.RetryCount, 10));
        var randomFactor = Random.Shared.Next(80, 120); // 80%-120% of base
        var suggestedDelay = (int)(exponentialDelay * (randomFactor / 100.0));

        // Cap at 30 seconds to avoid excessive delays
        return Math.Min(suggestedDelay, 30000);
    }
}