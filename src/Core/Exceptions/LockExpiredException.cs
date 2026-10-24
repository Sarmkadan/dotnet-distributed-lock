#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Exceptions;

/// <summary>
/// Thrown when an operation is attempted on an expired lock.
/// </summary>
public class LockExpiredException : DistributedLockException
{
    /// <summary>
    /// Gets the key of the lock that has expired.
    /// </summary>
    public string LockKey { get; }

    /// <summary>
    /// Gets the time at which the lock expired.
    /// </summary>
    public DateTime ExpirationTime { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LockExpiredException"/> class.
    /// </summary>
    /// <param name="lockKey">The key of the lock that has expired.</param>
    /// <param name="expirationTime">The time at which the lock expired.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lockKey"/> is <see langword="null"/>.</exception>
    public LockExpiredException(string lockKey, DateTime expirationTime)
        : base(FormatMessage(lockKey, expirationTime))
    {
        LockKey = lockKey;
        ExpirationTime = expirationTime;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LockExpiredException"/> class with an inner exception.
    /// </summary>
    /// <param name="lockKey">The key of the lock that has expired.</param>
    /// <param name="expirationTime">The time at which the lock expired.</param>
    /// <param name="innerException">The exception that caused the expiration failure.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lockKey"/> or <paramref name="innerException"/> is <see langword="null"/>.</exception>
    public LockExpiredException(string lockKey, DateTime expirationTime, Exception innerException)
        : base(FormatMessage(lockKey, expirationTime), innerException)
    {
        ArgumentNullException.ThrowIfNull(innerException);

        LockKey = lockKey;
        ExpirationTime = expirationTime;
    }

    /// <summary>
    /// Validates the lock key and builds the exception message.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lockKey"/> is <see langword="null"/>.</exception>
    private static string FormatMessage(string lockKey, DateTime expirationTime)
    {
        ArgumentNullException.ThrowIfNull(lockKey);

        return $"Lock '{lockKey}' has expired at {expirationTime:O}.";
    }
}
