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
    /// <summary>
    /// Attempts to acquire a lock in a non-blocking manner.
    /// </summary>
    /// <param name="lockKey">The unique identifier for the resource.</param>
    /// <param name="ownerId">The unique identifier for the lock owner.</param>
    /// <param name="duration">The optional duration to hold the lock.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A tuple indicating success, the lock instance (if successful), and an error message (if failed).</returns>
    Task<(bool Success, Lock? Lock, string? ErrorMessage)> TryAcquireAsync(
        string lockKey,
        string ownerId,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Acquires a lock with retry logic, blocking until successful or timeout reached.
    /// </summary>
    /// <param name="lockKey">The unique identifier for the resource.</param>
    /// <param name="ownerId">The unique identifier for the lock owner.</param>
    /// <param name="duration">The optional duration to hold the lock.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The acquired lock.</returns>
    /// <exception cref="LockAcquisitionException">Thrown if the lock cannot be acquired.</exception>
    Task<Lock> AcquireAsync(
        string lockKey,
        string ownerId,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Renews an existing lock.
    /// </summary>
    /// <param name="lockKey">The unique identifier for the resource.</param>
    /// <param name="ownerId">The unique identifier for the lock owner.</param>
    /// <param name="newDuration">The optional new duration to hold the lock.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the renewal was successful; otherwise, false.</returns>
    Task<bool> RenewAsync(
        string lockKey,
        string ownerId,
        TimeSpan? newDuration = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Renews an existing lock using its fencing token, guaranteeing the caller still holds
    /// the most recently issued token before extending the lock's duration.
    /// </summary>
    /// <param name="lockKey">The unique identifier for the resource.</param>
    /// <param name="fencingToken">The fencing token previously issued for this lock.</param>
    /// <param name="newDuration">The new duration to hold the lock.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The renewed lock.</returns>
    /// <exception cref="SarmKadan.DistributedLock.Exceptions.InvalidFencingTokenException">Thrown if the fencing token is no longer valid.</exception>
    Task<Lock> RenewLockAsync(
        string lockKey,
        ulong fencingToken,
        TimeSpan newDuration,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Releases a lock.
    /// </summary>
    /// <param name="lockKey">The unique identifier for the resource.</param>
    /// <param name="ownerId">The unique identifier for the lock owner.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the lock was successfully released; otherwise, false.</returns>
    Task<bool> ReleaseAsync(
        string lockKey,
        string ownerId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Retrieves lock information.
    /// </summary>
    /// <param name="lockKey">The unique identifier for the resource.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The lock information, or null if the lock does not exist.</returns>
    Task<Lock?> GetLockAsync(string lockKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a lock is currently held.
    /// </summary>
    /// <param name="lockKey">The unique identifier for the resource.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the lock is currently held; otherwise, false.</returns>
    Task<bool> IsLockedAsync(string lockKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active locks.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of all currently active locks.</returns>
    Task<IEnumerable<Lock>> GetAllActiveLockAsync(CancellationToken cancellationToken = default);
}
