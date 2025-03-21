#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Enums;

/// <summary>
/// Defines the strategy for acquiring a distributed lock.
/// </summary>
public enum AcquisitionMode
{
    /// <summary>
    /// Fail immediately if the lock cannot be acquired.
    /// </summary>
    NonBlocking = 0,

    /// <summary>
    /// Wait indefinitely until the lock is acquired.
    /// </summary>
    Blocking = 1,

    /// <summary>
    /// Wait with exponential backoff and retry logic.
    /// </summary>
    ExponentialBackoff = 2,

    /// <summary>
    /// Wait with linear backoff strategy.
    /// </summary>
    LinearBackoff = 3
}
