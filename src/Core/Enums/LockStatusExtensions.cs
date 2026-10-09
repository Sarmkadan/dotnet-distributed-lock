#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Enums;

/// <summary>
/// Extension methods for <see cref="LockStatus"/>.
/// </summary>
public static class LockStatusExtensions
{
    /// <summary>
    /// Determines whether the status is terminal, meaning the lock's lifecycle has ended
    /// and it will not transition to another state.
    /// </summary>
    /// <param name="status">The lock status to check.</param>
    /// <returns>
    /// True for <see cref="LockStatus.Expired"/>, <see cref="LockStatus.Released"/>, and
    /// <see cref="LockStatus.Failed"/>; otherwise, false.
    /// </returns>
    public static bool IsTerminal(this LockStatus status) => status switch
    {
        LockStatus.Expired => true,
        LockStatus.Released => true,
        LockStatus.Failed => true,
        _ => false
    };

    /// <summary>
    /// Determines whether the status represents a live lock, meaning the lock is acquired,
    /// held, or being acquired or renewed.
    /// </summary>
    /// <param name="status">The lock status to check.</param>
    /// <returns>
    /// True for <see cref="LockStatus.Acquiring"/>, <see cref="LockStatus.Acquired"/>,
    /// <see cref="LockStatus.Held"/>, and <see cref="LockStatus.Renewing"/>; otherwise, false.
    /// </returns>
    /// <remarks>
    /// This differs from <c>Lock.IsActive()</c>, which only checks <see cref="LockStatus.Acquiring"/>
    /// and <see cref="LockStatus.Renewing"/>.
    /// </remarks>
    public static bool IsActive(this LockStatus status) => status switch
    {
        LockStatus.Acquiring => true,
        LockStatus.Acquired => true,
        LockStatus.Held => true,
        LockStatus.Renewing => true,
        _ => false
    };
}
