#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Utilities.Helpers;

using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Extension methods for <see cref="ValidationHelper"/> that provide comprehensive validation
/// and error reporting capabilities.
/// </summary>
public static class ValidationHelperValidation
{
    /// <summary>
    /// Validates all aspects of lock configuration parameters and returns
    /// human-readable problems.
    /// </summary>
    /// <param name="lockName">The lock name to validate.</param>
    /// <param name="duration">The lock duration to validate.</param>
    /// <param name="renewalInterval">Optional renewal interval to validate.</param>
    /// <returns>List of validation problems, or empty list if valid.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="lockName"/> is null.</exception>
    public static IReadOnlyList<string> Validate(
        string? lockName,
        TimeSpan duration,
        TimeSpan? renewalInterval = null)
    {
        ArgumentNullException.ThrowIfNull(lockName);

        var errors = new List<string>();

        ValidationHelper.ValidateLockName(lockName);
        ValidationHelper.ValidateDuration(duration);
        ValidationHelper.ValidateRenewalInterval(renewalInterval, duration);

        return errors.AsReadOnly();
    }

    /// <summary>
    /// Validates lock configuration parameters with additional validations.
    /// </summary>
    /// <param name="lockName">The lock name to validate.</param>
    /// <param name="duration">The lock duration to validate.</param>
    /// <param name="renewalInterval">Optional renewal interval to validate.</param>
    /// <param name="fencingToken">The fencing token to validate.</param>
    /// <param name="ownerId">The owner ID to validate.</param>
    /// <param name="expiresAt">The expiration date to validate.</param>
    /// <param name="apiKey">The API key to validate.</param>
    /// <returns>List of validation problems, or empty list if valid.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="lockName"/> or <paramref name="ownerId"/> or <paramref name="apiKey"/> is null.
    /// </exception>
    public static IReadOnlyList<string> Validate(
        string? lockName,
        TimeSpan duration,
        TimeSpan? renewalInterval,
        ulong fencingToken,
        string? ownerId,
        DateTime expiresAt,
        string? apiKey)
    {
        ArgumentNullException.ThrowIfNull(lockName);
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(apiKey);

        var errors = new List<string>();

        // Validate lock name
        ValidationHelper.ValidateLockName(lockName);

        // Validate duration
        ValidationHelper.ValidateDuration(duration);

        // Validate renewal interval
        ValidationHelper.ValidateRenewalInterval(renewalInterval, duration);

        // Validate fencing token
        ValidationHelper.ValidateFencingToken(fencingToken);

        // Validate owner ID
        ValidationHelper.ValidateOwnerId(ownerId);

        // Validate expiration
        ValidationHelper.ValidateLockNotExpired(expiresAt);

        // Validate API key
        ValidationHelper.ValidateApiKey(apiKey);

        return errors.AsReadOnly();
    }

    /// <summary>
    /// Validates a collection of lock configuration parameters.
    /// </summary>
    /// <param name="configurations">Collection of lock configurations to validate.</param>
    /// <returns>List of validation problems, or empty list if all valid.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="configurations"/> is null.</exception>
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
    /// <param name="lockName">The lock name to check.</param>
    /// <param name="duration">The lock duration to check.</param>
    /// <param name="renewalInterval">Optional renewal interval to check.</param>
    /// <returns>True if valid, false otherwise.</returns>
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
    /// <param name="lockName">The lock name to check.</param>
    /// <param name="duration">The lock duration to check.</param>
    /// <param name="renewalInterval">Optional renewal interval to check.</param>
    /// <param name="fencingToken">The fencing token to check.</param>
    /// <param name="ownerId">The owner ID to check.</param>
    /// <param name="expiresAt">The expiration date to check.</param>
    /// <param name="apiKey">The API key to check.</param>
    /// <returns>True if valid, false otherwise.</returns>
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
    /// <param name="lockName">The lock name to validate.</param>
    /// <param name="duration">The lock duration to validate.</param>
    /// <param name="renewalInterval">Optional renewal interval to validate.</param>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="lockName"/> is null.</exception>
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
    /// <param name="lockName">The lock name to validate.</param>
    /// <param name="duration">The lock duration to validate.</param>
    /// <param name="renewalInterval">Optional renewal interval to validate.</param>
    /// <param name="fencingToken">The fencing token to validate.</param>
    /// <param name="ownerId">The owner ID to validate.</param>
    /// <param name="expiresAt">The expiration date to validate.</param>
    /// <param name="apiKey">The API key to validate.</param>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="lockName"/>, <paramref name="ownerId"/>, or <paramref name="apiKey"/> is null.
    /// </exception>
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
    /// <param name="configurations">Collection of lock configurations to validate.</param>
    /// <exception cref="ArgumentException">Thrown when any configuration is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="configurations"/> is null.</exception>
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