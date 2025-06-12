#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using System.Diagnostics.CodeAnalysis;
using SarmKadan.DistributedLock.Constants;
using SarmKadan.DistributedLock.Enums;

namespace SarmKadan.DistributedLock.Models;

/// <summary>
/// Provides validation helpers for <see cref="LockConfiguration"/> instances.
/// </summary>
public static class LockConfigurationValidation
{
    /// <summary>
    /// Validates the configuration and returns a list of human-readable problems.
    /// </summary>
    /// <param name="value">The configuration to validate.</param>
    /// <returns>An enumerable of validation error messages. Empty if valid.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public static IReadOnlyList<string> Validate([NotNull] this LockConfiguration? value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var errors = new List<string>();

        // Validate LockKey
        if (string.IsNullOrWhiteSpace(value.LockKey))
        {
            errors.Add("Lock key is required and cannot be null or whitespace.");
        }
        else if (value.LockKey.Length > 1024)
        {
            errors.Add("Lock key cannot exceed 1024 characters.");
        }

        // Validate LockDuration
        if (value.LockDuration.TotalMilliseconds <= 0)
        {
            errors.Add("Lock duration must be greater than zero.");
        }
        else if (value.LockDuration.TotalSeconds < LockConstants.MinimumLockTimeoutSeconds)
        {
            errors.Add($"Lock duration must be at least {LockConstants.MinimumLockTimeoutSeconds} seconds.");
        }
        else if (value.LockDuration.TotalSeconds > LockConstants.MaximumLockTimeoutSeconds)
        {
            errors.Add($"Lock duration must not exceed {LockConstants.MaximumLockTimeoutSeconds} seconds.");
        }

        // Validate AcquisitionTimeout
        if (value.AcquisitionTimeout.TotalMilliseconds <= 0)
        {
            errors.Add("Acquisition timeout must be greater than zero.");
        }

        // Validate AcquisitionMode (always valid as it's an enum)

        // Validate MaxRetries
        if (value.MaxRetries < 0)
        {
            errors.Add("Max retries cannot be negative.");
        }
        else if (value.MaxRetries > 100)
        {
            errors.Add("Max retries cannot exceed 100.");
        }

        // Validate RetryInterval
        if (value.RetryInterval.TotalMilliseconds < 0)
        {
            errors.Add("Retry interval cannot be negative.");
        }
        else if (value.RetryInterval.TotalMilliseconds > LockConstants.MaximumRetryDelayMilliseconds)
        {
            errors.Add($"Retry interval cannot exceed {LockConstants.MaximumRetryDelayMilliseconds} milliseconds.");
        }

        // Validate RenewalInterval
        if (value.RenewalInterval.TotalMilliseconds <= 0)
        {
            errors.Add("Renewal interval must be greater than zero.");
        }

        // Validate AutoRenewal constraints
        if (value.AutoRenewal)
        {
            if (value.RenewalInterval >= value.LockDuration)
            {
                errors.Add("Renewal interval must be less than lock duration when auto-renewal is enabled.");
            }
        }

        // Validate Metadata
        if (value.Metadata is not null)
        {
            if (value.Metadata.Length > 4096)
            {
                errors.Add("Metadata cannot exceed 4096 characters.");
            }
        }

        return errors.AsReadOnly();
    }

    /// <summary>
    /// Determines whether the configuration is valid.
    /// </summary>
    /// <param name="value">The configuration to check.</param>
    /// <returns>True if valid; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public static bool IsValid([NotNull] this LockConfiguration? value)
    {
        return value is not null && !value.Validate().Any();
    }

    /// <summary>
    /// Ensures the configuration is valid, throwing an <see cref="ArgumentException"/> with detailed error messages if not.
    /// </summary>
    /// <param name="value">The configuration to check.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the configuration is invalid, with a detailed message.</exception>
    public static void EnsureValid([NotNull] this LockConfiguration? value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var errors = value.Validate();
        if (!errors.Any())
        {
            return;
        }

        throw new ArgumentException(
            $"Lock configuration is invalid:{Environment.NewLine}- {
                string.Join(Environment.NewLine + "- ", errors)
            }");
    }
}