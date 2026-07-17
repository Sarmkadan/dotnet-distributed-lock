#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Formatters;

using System.Globalization;
using SarmKadan.DistributedLock.Models;

/// <summary>
/// Provides validation helpers for lock data structures that have been serialized or deserialized.
/// Validates that lock instances maintain semantic invariants after XML serialization/deserialization.
/// </summary>
public static class XmlLockSerializerValidation
{
    /// <summary>
    /// Validates that an <see cref="XmlLockSerializer"/> instance is semantically valid.
    /// Note: XmlLockSerializer is a stateless utility class and cannot be meaningfully validated.
    /// </summary>
    /// <param name="value">The serializer instance to validate.</param>
    /// <returns>An empty enumerable, as XmlLockSerializer has no validation state.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    public static IReadOnlyList<string> Validate(this XmlLockSerializer? value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Array.Empty<string>();
    }

    /// <summary>
    /// Determines whether an <see cref="XmlLockSerializer"/> instance is semantically valid.
    /// </summary>
    /// <param name="value">The serializer instance to check.</param>
    /// <returns>True if the instance is valid; otherwise, false.</returns>
    public static bool IsValid(this XmlLockSerializer? value) => Validate(value).Count == 0;

    /// <summary>
    /// Ensures that an <see cref="XmlLockSerializer"/> instance is semantically valid.
    /// </summary>
    /// <param name="value">The serializer instance to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the instance is invalid, listing all problems.</exception>
    public static void EnsureValid(this XmlLockSerializer? value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = Validate(value);

        if (problems.Count == 0)
            return;

        throw new ArgumentException(
            $"XmlLockSerializer is invalid. Problems:\n- {string.Join("\n- ", problems)}",
            nameof(value)
        );
    }

    /// <summary>
    /// Validates a <see cref="Lock"/> instance for semantic correctness.
    /// </summary>
    /// <param name="lock">The lock to validate.</param>
    /// <returns>An enumerable of human-readable validation problems; empty if valid.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="@lock"/> is null.</exception>
    public static IReadOnlyList<string> Validate(this Lock? @lock)
    {
        ArgumentNullException.ThrowIfNull(@lock);

        var problems = new List<string>();

        // Validate Key
        if (string.IsNullOrWhiteSpace(@lock.Key))
        {
            problems.Add("Lock.Key is null, empty, or whitespace.");
        }

        // Validate OwnerId
        if (string.IsNullOrWhiteSpace(@lock.OwnerId))
        {
            problems.Add("Lock.OwnerId is null, empty, or whitespace.");
        }

        // Validate Status
        if (@lock.Status == default)
        {
            problems.Add("Lock.Status is the default enum value (0/Unknown).");
        }

        // Validate AcquiredAt
        if (@lock.AcquiredAt == default)
        {
            problems.Add("Lock.AcquiredAt is the default DateTime (Unix epoch).");
        }
        else if (@lock.AcquiredAt.Kind != DateTimeKind.Utc)
        {
            problems.Add("Lock.AcquiredAt is not in UTC format.");
        }

        // Validate ExpiresAt
        if (@lock.ExpiresAt == default)
        {
            problems.Add("Lock.ExpiresAt is the default DateTime (Unix epoch).");
        }
        else if (@lock.ExpiresAt.Kind != DateTimeKind.Utc)
        {
            problems.Add("Lock.ExpiresAt is not in UTC format.");
        }
        else if (@lock.ExpiresAt <= @lock.AcquiredAt)
        {
            problems.Add("Lock.ExpiresAt must be after Lock.AcquiredAt.");
        }

        // Validate RenewedAt (if not null)
        if (@lock.RenewedAt.HasValue)
        {
            if (@lock.RenewedAt.Value == default)
            {
                problems.Add("Lock.RenewedAt is the default DateTime (Unix epoch).");
            }
            else if (@lock.RenewedAt.Value.Kind != DateTimeKind.Utc)
            {
                problems.Add("Lock.RenewedAt is not in UTC format.");
            }
            else if (@lock.RenewedAt.Value < @lock.AcquiredAt)
            {
                problems.Add("Lock.RenewedAt must be after or equal to Lock.AcquiredAt.");
            }
            else if (@lock.RenewedAt.Value > @lock.ExpiresAt)
            {
                problems.Add("Lock.RenewedAt must be before or equal to Lock.ExpiresAt.");
            }
        }

        // Validate RenewalCount
        if (@lock.RenewalCount < 0)
        {
            problems.Add("Lock.RenewalCount is negative.");
        }

        // Validate Duration
        if (@lock.Duration == default)
        {
            problems.Add("Lock.Duration is the default TimeSpan (00:00:00).");
        }
        else if (@lock.Duration.TotalSeconds < Constants.LockConstants.MinimumLockTimeoutSeconds)
        {
            problems.Add(
                $"Lock.Duration ({@lock.Duration.TotalSeconds}s) is less than the minimum allowed ({Constants.LockConstants.MinimumLockTimeoutSeconds}s)."
            );
        }
        else if (@lock.Duration.TotalSeconds > Constants.LockConstants.MaximumLockTimeoutSeconds)
        {
            problems.Add(
                $"Lock.Duration ({@lock.Duration.TotalSeconds}s) exceeds the maximum allowed ({Constants.LockConstants.MaximumLockTimeoutSeconds}s)."
            );
        }

        // Validate that ExpiresAt - AcquiredAt approximately equals Duration
        var actualDuration = @lock.ExpiresAt - @lock.AcquiredAt;
        var durationTolerance = TimeSpan.FromSeconds(1); // Allow 1 second tolerance for serialization/deserialization
        if (Math.Abs(actualDuration.TotalSeconds - @lock.Duration.TotalSeconds) > durationTolerance.TotalSeconds)
        {
            problems.Add(
                $"Lock.ExpiresAt - Lock.AcquiredAt ({actualDuration.TotalSeconds}s) does not match Lock.Duration ({@lock.Duration.TotalSeconds}s)."
            );
        }

        // Validate FencingToken if present
        if (@lock.FencingToken is not null)
        {
            problems.AddRange(Validate(@lock.FencingToken));
        }

        return problems.AsReadOnly();
    }

    /// <summary>
    /// Validates a <see cref="FencingToken"/> instance for semantic correctness.
    /// </summary>
    /// <param name="token">The fencing token to validate.</param>
    /// <returns>An enumerable of human-readable validation problems; empty if valid.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="token"/> is null.</exception>
    public static IReadOnlyList<string> Validate(this FencingToken? token)
    {
        ArgumentNullException.ThrowIfNull(token);

        var problems = new List<string>();

        // Validate Token string
        if (string.IsNullOrWhiteSpace(token.Token))
        {
            problems.Add("FencingToken.Token is null, empty, or whitespace.");
        }
        else if (token.Token.Length != Constants.LockConstants.FencingTokenLength)
        {
            problems.Add(
                $"FencingToken.Token length ({token.Token.Length}) does not match expected length ({Constants.LockConstants.FencingTokenLength})."
            );
        }

        // Validate SequenceNumber
        if (token.SequenceNumber < 0)
        {
            problems.Add("FencingToken.SequenceNumber is negative.");
        }

        // Validate IssuedAt
        if (token.IssuedAt == default)
        {
            problems.Add("FencingToken.IssuedAt is the default DateTime (Unix epoch).");
        }
        else if (token.IssuedAt.Kind != DateTimeKind.Utc)
        {
            problems.Add("FencingToken.IssuedAt is not in UTC format.");
        }

        return problems.AsReadOnly();
    }

    /// <summary>
    /// Validates a <see cref="LockMetrics"/> instance for semantic correctness.
    /// </summary>
    /// <param name="metrics">The metrics to validate.</param>
    /// <returns>An enumerable of human-readable validation problems; empty if valid.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="metrics"/> is null.</exception>
    public static IReadOnlyList<string> Validate(this LockMetrics? metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        var problems = new List<string>();

        // Validate CreatedAt
        if (metrics.CreatedAt == default)
        {
            problems.Add("LockMetrics.CreatedAt is the default DateTime (Unix epoch).");
        }
        else if (metrics.CreatedAt.Kind != DateTimeKind.Utc)
        {
            problems.Add("LockMetrics.CreatedAt is not in UTC format.");
        }

        // Validate LastUpdatedAt
        if (metrics.LastUpdatedAt == default)
        {
            problems.Add("LockMetrics.LastUpdatedAt is the default DateTime (Unix epoch).");
        }
        else if (metrics.LastUpdatedAt.Kind != DateTimeKind.Utc)
        {
            problems.Add("LockMetrics.LastUpdatedAt is not in UTC format.");
        }

        // Validate that LastUpdatedAt >= CreatedAt
        if (metrics.LastUpdatedAt < metrics.CreatedAt)
        {
            problems.Add("LockMetrics.LastUpdatedAt must be after or equal to LockMetrics.CreatedAt.");
        }

        // Validate counters (should not be negative)
        if (metrics.TotalAcquisitionAttempts < 0)
        {
            problems.Add("LockMetrics.TotalAcquisitionAttempts is negative.");
        }

        if (metrics.SuccessfulAcquisitions < 0)
        {
            problems.Add("LockMetrics.SuccessfulAcquisitions is negative.");
        }

        if (metrics.FailedAcquisitions < 0)
        {
            problems.Add("LockMetrics.FailedAcquisitions is negative.");
        }

        if (metrics.TotalRenewals < 0)
        {
            problems.Add("LockMetrics.TotalRenewals is negative.");
        }

        if (metrics.SuccessfulRenewals < 0)
        {
            problems.Add("LockMetrics.SuccessfulRenewals is negative.");
        }

        if (metrics.FailedRenewals < 0)
        {
            problems.Add("LockMetrics.FailedRenewals is negative.");
        }

        if (metrics.TotalReleases < 0)
        {
            problems.Add("LockMetrics.TotalReleases is negative.");
        }

        if (metrics.ExpiredLocks < 0)
        {
            problems.Add("LockMetrics.ExpiredLocks is negative.");
        }

        if (metrics.CurrentActiveLocks < 0)
        {
            problems.Add("LockMetrics.CurrentActiveLocks is negative.");
        }

        // Validate averages (should not be negative)
        if (metrics.AverageAcquisitionTimeMs < 0)
        {
            problems.Add("LockMetrics.AverageAcquisitionTimeMs is negative.");
        }

        if (metrics.AverageHoldTimeMs < 0)
        {
            problems.Add("LockMetrics.AverageHoldTimeMs is negative.");
        }

        return problems.AsReadOnly();
    }

    /// <summary>
    /// Determines whether a <see cref="Lock"/> instance is semantically valid.
    /// </summary>
    /// <param name="lock">The lock to check.</param>
    /// <returns>True if the lock is valid; otherwise, false.</returns>
    public static bool IsValid(this Lock? @lock) => Validate(@lock).Count == 0;

    /// <summary>
    /// Determines whether a <see cref="FencingToken"/> instance is semantically valid.
    /// </summary>
    /// <param name="token">The fencing token to check.</param>
    /// <returns>True if the token is valid; otherwise, false.</returns>
    public static bool IsValid(this FencingToken? token) => Validate(token).Count == 0;

    /// <summary>
    /// Determines whether a <see cref="LockMetrics"/> instance is semantically valid.
    /// </summary>
    /// <param name="metrics">The metrics to check.</param>
    /// <returns>True if the metrics are valid; otherwise, false.</returns>
    public static bool IsValid(this LockMetrics? metrics) => Validate(metrics).Count == 0;

    /// <summary>
    /// Ensures that a <see cref="Lock"/> instance is semantically valid.
    /// </summary>
    /// <param name="lock">The lock to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="@lock"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the lock is invalid, listing all problems.</exception>
    public static void EnsureValid(this Lock? @lock)
    {
        ArgumentNullException.ThrowIfNull(@lock);

        var problems = Validate(@lock);

        if (problems.Count == 0)
            return;

        throw new ArgumentException(
            $"Lock is invalid. Problems:\n- {string.Join("\n- ", problems)}",
            nameof(@lock)
        );
    }

    /// <summary>
    /// Ensures that a <see cref="FencingToken"/> instance is semantically valid.
    /// </summary>
    /// <param name="token">The fencing token to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="token"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the token is invalid, listing all problems.</exception>
    public static void EnsureValid(this FencingToken? token)
    {
        ArgumentNullException.ThrowIfNull(token);

        var problems = Validate(token);

        if (problems.Count == 0)
            return;

        throw new ArgumentException(
            $"FencingToken is invalid. Problems:\n- {string.Join("\n- ", problems)}",
            nameof(token)
        );
    }

    /// <summary>
    /// Ensures that a <see cref="LockMetrics"/> instance is semantically valid.
    /// </summary>
    /// <param name="metrics">The metrics to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="metrics"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the metrics are invalid, listing all problems.</exception>
    public static void EnsureValid(this LockMetrics? metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        var problems = Validate(metrics);

        if (problems.Count == 0)
            return;

        throw new ArgumentException(
            $"LockMetrics is invalid. Problems:\n- {string.Join("\n- ", problems)}",
            nameof(metrics)
        );
    }
}