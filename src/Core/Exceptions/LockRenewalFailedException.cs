#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Core.Exceptions;

/// <summary>
/// Exception thrown when a distributed lock renewal operation fails.
/// </summary>
public class LockRenewalFailedException : DistributedLockException
{
    /// <summary>
    /// Gets the ID of the lock that failed to renew.
    /// </summary>
    public string LockId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LockRenewalFailedException"/> class.
    /// </summary>
    /// <param name="lockId">The ID of the lock that failed to renew.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public LockRenewalFailedException(string lockId, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        LockId = lockId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LockRenewalFailedException"/> class with a specified lock ID.
    /// </summary>
    /// <param name="lockId">The ID of the lock that failed to renew.</param>
    public LockRenewalFailedException(string lockId)
        : base($"Failed to renew lock with ID '{lockId}'.")
    {
        LockId = lockId;
    }
}
