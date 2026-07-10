#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using System.Globalization;

namespace SarmKadan.DistributedLock.Services;

/// <summary>
/// Provides validation helpers for <see cref="LockMonitor"/> instances.
/// </summary>
public static class LockMonitorValidation
{
    /// <summary>
    /// Validates the configuration and state of a <see cref="LockMonitor"/> instance.
    /// </summary>
    /// <param name="value">The lock monitor instance to validate.</param>
    /// <returns>An immutable list of validation problems; empty if valid.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    public static IReadOnlyList<string> Validate(this LockMonitor? value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = new List<string>();

        // Validate monitored locks collection
        var monitoredLocks = value.GetMonitoredLocks().ToList();
        if (monitoredLocks.Count > 1000)
        {
            problems.Add("LockMonitor has too many monitored locks (maximum 1000).");
        }

        // Validate each monitored lock's configuration
        foreach (var lockKey in monitoredLocks)
        {
            // Validate lock key format
            if (string.IsNullOrWhiteSpace(lockKey))
            {
                problems.Add("Monitored lock key cannot be null or whitespace.");
            }
            else if (lockKey.Length > 1024)
            {
                problems.Add("Monitored lock key cannot exceed 1024 characters.");
            }
        }

        return problems.AsReadOnly();
    }

    /// <summary>
    /// Determines whether the specified <see cref="LockMonitor"/> instance is valid.
    /// </summary>
    /// <param name="value">The lock monitor instance to check.</param>
    /// <returns>True if the instance is valid; otherwise, false.</returns>
    public static bool IsValid(this LockMonitor? value)
    {
        return value?.Validate().Count == 0;
    }

    /// <summary>
    /// Ensures that the specified <see cref="LockMonitor"/> instance is valid.
    /// </summary>
    /// <param name="value">The lock monitor instance to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the instance is not valid, containing a list of problems.</exception>
    public static void EnsureValid(this LockMonitor? value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = value.Validate();
        if (problems.Count > 0)
        {
            throw new ArgumentException(
                $"LockMonitor is not valid. Problems:{Environment.NewLine}- {
                    string.Join(Environment.NewLine + "- ", problems)
                }",
                nameof(value)
            );
        }
    }
}