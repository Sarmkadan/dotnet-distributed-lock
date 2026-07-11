#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Utilities.Helpers;

using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Extension methods for ValidationHelper that provide comprehensive validation
/// and error reporting capabilities.
/// </summary>
public static class ValidationHelperValidation
{
    /// <summary>
    /// Validates all aspects of lock configuration parameters and returns
    /// human-readable problems.
    /// </summary>
    /// <param name="lockName">The lock name to validate</param>
    /// <param name="duration">The lock duration to validate</param>
    /// <param name="renewalInterval">Optional renewal interval to validate</param>
    /// <returns>List of validation problems, or empty list if valid</returns>
    public static IReadOnlyList<string> Validate(
        string? lockName,
        TimeSpan duration,
        TimeSpan? renewalInterval = null)
    {
        var errors = new List<string>();

        try
        {
            ValidationHelper.ValidateLockName(lockName);
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
        }

        try
        {
            ValidationHelper.ValidateDuration(duration);
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
        }

        try
        {
            ValidationHelper.ValidateRenewalInterval(renewalInterval, duration);
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
        }

        return errors.AsReadOnly();
    }

    /// <summary>
    /// Validates lock configuration parameters with additional validations.
    /// </summary>
    /// <param name="lockName">The lock name to validate</param>
    /// <param name="duration">The lock duration to validate</param>
    /// <param name="renewalInterval">Optional renewal interval to validate</param>
    /// <param name="fencingToken">The fencing token to validate</param>
    /// <param name="ownerId">The owner ID to validate</param>
    /// <param name="expiresAt">The expiration date to validate</param>
    /// <param name="apiKey">The API key to validate</param>
    /// <returns>List of validation problems, or empty list if valid</returns>
    public static IReadOnlyList<string> Validate(
        string? lockName,
        TimeSpan duration,
        TimeSpan? renewalInterval,
        ulong fencingToken,
        string? ownerId,
        DateTime expiresAt,
        string? apiKey)
    {
        var errors = new List<string>();

        // Validate lock name
        try
        {
            ValidationHelper.ValidateLockName(lockName);
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
        }

        // Validate duration
        try
        {
            ValidationHelper.ValidateDuration(duration);
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
        }

        // Validate renewal interval
        try
        {
            ValidationHelper.ValidateRenewalInterval(renewalInterval, duration);
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
        }

        // Validate fencing token
        try
        {
            ValidationHelper.ValidateFencingToken(fencingToken);
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
        }

        // Validate owner ID
        try
        {
            ValidationHelper.ValidateOwnerId(ownerId);
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
        }

        // Validate expiration
        try
        {
            ValidationHelper.ValidateLockNotExpired(expiresAt);
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
        }

        // Validate API key
        try
        {
            ValidationHelper.ValidateApiKey(apiKey);
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
        }

        return errors.AsReadOnly();
    }

    /// <summary>
    /// Validates a collection of lock configuration parameters.
    /// </summary>
    /// <param name="configurations">Collection of lock configurations to validate</param>
    /// <returns>List of validation problems, or empty list if all valid</returns>
    public static IReadOnlyList<string> Validate(
        IEnumerable<(string? LockName, TimeSpan Duration, TimeSpan? RenewalInterval)> configurations)
    {
        ArgumentNullException.ThrowIfNull(configurations);

        var allErrors = new List<string>();
        var index = 0;

        foreach (var config in configurations)
        {
            try
            {
                ValidationHelper.ValidateLockName(config.LockName);
            }
            catch (Exception ex)
            {
                allErrors.Add($"Configuration[{index}]: {ex.Message} (LockName: {config.LockName ?? "null"})");
            }

            try
            {
                ValidationHelper.ValidateDuration(config.Duration);
            }
            catch (Exception ex)
            {
                allErrors.Add($"Configuration[{index}]: {ex.Message} (Duration: {config.Duration})");
            }

            try
            {
                ValidationHelper.ValidateRenewalInterval(config.RenewalInterval, config.Duration);
            }
            catch (Exception ex)
            {
                allErrors.Add($"Configuration[{index}]: {ex.Message} (RenewalInterval: {config.RenewalInterval?.ToString() ?? "null"})");
            }

            index++;
        }

        return allErrors.AsReadOnly();
    }

