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
public class InMemoryLockRepository : ILockRepository
{
    private readonly Dictionary<string, Lock> _locks = new();
    private readonly ReaderWriterLockSlim _lockSlim = new();

    public Task<bool> AcquireAsync(Lock @lock, CancellationToken cancellationToken = default)
    {
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

    public Task<Lock?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
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

    public Task<Lock?> GetByKeyAndOwnerAsync(string key, string ownerId, CancellationToken cancellationToken = default)
    {
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

    public Task<bool> UpdateAsync(Lock @lock, CancellationToken cancellationToken = default)
    {
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

    public Task<bool> RenewAsync(string key, string ownerId, TimeSpan newDuration, CancellationToken cancellationToken = default)
    {
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

    public Task<bool> ReleaseAsync(string key, string ownerId, CancellationToken cancellationToken = default)
    {
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

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
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

    public Task<IEnumerable<Lock>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default)
    {
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
}
