#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using SarmKadan.DistributedLock.Models;

namespace SarmKadan.DistributedLock.Repository;

/// <summary>
/// Defines the contract for lock storage and retrieval operations.
/// </summary>
public interface ILockRepository
{
    /// <summary>
    /// Acquires a new lock.
    /// </summary>
    /// <param name="lock">The lock to acquire.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the lock was acquired successfully; otherwise, false.</returns>
    Task<bool> AcquireAsync(Lock @lock, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a lock by key.
    /// </summary>
    /// <param name="key">The lock key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The lock if found; otherwise, null.</returns>
    Task<Lock?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a lock by key and owner.
    /// </summary>
    /// <param name="key">The lock key.</param>
    /// <param name="ownerId">The lock owner identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The lock if found; otherwise, null.</returns>
    Task<Lock?> GetByKeyAndOwnerAsync(string key, string ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing lock.
    /// </summary>
    /// <param name="lock">The lock to update.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the update was successful; otherwise, false.</returns>
    Task<bool> UpdateAsync(Lock @lock, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renews a lock (extends expiration).
    /// </summary>
    /// <param name="key">The lock key.</param>
    /// <param name="ownerId">The lock owner identifier.</param>
    /// <param name="newDuration">The new duration for the lock.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the renewal was successful; otherwise, false.</returns>
    Task<bool> RenewAsync(string key, string ownerId, TimeSpan newDuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a lock.
    /// </summary>
    /// <param name="key">The lock key.</param>
    /// <param name="ownerId">The lock owner identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the release was successful; otherwise, false.</returns>
    Task<bool> ReleaseAsync(string key, string ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a lock exists and is still valid.
    /// </summary>
    /// <param name="key">The lock key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the lock exists; otherwise, false.</returns>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all active locks.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of all active locks.</returns>
    Task<IEnumerable<Lock>> GetAllActiveLockAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves locks by owner.
    /// </summary>
    /// <param name="ownerId">The lock owner identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of locks owned by the specified owner.</returns>
    Task<IEnumerable<Lock>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes expired locks.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of deleted locks.</returns>
    Task<int> DeleteExpiredLockAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all locks.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of cleared locks.</returns>
    Task<int> ClearAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if a given fencing token is still the latest for a lock.
    /// </summary>
    /// <param name="key">The lock key.</param>
    /// <param name="fencingToken">The fencing token to validate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the token is valid; otherwise, false.</returns>
    Task<bool> ValidateFencingTokenAsync(string key, ulong fencingToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a point-in-time snapshot of locks that are currently expired.
    /// Intended for callers that need to inspect an expired lock's state (e.g. its
    /// <c>ExpiresAt</c> version) before deleting it, to avoid deleting a lock that
    /// gets renewed concurrently after the snapshot is taken.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of expired locks as observed at the time of the call.</returns>
    Task<IEnumerable<Lock>> GetExpiredLocksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a lock only if its current expiration timestamp still matches
    /// <paramref name="expectedExpiresAt"/>, acting as a compare-and-delete guard.
    /// If the lock was renewed (or otherwise updated) after being observed, the
    /// expiration timestamp will no longer match and the delete is skipped.
    /// </summary>
    /// <param name="key">The lock key.</param>
    /// <param name="expectedExpiresAt">The expiration timestamp observed when the lock was read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the lock still matched and was deleted; otherwise, false.</returns>
    Task<bool> DeleteLockIfExpirationMatchesAsync(string key, DateTime expectedExpiresAt, CancellationToken cancellationToken = default);
}
