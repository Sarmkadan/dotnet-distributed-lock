#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using System.Globalization;
using SarmKadan.DistributedLock.Constants;
using SarmKadan.DistributedLock.Models;

namespace SarmKadan.DistributedLock.Services;

/// <summary>
/// Provides validation helpers for <see cref="LockService"/> instances.
/// </summary>
public static class LockServiceValidation
{
    /// <summary>
    /// Validates the specified <see cref="LockService"/> instance.
    /// </summary>
    /// <param name="value">The lock service to validate.</param>
    /// <returns>A list of human-readable validation problems; empty if the service is valid.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    public static IReadOnlyList<string> Validate(this LockService? value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = new List<string>();

        // Validate repository (should not be null)
        if (value.GetType().GetField("_repository", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(value) is null)
        {
            problems.Add("Repository dependency is null.");
        }

        // Validate logger (should not be null)
        if (value.GetType().GetField("_logger", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(value) is null)
        {
            problems.Add("Logger dependency is null.");
        }

        // Validate metrics (should not be null and should have reasonable initial values)
        var metricsField = value.GetType().GetProperty("GetMetrics", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (metricsField is not null)
        {
            var metrics = value.GetMetrics();
            if (metrics is null)
            {
                problems.Add("Metrics instance is null.");
            }
            else
            {
                // Validate metrics timestamps are reasonable
                var now = DateTime.UtcNow;
                var createdAt = metrics.CreatedAt;
                var lastUpdatedAt = metrics.LastUpdatedAt;

                if (createdAt > now.AddMinutes(1))
                {
                    problems.Add("Metrics CreatedAt timestamp is in the future.");
                }

                if (lastUpdatedAt > now.AddMinutes(1))
                {
                    problems.Add("Metrics LastUpdatedAt timestamp is in the future.");
                }

                if (createdAt > lastUpdatedAt)
                {
                    problems.Add("Metrics CreatedAt timestamp is after LastUpdatedAt.");
                }
            }
        }

        return problems.AsReadOnly();
    }

    /// <summary>
    /// Determines whether the specified <see cref="LockService"/> instance is valid.
    /// </summary>
    /// <param name="value">The lock service to check.</param>
    /// <returns><see langword="true"/> if the service is valid; otherwise, <see langword="false"/>.</returns>
    public static bool IsValid(this LockService? value)
    {
        return !value?.Validate().Any() ?? false;
    }

    /// <summary>
    /// Ensures that the specified <see cref="LockService"/> instance is valid, throwing an <see cref="ArgumentException"/> if it is not.
    /// </summary>
    /// <param name="value">The lock service to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the service has validation problems.</exception>
    public static void EnsureValid(this LockService? value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = value.Validate();
        if (problems.Count == 0)
        {
            return;
        }

        throw new ArgumentException(
            $"LockService validation failed:{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", problems)}",
            nameof(value)
        );
    }
}