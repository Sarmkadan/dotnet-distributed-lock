#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using SarmKadan.DistributedLock.Enums;

namespace SarmKadan.DistributedLock.Models;

/// <summary>
/// Represents a distributed lock with metadata about ownership, expiration, and status.
/// </summary>
public class Lock
{
    /// <summary>
    /// The unique identifier for the lock key.
    /// </summary>
    public string Key { get; set; }
    
    /// <summary>
    /// The identifier for the lock owner.
    /// </summary>
    public string OwnerId { get; set; }
    
    /// <summary>
    /// The fencing token associated with the lock (optional).
    /// </summary>
    public FencingToken? FencingToken { get; set; }
    
    /// <summary>
    /// The current status of the lock.
    /// </summary>
    public LockStatus Status { get; set; }
    
    /// <summary>
    /// The timestamp when the lock was acquired.
    /// </summary>
    public DateTime AcquiredAt { get; set; }
    
    /// <summary>
    /// The timestamp when the lock expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }
    
    /// <summary>
    /// The timestamp when the lock was last renewed (optional).
    /// </summary>
    public DateTime? RenewedAt { get; set; }
    
    /// <summary>
    /// The number of times the lock has been renewed.
    /// </summary>
    public int RenewalCount { get; set; }
    
    /// <summary>
    /// The duration for which the lock is held.
    /// </summary>
    public TimeSpan Duration { get; set; }
    
    /// <summary>
    /// Optional metadata associated with the lock.
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Lock"/> class.
    /// </summary>
    public Lock()
    {
        Key = string.Empty;
        OwnerId = string.Empty;
        Status = LockStatus.Unknown;
        AcquiredAt = DateTime.UtcNow;
        ExpiresAt = DateTime.UtcNow.AddSeconds(Constants.LockConstants.DefaultLockTimeoutSeconds);
        Duration = Constants.LockConstants.DefaultLockTimeout;
        RenewalCount = 0;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Lock"/> class with specified parameters.
    /// </summary>
    /// <param name="key">The unique lock key.</param>
    /// <param name="ownerId">The unique owner identifier.</param>
    /// <param name="duration">The duration to hold the lock.</param>
    public Lock(string key, string ownerId, TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Lock key cannot be null or empty.", nameof(key));

        if (string.IsNullOrWhiteSpace(ownerId))
            throw new ArgumentException("Owner ID cannot be null or empty.", nameof(ownerId));

        if (duration.TotalSeconds < Constants.LockConstants.MinimumLockTimeoutSeconds)
            throw new ArgumentException(
                $"Duration must be at least {Constants.LockConstants.MinimumLockTimeoutSeconds}s.",
                nameof(duration)
            );

        Key = key;
        OwnerId = ownerId;
        Duration = duration;
        Status = LockStatus.Acquiring;
        AcquiredAt = DateTime.UtcNow;
        ExpiresAt = AcquiredAt.Add(duration);
        RenewalCount = 0;
    }

    /// <summary>
    /// Checks if the lock has expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    /// <summary>
    /// Checks if the lock is still valid (acquired and not expired).
    /// </summary>
    public bool IsValid => Status == LockStatus.Held && !IsExpired;

    /// <summary>
    /// Checks if the lock is close to expiration (within 25% of its lifetime).
    /// </summary>
    public bool IsCloseToExpiration
    {
        get
        {
            var remainingTime = ExpiresAt - DateTime.UtcNow;
            var thresholdTime = Duration.Multiply(0.25);
            return remainingTime < thresholdTime;
        }
    }

    /// <summary>
    /// Renews the lock by extending its expiration time.
    /// </summary>
    /// <param name="newDuration">The new duration for the lock (optional).</param>
    /// <exception cref="Exceptions.LockExpiredException">Thrown if the lock is already expired.</exception>
    public void Renew(TimeSpan? newDuration = null)
    {
        if (IsExpired)
            throw new Exceptions.LockExpiredException(Key, ExpiresAt);

        var duration = newDuration ?? Duration;
        ExpiresAt = DateTime.UtcNow.Add(duration);
        RenewedAt = DateTime.UtcNow;
        RenewalCount++;
        Status = LockStatus.Held;
    }

    /// <summary>
    /// Marks the lock as released.
    /// </summary>
    public void Release()
    {
        Status = LockStatus.Released;
        ExpiresAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Validates ownership of the lock.
    /// </summary>
    /// <param name="providedOwnerId">The owner ID to validate.</param>
    /// <exception cref="Exceptions.LockNotOwnedException">Thrown if the provided owner ID does not match.</exception>
    public void ValidateOwnership(string providedOwnerId)
    {
        if (OwnerId != providedOwnerId)
            throw new Exceptions.LockNotOwnedException(Key, OwnerId, providedOwnerId);
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"Lock(Key={Key}, Owner={OwnerId}, Status={Status}, ExpiresAt={ExpiresAt:O}, IsValid={IsValid})";
}