    /// <summary>
    /// Determines whether the specified lock configuration is valid.
    /// </summary>
    /// <param name="lockName">The lock name to check</param>
    /// <param name="duration">The lock duration to check</param>
    /// <param name="renewalInterval">Optional renewal interval to check</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool IsValid(
        string? lockName,
        TimeSpan duration,
        TimeSpan? renewalInterval = null)
    {
        var errors = Validate(lockName, duration, renewalInterval);
        return errors.Count == 0;
    }

    /// <summary>
    /// Determines whether the specified lock configuration is valid.
    /// </summary>
    /// <param name="lockName">The lock name to check</param>
    /// <param name="duration">The lock duration to check</param>
    /// <param name="renewalInterval">Optional renewal interval to check</param>
    /// <param name="fencingToken">The fencing token to check</param>
    /// <param name="ownerId">The owner ID to check</param>
    /// <param name="expiresAt">The expiration date to check</param>
    /// <param name="apiKey">The API key to check</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool IsValid(
        string? lockName,
        TimeSpan duration,
        TimeSpan? renewalInterval,
        ulong fencingToken,
        string? ownerId,
        DateTime expiresAt,
        string? apiKey)
    {
        var errors = Validate(lockName, duration, renewalInterval, fencingToken, ownerId, expiresAt, apiKey);
        return errors.Count == 0;
    }

    /// <summary>
    /// Ensures that the specified lock configuration is valid, throwing an
    /// ArgumentException with detailed error messages if validation fails.
    /// </summary>
    /// <param name="lockName">The lock name to validate</param>
    /// <param name="duration">The lock duration to validate</param>
    /// <param name="renewalInterval">Optional renewal interval to validate</param>
    /// <exception cref="ArgumentException">Thrown when validation fails</exception>
    public static void EnsureValid(
        string? lockName,
        TimeSpan duration,
        TimeSpan? renewalInterval = null)
    {
        var errors = Validate(lockName, duration, renewalInterval);
        if (errors.Count > 0)
        {
            var message = $"Validation failed with {errors.Count} error(s):\n" +
                         string.Join("\n", errors.Select((e, i) => $" {i + 1}. {e}"));
            throw new ArgumentException(message);
        }
    }

    /// <summary>
    /// Ensures that the specified lock configuration is valid, throwing an
    /// ArgumentException with detailed error messages if validation fails.
    /// </summary>
    /// <param name="lockName">The lock name to validate</param>
    /// <param name="duration">The lock duration to validate</param>
    /// <param name="renewalInterval">Optional renewal interval to validate</param>
    /// <param name="fencingToken">The fencing token to validate</param>
    /// <param name="ownerId">The owner ID to validate</param>
    /// <param name="expiresAt">The expiration date to validate</param>
    /// <param name="apiKey">The API key to validate</param>
    /// <exception cref="ArgumentException">Thrown when validation fails</exception>
    public static void EnsureValid(
        string? lockName,
        TimeSpan duration,
        TimeSpan? renewalInterval,
        ulong fencingToken,
        string? ownerId,
        DateTime expiresAt,
        string? apiKey)
    {
        var errors = Validate(lockName, duration, renewalInterval, fencingToken, ownerId, expiresAt, apiKey);
        if (errors.Count > 0)
        {
            var message = $"Validation failed with {errors.Count} error(s):\n" +
                         string.Join("\n", errors.Select((e, i) => $" {i + 1}. {e}"));
            throw new ArgumentException(message);
        }
    }

    /// <summary>
    /// Ensures that a collection of lock configurations are all valid.
    /// </summary>
    /// <param name="configurations">Collection of lock configurations to validate</param>
    /// <exception cref="ArgumentException">Thrown when any configuration is invalid</exception>
    public static void EnsureValid(
        IEnumerable<(string? LockName, TimeSpan Duration, TimeSpan? RenewalInterval)> configurations)
    {
        ArgumentNullException.ThrowIfNull(configurations);

        var errors = Validate(configurations);
        if (errors.Count > 0)
        {
            var message = $"Validation failed with {errors.Count} error(s):\n" +
                         string.Join("\n", errors.Select((e, i) => $" {i + 1}. {e}"));
            throw new ArgumentException(message);
        }
    }
}