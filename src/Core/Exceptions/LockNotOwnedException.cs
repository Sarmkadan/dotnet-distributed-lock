#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Exceptions;

/// <summary>
/// Thrown when attempting to release or renew a lock that is not owned by the caller.
/// </summary>
public class LockNotOwnedException : DistributedLockException
{
    public string LockKey { get; }
    public string OwnerId { get; }
    public string ProvidedOwnerId { get; }

    public LockNotOwnedException(string lockKey, string ownerId, string providedOwnerId)
        : base($"Lock '{lockKey}' is owned by '{ownerId}', not '{providedOwnerId}'.")
    {
        LockKey = lockKey;
        OwnerId = ownerId;
        ProvidedOwnerId = providedOwnerId;
    }

    public LockNotOwnedException(string lockKey, string ownerId, string providedOwnerId, Exception innerException)
        : base($"Lock '{lockKey}' is owned by '{ownerId}', not '{providedOwnerId}'.", innerException)
    {
        LockKey = lockKey;
        OwnerId = ownerId;
        ProvidedOwnerId = providedOwnerId;
    }
}
