#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Caching;

using System.Collections.Concurrent;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Utilities.Extensions;

/// <summary>
/// In-memory cache manager for lock data.
/// Reduces backend load by caching frequently accessed locks with configurable TTL.
/// Implements automatic eviction of expired entries and size-based cleanup.
/// </summary>
public interface ILockCacheManager
{
    /// <summary>
    /// Gets a cached lock by its identifier.
    /// Returns <c>null</c> on a cache miss or when the entry has expired.
    /// </summary>
    /// <param name="lockId">The lock identifier (key). Must not be null or empty.</param>
    /// <returns>The cached lock, or <c>null</c> if not found or expired.</returns>
    Task<Lock?> GetAsync(string lockId);

    /// <summary>
    /// Adds or updates a lock in the cache, replacing any existing entry with the same key.
    /// May evict the least recently used entry when the cache is full.
    /// </summary>
    /// <param name="lock">The lock to cache. Must not be null.</param>
    Task SetAsync(Lock @lock);

    /// <summary>
    /// Removes a lock from the cache. Does nothing if the lock is not cached.
    /// </summary>
    /// <param name="lockId">The lock identifier (key). Must not be null or empty.</param>
    Task RemoveAsync(string lockId);

    /// <summary>
    /// Gets all non-expired locks currently held in the cache.
    /// Expired entries are purged before the result is returned.
    /// </summary>
    /// <returns>A new list containing the cached locks.</returns>
    Task<List<Lock>> GetAllAsync();

    /// <summary>
    /// Removes all entries from the cache and resets hit/miss statistics.
    /// </summary>
    Task ClearAsync();

    /// <summary>
    /// Gets a snapshot of the cache statistics.
    /// </summary>
    /// <returns>Current item count, hit/miss counters, hit rate and the snapshot timestamp (UTC).</returns>
    CacheStatistics GetStatistics();
}

/// <summary>
/// In-memory implementation using ConcurrentDictionary.
/// Thread-safe and suitable for single-instance deployments.
/// </summary>
public sealed class InMemoryLockCacheManager : ILockCacheManager
{
    private readonly ConcurrentDictionary<string, CachedLock> _cache;
    private readonly CacheConfiguration _config;
    private readonly ReaderWriterLockSlim _statsLock = new();
    private long _hits;
    private long _misses;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryLockCacheManager"/> class.
    /// </summary>
    /// <param name="config">Cache behavior settings. When <c>null</c>, default <see cref="CacheConfiguration"/> values are used.</param>
    public InMemoryLockCacheManager(CacheConfiguration? config = null)
    {
        _config = config ?? new CacheConfiguration();
        _cache = new ConcurrentDictionary<string, CachedLock>();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// An expired entry is removed on access and counted as a miss.
    /// A successful lookup refreshes the entry's last access time used for LRU eviction.
    /// </remarks>
    public async Task<Lock?> GetAsync(string lockId)
    {
        ArgumentException.ThrowIfNullOrEmpty(lockId);

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

    /// <inheritdoc/>
    public async Task SetAsync(Lock @lock)
    {
        ArgumentNullException.ThrowIfNull(@lock);

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

        _cache.AddOrUpdate(@lock.Key, cachedLock, (_, __) => cachedLock);
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string lockId)
    {
        ArgumentException.ThrowIfNullOrEmpty(lockId);

        _cache.TryRemove(lockId, out _);
    }

    /// <inheritdoc/>
    public async Task<List<Lock>> GetAllAsync()
    {
        CleanExpiredEntries();

        return _cache.Values
            .Select(cl => cl.Lock)
            .Where(l => l is not null)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task ClearAsync()
    {
        _cache.Clear();
        ResetStatistics();
    }

    /// <inheritdoc/>
    public CacheStatistics GetStatistics()
    {
        _statsLock.EnterReadLock();
        try
        {
            var total = _hits + _misses;
            var hitRate = total > 0 ? (double)_hits / total : 0;

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

        if (oldest is not null && oldest.Lock is not null)
        {
            _cache.TryRemove(oldest.Lock.Key, out _);
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
        try
        {
            _hits++;
        }
        finally
        {
            _statsLock.ExitWriteLock();
        }
    }

    private void IncrementMiss()
    {
        _statsLock.EnterWriteLock();
        try
        {
            _misses++;
        }
        finally
        {
            _statsLock.ExitWriteLock();
        }
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

    /// <summary>
    /// Wrapper that stores a cached lock together with its caching and access timestamps.
    /// </summary>
    private class CachedLock
    {
        /// <summary>The cached lock instance.</summary>
        public required Lock Lock { get; init; }

        /// <summary>UTC time when the entry was stored. Used for TTL expiration.</summary>
        public DateTime CachedAt { get; init; }

        /// <summary>UTC time of the last successful read. Used for LRU eviction.</summary>
        public DateTime LastAccessTime { get; set; }

        /// <summary>
        /// Determines whether the entry has outlived its time-to-live.
        /// </summary>
        /// <param name="ttlSeconds">Time-to-live in seconds.</param>
        /// <returns><c>true</c> if the entry age exceeds <paramref name="ttlSeconds"/>; otherwise <c>false</c>.</returns>
        public bool IsExpired(int ttlSeconds) =>
            (DateTime.UtcNow - CachedAt).TotalSeconds > ttlSeconds;
    }
}

/// <summary>
/// Configuration for cache behavior.
/// </summary>
public sealed class CacheConfiguration
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
    /// <summary>Number of entries currently stored in the cache.</summary>
    public int CachedItems { get; init; }

    /// <summary>Total number of successful cache lookups.</summary>
    public long Hits { get; init; }

    /// <summary>Total number of failed lookups (absent or expired entries).</summary>
    public long Misses { get; init; }

    /// <summary>Hit ratio as a fraction between 0 and 1.</summary>
    public double HitRate { get; init; }

    /// <summary>UTC time at which the statistics snapshot was taken.</summary>
    public DateTime Timestamp { get; init; }
}
