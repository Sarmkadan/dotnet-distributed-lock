#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using SarmKadan.DistributedLock.Models;

namespace SarmKadan.DistributedLock.Services;

/// <summary>
/// Defines the contract for distributed lock operations.
/// </summary>
public interface ILockService
{
    // Attempts to acquire a lock
    Task<(bool Success, Lock? Lock, string? ErrorMessage)> TryAcquireAsync(
        string lockKey,
        string ownerId,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default
    );

    // Acquires a lock with retry logic
    Task<Lock> AcquireAsync(
        string lockKey,
        string ownerId,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default
    );

    // Renews an existing lock
    Task<bool> RenewAsync(
        string lockKey,
        string ownerId,
        TimeSpan? newDuration = null,
        CancellationToken cancellationToken = default
    );

    // Releases a lock
    Task<bool> ReleaseAsync(
        string lockKey,
        string ownerId,
        CancellationToken cancellationToken = default
    );

    // Retrieves lock information
    Task<Lock?> GetLockAsync(string lockKey, CancellationToken cancellationToken = default);

    // Checks if a lock is currently held
    Task<bool> IsLockedAsync(string lockKey, CancellationToken cancellationToken = default);

    // Gets all active locks
    Task<IEnumerable<Lock>> GetAllActiveLockAsync(CancellationToken cancellationToken = default);
}
