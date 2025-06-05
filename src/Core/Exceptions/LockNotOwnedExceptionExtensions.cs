#nullable enable

// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;

namespace SarmKadan.DistributedLock.Exceptions;

/// <summary>
/// Provides extension methods for <see cref="LockNotOwnedException"/> to enhance error handling and diagnostic capabilities.
/// </summary>
public static class LockNotOwnedExceptionExtensions
{
    /// <summary>
    /// Creates a detailed error message that includes both the exception details and suggested corrective actions.
    /// </summary>
    /// <param name="exception">The exception instance.</param>
    /// <returns>A formatted error message with actionable guidance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception"/> is null.</exception>
    public static string ToDetailedErrorMessage(this LockNotOwnedException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return $"""
Lock ownership conflict detected:

Lock Key:      {exception.LockKey}
Correct Owner:  {exception.OwnerId}
Provided Owner: {exception.ProvidedOwnerId}

Error Details:  {exception.Message}

Suggested Actions:
1. Verify the lock key matches the intended resource
2. Ensure you are using the correct owner identifier
3. Check if the lock was released by another process
4. Consider using a retry policy with exponential backoff for lock acquisition

If this is unexpected, investigate concurrent access patterns or potential race conditions in your code.
""";
    }

    /// <summary>
    /// Determines whether the provided owner ID matches the actual lock owner.
    /// </summary>
    /// <param name="exception">The exception instance.</param>
    /// <param name="expectedOwnerId">The expected owner ID to compare against.</param>
    /// <returns>True if the IDs match; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception"/> is null.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expectedOwnerId"/> is null.</exception>
    public static bool IsOwnerMismatch(this LockNotOwnedException exception, string expectedOwnerId)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(expectedOwnerId);

        return string.Equals(exception.OwnerId, expectedOwnerId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Gets a collection of suggested owner IDs that might be valid for this lock.
    /// Useful for debugging scenarios where the correct owner is unknown.
    /// </summary>
    /// <param name="exception">The exception instance.</param>
    /// <returns>An enumerable of suggested owner IDs.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception"/> is null.</exception>
    public static IEnumerable<string> GetSuggestedOwnerIds(this LockNotOwnedException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        yield return exception.OwnerId;
        yield return exception.ProvidedOwnerId;
        yield return Guid.NewGuid().ToString("N");
        yield return $"lock-{exception.LockKey.GetHashCode():X8}";
    }

    /// <summary>
    /// Creates a new exception instance with updated owner information.
    /// Useful for rethrowing with corrected context.
    /// </summary>
    /// <param name="exception">The original exception instance.</param>
    /// <param name="newOwnerId">The corrected owner ID to use in the new exception.</param>
    /// <returns>A new <see cref="LockNotOwnedException"/> with updated owner information.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception"/> is null.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="newOwnerId"/> is null.</exception>
    public static LockNotOwnedException WithCorrectedOwner(this LockNotOwnedException exception, string newOwnerId)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(newOwnerId);

        return new LockNotOwnedException(
            exception.LockKey,
            exception.OwnerId,
            newOwnerId
        );
    }
}