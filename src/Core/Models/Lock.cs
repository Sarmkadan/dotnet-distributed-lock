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
    public string Key { get; set; }
    public string OwnerId { get; set; }
    public FencingToken? FencingToken { get; set; }
    public LockStatus Status { get; set; }
    public DateTime AcquiredAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RenewedAt { get; set; }
    public int RenewalCount { get; set; }
    public TimeSpan Duration { get; set; }
    public string? Metadata { get; set; }

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

    // Checks if the lock has expired
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    // Checks if the lock is still valid (acquired and not expired)
    public bool IsValid => Status == LockStatus.Held && !IsExpired;

    // Checks if the lock is close to expiration (within 25% of its lifetime)
    public bool IsCloseToExpiration
    {
        get
        {
            var remainingTime = ExpiresAt - DateTime.UtcNow;
            var thresholdTime = Duration.Multiply(0.25);
            return remainingTime < thresholdTime;
        }
    }

    // Renews the lock by extending its expiration time
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

    // Marks the lock as released
    public void Release()
    {
        Status = LockStatus.Released;
        ExpiresAt = DateTime.UtcNow;
    }

    // Validates ownership of the lock
    public void ValidateOwnership(string providedOwnerId)
    {
        if (OwnerId != providedOwnerId)
            throw new Exceptions.LockNotOwnedException(Key, OwnerId, providedOwnerId);
    }

    public override string ToString() =>
        $"Lock(Key={Key}, Owner={OwnerId}, Status={Status}, ExpiresAt={ExpiresAt:O}, IsValid={IsValid})";
}
