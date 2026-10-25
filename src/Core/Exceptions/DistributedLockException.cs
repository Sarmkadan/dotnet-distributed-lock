#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Exceptions;

using SarmKadan.DistributedLock.Core.Exceptions;

/// <summary>
/// Base exception for all distributed lock-related errors.
/// </summary>
public class DistributedLockException : Exception
{
    public DistributedLockException(string message) : base(message)
    {
        ArgumentNullException.ThrowIfNull(message);
    }

    public DistributedLockException(string message, Exception? innerException)
        : base(message, innerException) { }

    /// <summary>
    /// Returns the base exception string followed by the lock-specific context (resource, owner, etc.)
    /// when the concrete exception type provides it.
    /// </summary>
    /// <returns>The exception string including lock context when available.</returns>
    public override string ToString()
    {
        var baseText = base.ToString();
        var context = GetLockContext();

        return string.IsNullOrEmpty(context)
            ? baseText
            : $"{baseText}{Environment.NewLine}Lock context: {context}";
    }

    private string? GetLockContext() => this switch
    {
        LockNotOwnedException e =>
            $"LockKey='{e.LockKey}', OwnerId='{e.OwnerId}', ProvidedOwnerId='{e.ProvidedOwnerId}'",
        LockAcquisitionException e =>
            $"LockKey='{e.LockKey}', Timeout={e.Timeout}, RetryCount={e.RetryCount}",
        LockExpiredException e =>
            $"LockKey='{e.LockKey}', ExpirationTime={e.ExpirationTime:O}",
        InvalidFencingTokenException e =>
            $"ProvidedToken='{e.ProvidedToken}', CurrentToken='{e.CurrentToken}'",
        LockRenewalFailedException e =>
            $"LockId='{e.LockId}'",
        _ => null
    };
}
