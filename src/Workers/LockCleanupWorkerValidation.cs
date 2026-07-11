#nullable enable

// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Workers;

using System.Globalization;

/// <summary>
/// Provides validation helpers for <see cref="LockCleanupWorker"/> instances.
/// Validates configuration values for lock cleanup operations including timing,
/// batch sizes, and logging preferences.
/// </summary>
public static class LockCleanupWorkerValidation
{
    /// <summary>
    /// Validates the specified lock cleanup worker configuration.
    /// </summary>
    /// <param name="value">The lock cleanup worker to validate</param>
    /// <returns>A list of human-readable validation problems; empty if valid</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null</exception>
    public static IReadOnlyList<string> Validate(this LockCleanupWorker? value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var errors = new List<string>();
        var options = GetWorkerOptions(value);

        if (options is null)
        {
            errors.Add("Worker options cannot be null.");
            return errors;
        }

        if (options.InitialDelayMs < 0)
        {
            errors.Add($"Initial delay must be non-negative, but was {options.InitialDelayMs}.");
        }

        if (options.CleanupIntervalMs <= 0)
        {
            errors.Add($"Cleanup interval must be positive, but was {options.CleanupIntervalMs}.");
        }

        if (options.BatchSize <= 0)
        {
            errors.Add($"Batch size must be positive, but was {options.BatchSize}.");
        }

        if (options.BatchSize > 100_000)
        {
            errors.Add($"Batch size {options.BatchSize} is excessively large (max recommended: 100,000).");
        }

        // VerboseLogging is a boolean, no validation needed

        if (options.MinimumExpiredDuration <= TimeSpan.Zero)
        {
            errors.Add($"Minimum expired duration must be positive, but was {options.MinimumExpiredDuration}.");
        }

        if (options.MinimumExpiredDuration > TimeSpan.FromDays(1))
        {
            errors.Add($"Minimum expired duration {options.MinimumExpiredDuration} is excessively long (max recommended: 1 day).");
        }

        return errors;
    }

    /// <summary>
    /// Determines whether the specified lock cleanup worker configuration is valid.
    /// </summary>
    /// <param name="value">The lock cleanup worker to check</param>
    /// <returns><see langword="true"/> if valid; otherwise, <see langword="false"/></returns>
    public static bool IsValid(this LockCleanupWorker? value) => Validate(value).Count == 0;

    /// <summary>
    /// Ensures that the specified lock cleanup worker configuration is valid.
    /// </summary>
    /// <param name="value">The lock cleanup worker to validate</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null</exception>
    /// <exception cref="ArgumentException">Thrown if validation fails, containing a list of problems</exception>
    public static void EnsureValid(this LockCleanupWorker? value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var errors = Validate(value);
        if (errors.Count == 0)
        {
            return;
        }

        throw new ArgumentException(
            $"LockCleanupWorker configuration is invalid. Problems: {string.Join(" ", errors)}",
            nameof(value));
    }

    private static LockCleanupWorkerOptions? GetWorkerOptions(LockCleanupWorker worker)
    {
        // Use reflection to access the private _options field
        var field = typeof(LockCleanupWorker).GetField(
            "_options",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field?.GetValue(worker) as LockCleanupWorkerOptions;
    }
}