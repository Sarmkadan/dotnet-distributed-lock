#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Constants;

/// <summary>
/// Contains constant values used throughout the distributed lock system.
/// </summary>
public static class LockConstants
{
    public const int DefaultLockTimeoutSeconds = 30;
    public const int DefaultAcquisitionTimeoutSeconds = 5;
    public const int DefaultRenewalIntervalSeconds = 10;
    public const int MinimumLockTimeoutSeconds = 1;
    public const int MaximumLockTimeoutSeconds = 3600;

    public const int DefaultMaxRetries = 3;
    public const int DefaultRetryDelayMilliseconds = 100;
    public const int MaximumRetryDelayMilliseconds = 5000;

    public const int DefaultMonitoringIntervalMilliseconds = 500;
    public const int MinimumMonitoringIntervalMilliseconds = 100;

    public const string LockKeyPrefix = "distributed_lock:";
    public const string FencingTokenSeparator = ":";

    public const int FencingTokenLength = 32;
    public const int DefaultMaxConcurrentLocks = 1000;

    public static readonly TimeSpan DefaultLockTimeout = TimeSpan.FromSeconds(DefaultLockTimeoutSeconds);
    public static readonly TimeSpan DefaultAcquisitionTimeout = TimeSpan.FromSeconds(DefaultAcquisitionTimeoutSeconds);
    public static readonly TimeSpan DefaultRenewalInterval = TimeSpan.FromSeconds(DefaultRenewalIntervalSeconds);
}
