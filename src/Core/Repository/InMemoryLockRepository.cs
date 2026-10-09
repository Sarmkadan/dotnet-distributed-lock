#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using SarmKadan.DistributedLock.Models;

namespace SarmKadan.DistributedLock.Repository;

/// <summary>
/// In-memory implementation of ILockRepository for testing and development.
/// Not suitable for production distributed locking.
/// </summary>
public sealed class InMemoryLockRepository : ILockRepository
{
    private readonly Dictionary<string, Lock> _locks = new();
    private readonly ReaderWriterLockSlim _lockSlim = new();

    /// <summary>
    /// Attempts to acquire the specified lock.
    /// Succeeds when no lock with the same key is stored or the stored lock has expired.
    /// On success, the lock status is set to <see cref="Enums.LockStatus.Acquired"/> and the lock is stored.
    /// </summary>
    /// <param name="lock">The lock to acquire.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the lock was acquired; false if an unexpired lock with the same key is held.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lock"/> is <see langword="null"/>.</exception>
    public Task<bool> AcquireAsync(Lock @lock, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@lock);
        _lockSlim.EnterWriteLock();
        try
        {
            if (_locks.ContainsKey(@lock.Key))
            {
                var existing = _locks[@lock.Key];
                if (!existing.IsExpired)
                    return Task.FromResult(false);
            }

            @lock.Status = Enums.LockStatus.Acquired;
            _locks[@lock.Key] = @lock;
            return Task.FromResult(true);
        }
        finally
        {
            _lockSlim.ExitWriteLock();
        }
    }

    /// <summary>
    /// Retrieves the lock stored under the specified key.
    /// If the stored lock has expired, it is removed from storage and <see langword="null"/> is returned.
    /// </summary>
    /// <param name="key">The lock key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The unexpired lock if found; otherwise, <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty.</exception>
    public Task<Lock?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        _lockSlim.EnterReadLock();
        try
        {
            if (_locks.TryGetValue(key, out var @lock))
            {
                if (!@lock.IsExpired)
                    return Task.FromResult(@lock)!;

                _lockSlim.ExitReadLock();
                _lockSlim.EnterWriteLock();
                try
                {
                    _locks.Remove(key);
                }
                finally
                {
                    _lockSlim.ExitWriteLock();
                }
                return Task.FromResult<Lock?>(null);
            }
            return Task.FromResult<Lock?>(null);
        }
        finally
        {
            if (_lockSlim.IsReadLockHeld)
                _lockSlim.ExitReadLock();
        }
    }

    /// <summary>
    /// Retrieves the unexpired lock stored under the specified key, but only if it is owned by the specified owner.
    /// </summary>
    /// <param name="key">The lock key.</param>
    /// <param name="ownerId">The lock owner identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The lock if found, unexpired, and owned by <paramref name="ownerId"/>; otherwise, <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="ownerId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> or <paramref name="ownerId"/> is empty.</exception>
    public Task<Lock?> GetByKeyAndOwnerAsync(string key, string ownerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentException.ThrowIfNullOrEmpty(ownerId);
        _lockSlim.EnterReadLock();
        try
        {
            if (_locks.TryGetValue(key, out var @lock) && @lock.OwnerId == ownerId && !@lock.IsExpired)
                return Task.FromResult(@lock)!;
            return Task.FromResult<Lock?>(null);
        }
        finally
        {
            _lockSlim.ExitReadLock();
        }
    }

    /// <summary>
    /// Replaces the stored lock that has the same key as the specified lock.
    /// Does nothing if no lock with that key is stored.
    /// </summary>
    /// <param name="lock">The lock containing the new state.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the stored lock was replaced; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lock"/> is <see langword="null"/>.</exception>
    public Task<bool> UpdateAsync(Lock @lock, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@lock);
        _lockSlim.EnterWriteLock();
        try
        {
            if (_locks.ContainsKey(@lock.Key))
            {
                _locks[@lock.Key] = @lock;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
        finally
        {
            _lockSlim.ExitWriteLock();
        }
    }

    /// <summary>
    /// Extends the duration of the lock stored under the specified key, if it is owned by the specified owner.
    /// The stored lock is renewed whenever the owner matches, regardless of whether it has already expired.
    /// </summary>
    /// <param name="key">The lock key.</param>
    /// <param name="ownerId">The lock owner identifier.</param>
    /// <param name="newDuration">The new duration for the lock.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the lock was renewed; false if no lock with the key is owned by <paramref name="ownerId"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="ownerId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> or <paramref name="ownerId"/> is empty.</exception>
    public Task<bool> RenewAsync(string key, string ownerId, TimeSpan newDuration, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentException.ThrowIfNullOrEmpty(ownerId);
        _lockSlim.EnterWriteLock();
        try
        {
            if (_locks.TryGetValue(key, out var @lock) && @lock.OwnerId == ownerId)
            {
                @lock.Renew(newDuration);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
        finally
        {
            _lockSlim.ExitWriteLock();
        }
    }

    /// <summary>
    /// Releases the lock stored under the specified key, if it is owned by the specified owner.
    /// The lock is removed from storage.
    /// </summary>
    /// <param name="key">The lock key.</param>
    /// <param name="ownerId">The lock owner identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the lock was released; false if no lock with the key is owned by <paramref name="ownerId"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="ownerId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> or <paramref name="ownerId"/> is empty.</exception>
    public Task<bool> ReleaseAsync(string key, string ownerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentException.ThrowIfNullOrEmpty(ownerId);
        _lockSlim.EnterWriteLock();
        try
        {
            if (_locks.TryGetValue(key, out var @lock) && @lock.OwnerId == ownerId)
            {
                @lock.Release();
                _locks.Remove(key);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
        finally
        {
            _lockSlim.ExitWriteLock();
        }
    }

    /// <summary>
    /// Determines whether an unexpired lock is stored under the specified key.
    /// </summary>
    /// <param name="key">The lock key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if an unexpired lock with the key exists; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty.</exception>
    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        _lockSlim.EnterReadLock();
        try
        {
            return Task.FromResult(_locks.ContainsKey(key) && !_locks[key].IsExpired);
        }
        finally
        {
            _lockSlim.ExitReadLock();
        }
    }

    /// <summary>
    /// Retrieves a snapshot of all unexpired locks.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of all active locks at the time of the call.</returns>
    public Task<IEnumerable<Lock>> GetAllActiveLockAsync(CancellationToken cancellationToken = default)
    {
        _lockSlim.EnterReadLock();
        try
        {
            var activeLocks = _locks.Values.Where(l => !l.IsExpired).ToList();
            return Task.FromResult(activeLocks.AsEnumerable());
        }
        finally
        {
            _lockSlim.ExitReadLock();
        }
    }

    /// <summary>
    /// Retrieves a snapshot of all unexpired locks owned by the specified owner.
    /// </summary>
    /// <param name="ownerId">The lock owner identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of unexpired locks owned by <paramref name="ownerId"/> at the time of the call.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ownerId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="ownerId"/> is empty.</exception>
    public Task<IEnumerable<Lock>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(ownerId);
        _lockSlim.EnterReadLock();
        try
        {
            var ownerLocks = _locks.Values.Where(l => l.OwnerId == ownerId && !l.IsExpired).ToList();
            return Task.FromResult(ownerLocks.AsEnumerable());
        }
        finally
        {
            _lockSlim.ExitReadLock();
        }
    }

    /// <summary>
    /// Removes all expired locks from storage.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of expired locks that were removed.</returns>
    public Task<int> DeleteExpiredLockAsync(CancellationToken cancellationToken = default)
    {
        _lockSlim.EnterWriteLock();
        try
        {
            var expiredKeys = _locks.Where(kvp => kvp.Value.IsExpired).Select(kvp => kvp.Key).ToList();
            foreach (var key in expiredKeys)
                _locks.Remove(key);
            return Task.FromResult(expiredKeys.Count);
        }
        finally
        {
            _lockSlim.ExitWriteLock();
        }
    }

    /// <inheritdoc/>
    public Task<IEnumerable<Lock>> GetExpiredLocksAsync(CancellationToken cancellationToken = default)
    {
        _lockSlim.EnterReadLock();
        try
        {
            var expiredLocks = _locks.Values.Where(l => l.IsExpired).ToList();
            return Task.FromResult(expiredLocks.AsEnumerable());
        }
        finally
        {
            _lockSlim.ExitReadLock();
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null or empty.</exception>
    public Task<bool> DeleteLockIfExpirationMatchesAsync(string key, DateTime expectedExpiresAt, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        _lockSlim.EnterWriteLock();
        try
        {
            if (_locks.TryGetValue(key, out var current) && current.ExpiresAt == expectedExpiresAt)
            {
                _locks.Remove(key);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
        finally
        {
            _lockSlim.ExitWriteLock();
        }
    }

    /// <summary>
    /// Removes all locks from storage, including unexpired ones.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of locks that were removed.</returns>
    public Task<int> ClearAllAsync(CancellationToken cancellationToken = default)
    {
        _lockSlim.EnterWriteLock();
        try
        {
            int count = _locks.Count;
            _locks.Clear();
            return Task.FromResult(count);
        }
        finally
        {
            _lockSlim.ExitWriteLock();
        }
    }

    /// <summary>
    /// Validates that the specified fencing token matches the current, unexpired lock stored under the key.
    /// </summary>
    /// <param name="key">The lock key.</param>
    /// <param name="fencingToken">The fencing token to validate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if an unexpired lock exists and its fencing token sequence number equals <paramref name="fencingToken"/>; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty.</exception>
    public Task<bool> ValidateFencingTokenAsync(string key, ulong fencingToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        _lockSlim.EnterReadLock();
        try
        {
            if (_locks.TryGetValue(key, out var @lock))
            {
                // If lock exists, is not expired, and fencing token matches
                return Task.FromResult(!@lock.IsExpired && @lock.FencingToken?.SequenceNumber == (long)fencingToken);
            }
            return Task.FromResult(false);
        }
        finally
        {
            _lockSlim.ExitReadLock();
        }
    }
}