#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Exceptions;

/// <summary>
/// Thrown when a lock cannot be acquired within the specified timeout.
/// </summary>
public class LockAcquisitionException : DistributedLockException
{
    public string LockKey { get; }
    public TimeSpan Timeout { get; }
    public int RetryCount { get; }

    public LockAcquisitionException(string lockKey, TimeSpan timeout, int retryCount = 0)
        : base($"Failed to acquire lock '{lockKey}' within {timeout.TotalSeconds}s after {retryCount} retries.")
    {
        LockKey = lockKey;
        Timeout = timeout;
        RetryCount = retryCount;
    }

    public LockAcquisitionException(string lockKey, TimeSpan timeout, int retryCount, Exception innerException)
        : base($"Failed to acquire lock '{lockKey}' within {timeout.TotalSeconds}s after {retryCount} retries.", innerException)
    {
        LockKey = lockKey;
        Timeout = timeout;
        RetryCount = retryCount;
    }
}
