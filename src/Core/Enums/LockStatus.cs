// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Enums;

/// <summary>
/// Represents the current status of a distributed lock.
/// </summary>
public enum LockStatus
{
    /// <summary>
    /// Lock acquisition is in progress.
    /// </summary>
    Acquiring = 0,

    /// <summary>
    /// Lock has been successfully acquired.
    /// </summary>
    Acquired = 1,

    /// <summary>
    /// Lock is currently held by the owner.
    /// </summary>
    Held = 2,

    /// <summary>
    /// Lock renewal is in progress.
    /// </summary>
    Renewing = 3,

    /// <summary>
    /// Lock has expired and is no longer valid.
    /// </summary>
    Expired = 4,

    /// <summary>
    /// Lock release is in progress.
    /// </summary>
    Releasing = 5,

    /// <summary>
    /// Lock has been released.
    /// </summary>
    Released = 6,

    /// <summary>
    /// Lock acquisition or renewal failed.
    /// </summary>
    Failed = 7,

    /// <summary>
    /// Lock is in an unknown or error state.
    /// </summary>
    Unknown = 8
}
