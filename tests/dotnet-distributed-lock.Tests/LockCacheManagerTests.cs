#nullable enable
using FluentAssertions;
using SarmKadan.DistributedLock.Caching;
using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Models;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Contains unit tests for the <see cref="InMemoryLockCacheManager"/> class, ensuring correct behavior of cache operations such as Get, Set, Remove, and statistics tracking.
/// </summary>
public class InMemoryLockCacheManagerTests
{
    private readonly InMemoryLockCacheManager _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryLockCacheManagerTests"/> class, setting up an empty <see cref="InMemoryLockCacheManager"/> for testing.
    /// </summary>
    public InMemoryLockCacheManagerTests()
    {
        _cache = new InMemoryLockCacheManager();
    }

    // -------------------------------------------------------------------------
    // GetAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="InMemoryLockCacheManager.GetAsync(string)"/> returns null when the cache is empty.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetAsync_WithEmptyCache_ReturnsNull()
    {
        // Act
        var result = await _cache.GetAsync("lock:nonexistent");

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that <see cref="InMemoryLockCacheManager.GetAsync(string)"/> returns the expected lock after it has been set.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetAsync_AfterSet_ReturnsCachedLock()
    {
        // Arrange
        var @lock = new Lock("lock:1", "owner-1", TimeSpan.FromSeconds(30))
        {
            Status = LockStatus.Held
        };
        await _cache.SetAsync(@lock);

        // Act
        var cached = await _cache.GetAsync("lock:1");

        // Assert
        cached.Should().NotBeNull();
        cached!.Key.Should().Be("lock:1");
        cached.OwnerId.Should().Be("owner-1");
    }

    /// <summary>
    /// Verifies that <see cref="InMemoryLockCacheManager.GetAsync(string)"/> returns null when the requested key is null.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetAsync_WithNullId_ReturnsNull()
    {
        // Act
        var result = await _cache.GetAsync(null!);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that <see cref="InMemoryLockCacheManager.GetAsync(string)"/> returns null when the requested key is empty.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetAsync_WithEmptyId_ReturnsNull()
    {
        // Act
        var result = await _cache.GetAsync("");

        // Assert
        result.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // SetAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="InMemoryLockCacheManager.SetAsync(Lock)"/> does not throw an exception when the lock is null.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task SetAsync_WithNullLock_DoesNotThrow()
    {
        // Act & Assert
        await _cache.Invoking(c => c.SetAsync(null!)).Should().NotThrowAsync();
    }

    /// <summary>
    /// Verifies that a lock stored via <see cref="InMemoryLockCacheManager.SetAsync(Lock)"/> can be retrieved.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task SetAsync_StoreLock_CanBeRetrieved()
    {
        // Arrange
        var @lock = new Lock("lock:test", "owner-1", TimeSpan.FromSeconds(30));

        // Act
        await _cache.SetAsync(@lock);
        var retrieved = await _cache.GetAsync("lock:test");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Key.Should().Be("lock:test");
    }

    /// <summary>
    /// Verifies that multiple locks stored via <see cref="InMemoryLockCacheManager.SetAsync(Lock)"/> can all be retrieved.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task SetAsync_MultiipleLocks_AllCanBeRetrieved()
    {
        // Arrange
        var locks = Enumerable.Range(1, 5)
            .Select(i => new Lock($"lock:{i}", $"owner-{i}", TimeSpan.FromSeconds(30)))
            .ToList();

        // Act
        foreach (var @lock in locks)
        {
            await _cache.SetAsync(@lock);
        }

        // Act & Assert
        for (int i = 1; i <= 5; i++)
        {
            var cached = await _cache.GetAsync($"lock:{i}");
            cached.Should().NotBeNull();
            cached!.OwnerId.Should().Be($"owner-{i}");
        }
    }

    /// <summary>
    /// Verifies that <see cref="InMemoryLockCacheManager.SetAsync(Lock)"/> overwrites an existing lock if the key is the same.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task SetAsync_OverwriteExistingLock()
    {
        // Arrange
        var lock1 = new Lock("lock:1", "owner-1", TimeSpan.FromSeconds(30));
        var lock2 = new Lock("lock:1", "owner-2", TimeSpan.FromSeconds(60));

        // Act
        await _cache.SetAsync(lock1);
        await _cache.SetAsync(lock2);
        var cached = await _cache.GetAsync("lock:1");

        // Assert
        cached.Should().NotBeNull();
        cached!.OwnerId.Should().Be("owner-2");
        cached.Duration.Should().Be(TimeSpan.FromSeconds(60));
    }

    // -------------------------------------------------------------------------
    // RemoveAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="InMemoryLockCacheManager.RemoveAsync(string)"/> successfully removes a lock from the cache.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task RemoveAsync_RemovesLock()
    {
        // Arrange
        var @lock = new Lock("lock:1", "owner-1", TimeSpan.FromSeconds(30));
        await _cache.SetAsync(@lock);

        // Act
        await _cache.RemoveAsync("lock:1");
        var cached = await _cache.GetAsync("lock:1");

        // Assert
        cached.Should().BeNull();
    }

    /// <summary>
    /// Verifies that <see cref="InMemoryLockCacheManager.RemoveAsync(string)"/> does not throw when removing a non-existent lock.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task RemoveAsync_WithNonexistentLock_DoesNotThrow()
    {
        // Act & Assert
        await _cache.Invoking(c => c.RemoveAsync("lock:ghost")).Should().NotThrowAsync();
    }

    /// <summary>
    /// Verifies that <see cref="InMemoryLockCacheManager.RemoveAsync(string)"/> does not throw when the key is empty.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task RemoveAsync_WithEmptyId_DoesNotThrow()
    {
        // Act & Assert
        await _cache.Invoking(c => c.RemoveAsync("")).Should().NotThrowAsync();
    }

    // -------------------------------------------------------------------------
    // GetAllAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="InMemoryLockCacheManager.GetAllAsync()"/> returns an empty list when the cache is empty.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetAllAsync_WithEmptyCache_ReturnsEmptyList()
    {
        // Act
        var all = await _cache.GetAllAsync();

        // Assert
        all.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that <see cref="InMemoryLockCacheManager.GetAllAsync()"/> returns all cached locks.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetAllAsync_ReturnsAllCachedLocks()
    {
        // Arrange
        var locks = Enumerable.Range(1, 5)
            .Select(i => new Lock($"lock:{i}", $"owner-{i}", TimeSpan.FromSeconds(30)))
            .ToList();

        foreach (var @lock in locks)
        {
            await _cache.SetAsync(@lock);
        }

        // Act
        var all = await _cache.GetAllAsync();

        // Assert
        all.Should().HaveCount(5);
        all.Select(l => l.Key).Should().Contain(new[] { "lock:1", "lock:2", "lock:3", "lock:4", "lock:5" });
    }

    /// <summary>
    /// Verifies that <see cref="InMemoryLockCacheManager.GetAllAsync()"/> does not return locks that have been removed.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetAllAsync_DoesNotReturnRemovedLocks()
    {
        // Arrange
        var lock1 = new Lock("lock:1", "owner-1", TimeSpan.FromSeconds(30));
        var lock2 = new Lock("lock:2", "owner-2", TimeSpan.FromSeconds(30));

        await _cache.SetAsync(lock1);
        await _cache.SetAsync(lock2);

        // Act
        await _cache.RemoveAsync("lock:1");
        var all = await _cache.GetAllAsync();

        // Assert
        all.Should().HaveCount(1);
        all.Single().Key.Should().Be("lock:2");
    }

    // -------------------------------------------------------------------------
    // ClearAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="InMemoryLockCacheManager.ClearAsync()"/> removes all locks from the cache.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ClearAsync_RemovesAllLocks()
    {
        // Arrange
        for (int i = 1; i <= 5; i++)
        {
            var @lock = new Lock($"lock:{i}", "owner-1", TimeSpan.FromSeconds(30));
            await _cache.SetAsync(@lock);
        }

        // Act
        await _cache.ClearAsync();
        var all = await _cache.GetAllAsync();

        // Assert
        all.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that <see cref="InMemoryLockCacheManager.ClearAsync()"/> does not throw when the cache is empty.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ClearAsync_WithEmptyCache_DoesNotThrow()
    {
        // Act & Assert
        await _cache.Invoking(c => c.ClearAsync()).Should().NotThrowAsync();
    }

    // -------------------------------------------------------------------------
    // Hit/Miss Statistics
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="InMemoryLockCacheManager.GetStatistics()"/> returns zero hits and misses in the initial state.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetStatistics_InitialState_HasZeroHitsAndMisses()
    {
        // Act
        var stats = _cache.GetStatistics();

        // Assert
        stats.Hits.Should().Be(0);
        stats.Misses.Should().Be(0);
    }

    /// <summary>
    /// Verifies that <see cref="InMemoryLockCacheManager.GetStatistics()"/> records a hit after a successful cache look-up.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetStatistics_AfterCacheHit_RecordsHit()
    {
        // Arrange
        var @lock = new Lock("lock:1", "owner-1", TimeSpan.FromSeconds(30));
        await _cache.SetAsync(@lock);

        // Act
        await _cache.GetAsync("lock:1");
        var stats = _cache.GetStatistics();

        // Assert
        stats.Hits.Should().Be(1);
        stats.Misses.Should().Be(0);
    }

    /// <summary>
    /// Verifies that <see cref="InMemoryLockCacheManager.GetStatistics()"/> records a miss after an unsuccessful cache look-up.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetStatistics_AfterCacheMiss_RecordsMiss()
    {
        // Act
        await _cache.GetAsync("lock:nonexistent");
        var stats = _cache.GetStatistics();

        // Assert
        stats.Hits.Should().Be(0);
        stats.Misses.Should().Be(1);
    }

    /// <summary>
    /// Verifies that <see cref="InMemoryLockCacheManager.GetStatistics()"/> correctly calculates the cache hit rate.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetStatistics_CalculatesHitRate()
    {
        // Arrange
        var @lock = new Lock("lock:1", "owner-1", TimeSpan.FromSeconds(30));
        await _cache.SetAsync(@lock);

        // Act — 3 hits, 1 miss
        await _cache.GetAsync("lock:1");
        await _cache.GetAsync("lock:1");
        await _cache.GetAsync("lock:1");
        await _cache.GetAsync("lock:nonexistent");

        var stats = _cache.GetStatistics();

        // Assert
        stats.HitRate.Should().BeApproximately(0.75, 0.01); // 3/4 = 75%
    }

    /// <summary>
    /// Verifies that the hit rate is zero when there are only misses.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetStatistics_WithOnlyMisses_HitRateIsZero()
    {
        // Act
        await _cache.GetAsync("lock:1");
        await _cache.GetAsync("lock:2");

        var stats = _cache.GetStatistics();

        // Assert
        stats.HitRate.Should().Be(0);
    }

    /// <summary>
    /// Verifies that the hit rate is one when there are only hits.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetStatistics_WithOnlyHits_HitRateIsOne()
    {
        // Arrange
        var @lock = new Lock("lock:1", "owner-1", TimeSpan.FromSeconds(30));
        await _cache.SetAsync(@lock);

        // Act
        await _cache.GetAsync("lock:1");
        await _cache.GetAsync("lock:1");

        var stats = _cache.GetStatistics();

        // Assert
        stats.HitRate.Should().Be(1.0);
    }

    // -------------------------------------------------------------------------
    // Configuration
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="InMemoryLockCacheManager"/> respects the provided <see cref="CacheConfiguration"/>.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Constructor_WithCustomConfiguration_UsesIt()
    {
        // Arrange
        var config = new CacheConfiguration { MaxCacheSize = 10, TtlSeconds = 60 };
        var cacheManager = new InMemoryLockCacheManager(config);

        // Act & Assert — should respect max size
        for (int i = 0; i < 15; i++)
        {
            var @lock = new Lock($"lock:{i}", "owner-1", TimeSpan.FromSeconds(30));
            await cacheManager.SetAsync(@lock);
        }

        var all = await cacheManager.GetAllAsync();
        all.Should().HaveCountLessThanOrEqualTo(10);
    }

    // -------------------------------------------------------------------------
    // Concurrency
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that concurrent access (sets and gets) maintains cache consistency.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ConcurrentAccess_SetAndGet_MaintainsConsistency()
    {
        // Arrange
        var lockCount = 100;
        var locks = Enumerable.Range(0, lockCount)
            .Select(i => new Lock($"lock:{i}", "owner-1", TimeSpan.FromSeconds(30)))
            .ToList();

        // Act — concurrent sets and gets
        var setTasks = locks.Select(l => _cache.SetAsync(l)).ToList();
        await Task.WhenAll(setTasks);

        var getTasks = locks.Select(l => _cache.GetAsync(l.Key)).ToList();
        var results = await Task.WhenAll(getTasks);

        // Assert
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
    }

    /// <summary>
    /// Verifies that concurrent mixed operations (set, remove, get) maintain cache consistency.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ConcurrentAccess_SetRemoveGet_MaintainsConsistency()
    {
        // Arrange
        var lockCount = 50;
        var locks = Enumerable.Range(0, lockCount)
            .Select(i => new Lock($"lock:{i}", "owner-1", TimeSpan.FromSeconds(30)))
            .ToList();

        foreach (var @lock in locks)
        {
            await _cache.SetAsync(@lock);
        }

        // Act — concurrent mixed operations
        var tasks = new List<Task>();

        for (int i = 0; i < lockCount; i++)
        {
            var index = i;
            if (index % 3 == 0)
            {
                tasks.Add(_cache.RemoveAsync($"lock:{index}"));
            }
            else
            {
                tasks.Add(_cache.GetAsync($"lock:{index}"));
            }
        }

        await Task.WhenAll(tasks);

        // Assert — should not throw and maintain consistent state
        var all = await _cache.GetAllAsync();
        all.Should().NotBeNull();
    }
}
