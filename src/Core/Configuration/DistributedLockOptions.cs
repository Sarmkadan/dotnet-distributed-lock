// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using SarmKadan.DistributedLock.Enums;

namespace SarmKadan.DistributedLock.Configuration;

/// <summary>
/// Configuration options for the distributed lock system.
/// </summary>
public class DistributedLockOptions
{
    public BackendType BackendType { get; set; } = BackendType.InMemory;
    public string ConnectionString { get; set; } = string.Empty;
    public TimeSpan DefaultLockDuration { get; set; } = Constants.LockConstants.DefaultLockTimeout;
    public TimeSpan DefaultAcquisitionTimeout { get; set; } = Constants.LockConstants.DefaultAcquisitionTimeout;
    public TimeSpan DefaultRenewalInterval { get; set; } = Constants.LockConstants.DefaultRenewalInterval;
    public int DefaultMaxRetries { get; set; } = Constants.LockConstants.DefaultMaxRetries;
    public int DefaultRetryDelayMs { get; set; } = Constants.LockConstants.DefaultRetryDelayMilliseconds;
    public AcquisitionMode DefaultAcquisitionMode { get; set; } = AcquisitionMode.Blocking;
    public bool EnableAutoRenewal { get; set; } = true;
    public bool UseFencingTokens { get; set; } = true;
    public TimeSpan MonitoringInterval { get; set; } = TimeSpan.FromMilliseconds(Constants.LockConstants.DefaultMonitoringIntervalMilliseconds);
    public int MaxConcurrentLocks { get; set; } = Constants.LockConstants.DefaultMaxConcurrentLocks;
    public bool EnableMetrics { get; set; } = true;
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public IEnumerable<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ConnectionString) && BackendType != BackendType.InMemory)
            errors.Add("Connection string is required for non-InMemory backends.");

        if (DefaultLockDuration.TotalSeconds < Constants.LockConstants.MinimumLockTimeoutSeconds)
            errors.Add($"Default lock duration must be at least {Constants.LockConstants.MinimumLockTimeoutSeconds}s.");

        if (DefaultLockDuration.TotalSeconds > Constants.LockConstants.MaximumLockTimeoutSeconds)
            errors.Add($"Default lock duration must not exceed {Constants.LockConstants.MaximumLockTimeoutSeconds}s.");

        if (DefaultAcquisitionTimeout.TotalMilliseconds <= 0)
            errors.Add("Default acquisition timeout must be greater than zero.");

        if (DefaultRenewalInterval.TotalMilliseconds <= 0)
            errors.Add("Default renewal interval must be greater than zero.");

        if (DefaultRenewalInterval >= DefaultLockDuration)
            errors.Add("Renewal interval must be less than lock duration.");

        if (DefaultMaxRetries < 0)
            errors.Add("Max retries cannot be negative.");

        if (DefaultRetryDelayMs < 0)
            errors.Add("Retry delay cannot be negative.");

        if (MaxConcurrentLocks <= 0)
            errors.Add("Max concurrent locks must be greater than zero.");

        return errors;
    }

    public bool IsValid => !Validate().Any();
}
