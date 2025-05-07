#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Services;

/// <summary>
/// Defines the contract for lock acquisition retry policies.
/// </summary>
public interface ILockRetryPolicy
{
    /// <summary>Gets the maximum number of retry attempts.</summary>
    int MaxRetries { get; }

    /// <summary>Gets the initial delay between retry attempts.</summary>
    TimeSpan InitialDelay { get; }

    /// <summary>Gets the maximum delay between retry attempts.</summary>
    TimeSpan MaxDelay { get; }

    /// <summary>Gets the jitter factor (0–1) applied to each delay to prevent thundering herd.</summary>
    double JitterFactor { get; }

    /// <summary>
    /// Calculates the delay before the next retry attempt.
    /// </summary>
    /// <param name="attempt">The zero-based attempt index.</param>
    TimeSpan GetDelay(int attempt);
}

/// <summary>
/// Default retry policy using exponential backoff with optional jitter.
/// </summary>
public sealed class DefaultLockRetryPolicy : ILockRetryPolicy
{
    private static readonly Random _random = new();

    public int MaxRetries { get; }
    public TimeSpan InitialDelay { get; }
    public TimeSpan MaxDelay { get; }
    public double JitterFactor { get; }

    public DefaultLockRetryPolicy(
        int maxRetries = Constants.LockConstants.DefaultMaxRetries,
        TimeSpan? initialDelay = null,
        TimeSpan? maxDelay = null,
        double jitterFactor = 0.1)
    {
        if (maxRetries < 0)
            throw new ArgumentOutOfRangeException(nameof(maxRetries), "Must be non-negative.");
        if (jitterFactor is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(jitterFactor), "Must be between 0 and 1.");

        MaxRetries = maxRetries;
        InitialDelay = initialDelay ?? TimeSpan.FromMilliseconds(Constants.LockConstants.DefaultRetryDelayMilliseconds);
        MaxDelay = maxDelay ?? TimeSpan.FromMilliseconds(Constants.LockConstants.MaximumRetryDelayMilliseconds);
        JitterFactor = jitterFactor;
    }

    public TimeSpan GetDelay(int attempt)
    {
        // Exponential backoff: initialDelay * 2^attempt
        var exponentialMs = InitialDelay.TotalMilliseconds * Math.Pow(2, attempt);
        var cappedMs = Math.Min(exponentialMs, MaxDelay.TotalMilliseconds);

        // Apply jitter
        var jitterMs = cappedMs * JitterFactor * _random.NextDouble();
        return TimeSpan.FromMilliseconds(cappedMs + jitterMs);
    }
}
