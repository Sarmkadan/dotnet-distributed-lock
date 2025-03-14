// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Utilities.Helpers;

using SarmKadan.DistributedLock.Utilities.Extensions;

/// <summary>
/// Helper class for validating lock parameters and request data.
/// Provides centralized validation logic for consistent error handling.
/// </summary>
public static class ValidationHelper
{
    /// <summary>
    /// Validates a lock name against naming rules.
    /// Throws InvalidOperationException if validation fails.
    /// </summary>
    public static void ValidateLockName(string? lockName)
    {
        if (string.IsNullOrWhiteSpace(lockName))
            throw new InvalidOperationException("Lock name cannot be null or empty");

        if (!lockName.IsValidLockName())
            throw new InvalidOperationException(
                $"Invalid lock name: '{lockName}'. Must be 1-256 characters and contain only alphanumeric, hyphens, underscores, dots, or colons.");
    }

    /// <summary>
    /// Validates a lock duration is positive and reasonable.
    /// </summary>
    public static void ValidateDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
            throw new InvalidOperationException("Lock duration must be greater than zero");

        if (duration > TimeSpan.FromHours(24))
            throw new InvalidOperationException("Lock duration cannot exceed 24 hours");
    }

    /// <summary>
    /// Validates a renewal interval is valid relative to lock duration.
    /// </summary>
    public static void ValidateRenewalInterval(TimeSpan? renewalInterval, TimeSpan lockDuration)
    {
        if (!renewalInterval.HasValue)
            return;

        if (renewalInterval.Value <= TimeSpan.Zero)
            throw new InvalidOperationException("Renewal interval must be greater than zero");

        if (renewalInterval.Value >= lockDuration)
            throw new InvalidOperationException("Renewal interval must be less than lock duration");
    }

    /// <summary>
    /// Validates a fencing token is valid and non-zero.
    /// </summary>
    public static void ValidateFencingToken(ulong fencingToken)
    {
        if (fencingToken == 0)
            throw new InvalidOperationException("Fencing token cannot be zero");
    }

    /// <summary>
    /// Validates owner ID is valid.
    /// </summary>
    public static void ValidateOwnerId(string? ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            throw new InvalidOperationException("Owner ID cannot be null or empty");

        if (ownerId.Length > 256)
            throw new InvalidOperationException("Owner ID cannot exceed 256 characters");
    }

    /// <summary>
    /// Validates that a lock has not expired.
    /// </summary>
    public static void ValidateLockNotExpired(DateTime expiresAt)
    {
        if (expiresAt.IsExpired())
            throw new InvalidOperationException("Lock has already expired");
    }

    /// <summary>
    /// Validates an API key format.
    /// </summary>
    public static void ValidateApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("API key is required");

        if (apiKey.Length < 20)
            throw new InvalidOperationException("API key must be at least 20 characters");

        if (apiKey.Length > 256)
            throw new InvalidOperationException("API key cannot exceed 256 characters");
    }

    /// <summary>
    /// Collects all validation errors for a batch operation and throws if any exist.
    /// </summary>
    public static void ThrowIfAnyErrors(List<string> errors)
    {
        if (errors.Count > 0)
        {
            var message = "Validation failed with " + errors.Count + " error(s):\n" +
                         string.Join("\n", errors.Select((e, i) => $"  {i + 1}. {e}"));
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// Validates request headers for required values.
    /// </summary>
    public static void ValidateHeaders(IDictionary<string, string> headers, params string[] requiredHeaders)
    {
        var missing = requiredHeaders.Where(h => !headers.ContainsKey(h)).ToList();

        if (missing.Count > 0)
        {
            var headerList = string.Join(", ", missing.Select(h => $"'{h}'"));
            throw new InvalidOperationException($"Missing required headers: {headerList}");
        }
    }

    /// <summary>
    /// Validates that a string can be parsed as the specified type.
    /// </summary>
    public static bool TryParseAs<T>(string? value, out T? result) where T : struct, IComparable, IConvertible
    {
        result = default;

        if (string.IsNullOrEmpty(value))
            return false;

        try
        {
            result = (T)Convert.ChangeType(value, typeof(T));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a validation result containing both success status and error messages.
    /// </summary>
    public static ValidationResult ValidateLockConfiguration(
        string? lockName,
        TimeSpan duration,
        TimeSpan? renewalInterval = null)
    {
        var errors = new List<string>();

        try { ValidateLockName(lockName); }
        catch (Exception ex) { errors.Add(ex.Message); }

        try { ValidateDuration(duration); }
        catch (Exception ex) { errors.Add(ex.Message); }

        try { ValidateRenewalInterval(renewalInterval, duration); }
        catch (Exception ex) { errors.Add(ex.Message); }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}

/// <summary>
/// Encapsulates validation results.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public string ErrorMessage => string.Join("; ", Errors);
}
