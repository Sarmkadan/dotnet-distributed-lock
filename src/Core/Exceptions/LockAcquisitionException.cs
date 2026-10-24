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
    /// <summary>
    /// Gets the key of the lock that could not be acquired.
    /// </summary>
    public string LockKey { get; }

    /// <summary>
    /// Gets the acquisition timeout that was exceeded.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Gets the number of retries performed before giving up.
    /// </summary>
    public int RetryCount { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LockAcquisitionException"/> class.
    /// </summary>
    /// <param name="lockKey">The key of the lock that could not be acquired.</param>
    /// <param name="timeout">The acquisition timeout that was exceeded.</param>
    /// <param name="retryCount">The number of retries performed before giving up.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lockKey"/> is <see langword="null"/>.</exception>
    public LockAcquisitionException(string lockKey, TimeSpan timeout, int retryCount = 0)
        : base(FormatMessage(lockKey, timeout, retryCount))
    {
        LockKey = lockKey;
        Timeout = timeout;
        RetryCount = retryCount;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LockAcquisitionException"/> class with an inner exception.
    /// </summary>
    /// <param name="lockKey">The key of the lock that could not be acquired.</param>
    /// <param name="timeout">The acquisition timeout that was exceeded.</param>
    /// <param name="retryCount">The number of retries performed before giving up.</param>
    /// <param name="innerException">The exception that caused the acquisition failure.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lockKey"/> or <paramref name="innerException"/> is <see langword="null"/>.</exception>
    public LockAcquisitionException(string lockKey, TimeSpan timeout, int retryCount, Exception innerException)
        : base(FormatMessage(lockKey, timeout, retryCount), innerException)
    {
        ArgumentNullException.ThrowIfNull(innerException);

        LockKey = lockKey;
        Timeout = timeout;
        RetryCount = retryCount;
    }

    /// <summary>
    /// Validates the lock key and builds the exception message.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lockKey"/> is <see langword="null"/>.</exception>
    private static string FormatMessage(string lockKey, TimeSpan timeout, int retryCount)
    {
        ArgumentNullException.ThrowIfNull(lockKey);

        return $"Failed to acquire lock '{lockKey}' within {timeout.TotalSeconds}s after {retryCount} retries.";
    }
}
