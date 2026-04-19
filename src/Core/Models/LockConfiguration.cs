#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using SarmKadan.DistributedLock.Enums;

namespace SarmKadan.DistributedLock.Models;

/// <summary>
/// Configuration for a specific distributed lock instance.
/// </summary>
public class LockConfiguration
{
    public string LockKey { get; set; }
    public TimeSpan LockDuration { get; set; }
    public TimeSpan AcquisitionTimeout { get; set; }
    public AcquisitionMode AcquisitionMode { get; set; }
    public int MaxRetries { get; set; }
    public TimeSpan RetryInterval { get; set; }
    public TimeSpan RenewalInterval { get; set; }
    public bool AutoRenewal { get; set; }
    public bool UseFencingToken { get; set; }
    public string? Metadata { get; set; }

    public LockConfiguration()
    {
        LockKey = string.Empty;
        LockDuration = Constants.LockConstants.DefaultLockTimeout;
        AcquisitionTimeout = Constants.LockConstants.DefaultAcquisitionTimeout;
        AcquisitionMode = AcquisitionMode.Blocking;
        MaxRetries = Constants.LockConstants.DefaultMaxRetries;
        RetryInterval = TimeSpan.FromMilliseconds(Constants.LockConstants.DefaultRetryDelayMilliseconds);
        RenewalInterval = Constants.LockConstants.DefaultRenewalInterval;
        AutoRenewal = true;
        UseFencingToken = true;
    }

    public LockConfiguration(string lockKey)
        : this()
    {
        if (string.IsNullOrWhiteSpace(lockKey))
            throw new ArgumentException("Lock key cannot be null or empty.", nameof(lockKey));

        LockKey = lockKey;
    }

    // Validates the configuration for consistency
    public IEnumerable<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(LockKey))
            errors.Add("Lock key is required.");

        if (LockDuration.TotalSeconds < Constants.LockConstants.MinimumLockTimeoutSeconds)
            errors.Add($"Lock duration must be at least {Constants.LockConstants.MinimumLockTimeoutSeconds}s.");

        if (LockDuration.TotalSeconds > Constants.LockConstants.MaximumLockTimeoutSeconds)
            errors.Add($"Lock duration must not exceed {Constants.LockConstants.MaximumLockTimeoutSeconds}s.");

        if (AcquisitionTimeout.TotalMilliseconds <= 0)
            errors.Add("Acquisition timeout must be greater than zero.");

        if (MaxRetries < 0)
            errors.Add("Max retries cannot be negative.");

        if (RetryInterval.TotalMilliseconds < 0)
            errors.Add("Retry interval cannot be negative.");

        if (AutoRenewal && RenewalInterval >= LockDuration)
            errors.Add("Renewal interval must be less than lock duration for auto-renewal to work.");

        if (RenewalInterval.TotalMilliseconds <= 0 && AutoRenewal)
            errors.Add("Renewal interval must be greater than zero when auto-renewal is enabled.");

        return errors;
    }

    // Checks if the configuration is valid
    public bool IsValid => !Validate().Any();

    public override string ToString() =>
        $"LockConfiguration(Key={LockKey}, Duration={LockDuration.TotalSeconds:F1}s, " +
        $"Mode={AcquisitionMode}, AutoRenewal={AutoRenewal}, UseFencingToken={UseFencingToken})";
}
