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
    public string LockKey { get; }
    public DateTime ExpirationTime { get; }

    public LockExpiredException(string lockKey, DateTime expirationTime)
        : base($"Lock '{lockKey}' has expired at {expirationTime:O}.")
    {
        LockKey = lockKey;
        ExpirationTime = expirationTime;
    }

    public LockExpiredException(string lockKey, DateTime expirationTime, Exception innerException)
        : base($"Lock '{lockKey}' has expired at {expirationTime:O}.", innerException)
    {
        LockKey = lockKey;
        ExpirationTime = expirationTime;
    }
}
