// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Exceptions;

/// <summary>
/// Base exception for all distributed lock-related errors.
/// </summary>
public class DistributedLockException : Exception
{
    public DistributedLockException(string message) : base(message) { }

    public DistributedLockException(string message, Exception innerException)
        : base(message, innerException) { }
}
