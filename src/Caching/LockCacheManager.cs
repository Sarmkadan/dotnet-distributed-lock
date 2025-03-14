// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Caching;

using System.Collections.Concurrent;
using SarmKadan.DistributedLock.Core.Models;
using SarmKadan.DistributedLock.Utilities.Extensions;

/// <summary>
/// In-memory cache manager for lock data.
/// Reduces backend load by caching frequently accessed locks with configurable TTL.
/// Implements automatic eviction of expired entries and size-based cleanup.
/// </summary>
public interface ILockCacheManager
{
    Task<Lock?> GetAsync(string lockId);
    Task SetAsync(Lock @lock);
    Task RemoveAsync(string lockId);
    Task<List<Lock>> GetAllAsync();
    Task ClearAsync();
    CacheStatistics GetStatistics();
}

/// <summary>
/// In-memory implementation using ConcurrentDictionary.
/// Thread-safe and suitable for single-instance deployments.
/// </summary>
public class InMemoryLockCacheManager : ILockCacheManager
{
    private readonly ConcurrentDictionary<string, CachedLock> _cache;
    private readonly CacheConfiguration _config;
    private readonly ReaderWriterLockSlim _statsLock = new();
    private long _hits;
    private long _misses;

    public InMemoryLockCacheManager(CacheConfiguration? config = null)
    {
        _config = config ?? new CacheConfiguration();
        _cache = new ConcurrentDictionary<string, CachedLock>();
    }

    public async Task<Lock?> GetAsync(string lockId)
    {
        if (string.IsNullOrEmpty(lockId))
            return null;

        if (_cache.TryGetValue(lockId, out var cachedLock))
        {
            // Check if entry has expired
            if (cachedLock.IsExpired(_config.TtlSeconds))
            {
                _cache.TryRemove(lockId, out _);
                IncrementMiss();
                return null;
            }

            IncrementHit();
            cachedLock.LastAccessTime = DateTime.UtcNow;
            return cachedLock.Lock;
        }

        IncrementMiss();
        return null;
    }

    public async Task SetAsync(Lock @lock)
    {
        if (@lock == null)
            return;

        // Enforce size limit by removing least recently used entries
        if (_cache.Count >= _config.MaxCacheSize)
        {
            EvictLRU();
        }

        var cachedLock = new CachedLock
        {
            Lock = @lock,
            CachedAt = DateTime.UtcNow,
            LastAccessTime = DateTime.UtcNow
        };

        _cache.AddOrUpdate(@lock.Id, cachedLock, (_, __) => cachedLock);
    }

    public async Task RemoveAsync(string lockId)
    {
        if (!string.IsNullOrEmpty(lockId))
        {
            _cache.TryRemove(lockId, out _);
        }
    }

    public async Task<List<Lock>> GetAllAsync()
    {
        CleanExpiredEntries();

        return _cache.Values
            .Select(cl => cl.Lock)
            .Where(l => l != null)
            .ToList();
    }

    public async Task ClearAsync()
    {
        _cache.Clear();
        ResetStatistics();
    }

    public CacheStatistics GetStatistics()
    {
        _statsLock.EnterReadLock();
        try
        {
            var total = _hits + _misses;
            var hitRate = total > 0 ? (double)_hits / total * 100 : 0;

            return new CacheStatistics
            {
                CachedItems = _cache.Count,
                Hits = _hits,
                Misses = _misses,
                HitRate = hitRate,
                Timestamp = DateTime.UtcNow
            };
        }
        finally
        {
            _statsLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Removes least recently used entry when cache is full.
    /// </summary>
    private void EvictLRU()
    {
        var oldest = _cache.Values
            .OrderBy(x => x.LastAccessTime)
            .FirstOrDefault();

        if (oldest != null && oldest.Lock != null)
        {
            _cache.TryRemove(oldest.Lock.Id, out _);
        }
    }

    /// <summary>
    /// Removes all expired entries from the cache.
    /// </summary>
    private void CleanExpiredEntries()
    {
        var expired = _cache
            .Where(x => x.Value.IsExpired(_config.TtlSeconds))
            .Select(x => x.Key)
            .ToList();

        foreach (var key in expired)
        {
            _cache.TryRemove(key, out _);
        }
    }

    private void IncrementHit()
    {
        _statsLock.EnterWriteLock();
        try { _hits++; }
        finally { _statsLock.ExitWriteLock(); }
    }

    private void IncrementMiss()
    {
        _statsLock.EnterWriteLock();
        try { _misses++; }
        finally { _statsLock.ExitWriteLock(); }
    }

    private void ResetStatistics()
    {
        _statsLock.EnterWriteLock();
        try
        {
            _hits = 0;
            _misses = 0;
        }
        finally
        {
            _statsLock.ExitWriteLock();
        }
    }

    private class CachedLock
    {
        public required Lock Lock { get; init; }
        public DateTime CachedAt { get; init; }
        public DateTime LastAccessTime { get; set; }

        public bool IsExpired(int ttlSeconds) =>
            (DateTime.UtcNow - CachedAt).TotalSeconds > ttlSeconds;
    }
}

/// <summary>
/// Configuration for cache behavior.
/// </summary>
public class CacheConfiguration
{
    /// <summary>
    /// Time-to-live for cached entries in seconds (default: 5 minutes).
    /// </summary>
    public int TtlSeconds { get; set; } = 300;

    /// <summary>
    /// Maximum number of items to keep in cache.
    /// </summary>
    public int MaxCacheSize { get; set; } = 10000;

    /// <summary>
    /// Enable compression for cached values.
    /// Reduces memory usage for large lock data.
    /// </summary>
    public bool EnableCompression { get; set; } = false;
}

/// <summary>
/// Cache statistics for monitoring performance.
/// </summary>
public record CacheStatistics
{
    public int CachedItems { get; init; }
    public long Hits { get; init; }
    public long Misses { get; init; }
    public double HitRate { get; init; }
    public DateTime Timestamp { get; init; }
}
