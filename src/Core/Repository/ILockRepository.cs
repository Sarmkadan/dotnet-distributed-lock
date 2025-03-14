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
    // Acquires a new lock
    Task<bool> AcquireAsync(Lock @lock, CancellationToken cancellationToken = default);

    // Retrieves a lock by key
    Task<Lock?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    // Retrieves a lock by key and owner
    Task<Lock?> GetByKeyAndOwnerAsync(string key, string ownerId, CancellationToken cancellationToken = default);

    // Updates an existing lock
    Task<bool> UpdateAsync(Lock @lock, CancellationToken cancellationToken = default);

    // Renews a lock (extends expiration)
    Task<bool> RenewAsync(string key, string ownerId, TimeSpan newDuration, CancellationToken cancellationToken = default);

    // Releases a lock
    Task<bool> ReleaseAsync(string key, string ownerId, CancellationToken cancellationToken = default);

    // Checks if a lock exists and is still valid
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    // Retrieves all active locks
    Task<IEnumerable<Lock>> GetAllActiveLockAsync(CancellationToken cancellationToken = default);

    // Retrieves locks by owner
    Task<IEnumerable<Lock>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default);

    // Deletes expired locks
    Task<int> DeleteExpiredLockAsync(CancellationToken cancellationToken = default);

    // Clears all locks (typically used for testing or reset scenarios)
    Task<int> ClearAllAsync(CancellationToken cancellationToken = default);
}
