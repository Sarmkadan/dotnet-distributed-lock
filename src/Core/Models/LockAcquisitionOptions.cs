#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =====================================================================

namespace SarmKadan.DistributedLock.Models;

/// <summary>
/// Options for acquiring a distributed lock with automatic renewal support.
/// </summary>
public class LockAcquisitionOptions
{
    /// <summary>
    /// Gets or sets whether automatic lock renewal (heartbeat) should be enabled.
    /// Defaults to true.
    /// </summary>
    public bool EnableAutoRenewal { get; set; } = true;

    /// <summary>
    /// Gets or sets the renewal interval fraction (e.g., 0.33 for 1/3 of lock duration).
    /// The lock will be renewed at this interval to prevent expiration.
    /// Defaults to 0.33 (renew at 1/3 of lock duration).
    /// </summary>
    /// <remarks>
    /// A value of 0.33 means renew every 10 seconds for a 30-second lock.
    /// Must be between 0.01 and 0.99.
    /// </remarks>
    public double RenewalFraction { get; set; } = 0.33;

    /// <summary>
    /// Gets or sets the maximum number of renewal attempts before giving up.
    /// Defaults to 3.
    /// </summary>
    public int MaxRenewals { get; set; } = 3;

    /// <summary>
    /// Validates the options.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if options are invalid.</exception>
    public void Validate()
    {
        if (RenewalFraction <= 0.01 || RenewalFraction >= 0.99)
        {
            throw new ArgumentException(
                $"RenewalFraction must be between 0.01 and 0.99, but was {RenewalFraction}.",
                nameof(RenewalFraction)
            );
        }

        if (MaxRenewals < 0)
        {
            throw new ArgumentException(
                $"MaxRenewals must be non-negative, but was {MaxRenewals}.",
                nameof(MaxRenewals)
            );
        }
    }
}
