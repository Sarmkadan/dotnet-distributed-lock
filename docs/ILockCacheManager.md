# ILockCacheManager

`ILockCacheManager` defines the contract for caching distributed lock metadata in the `dotnet-distributed-lock` library. Implementations store, retrieve, and evict `Lock` objects based on time-to-live (TTL), cache size limits, and access patterns. The interface exposes asynchronous CRUD operations, a statistics snapshot, and configuration properties that govern expiration, capacity, and compression behaviour. The built-in `InMemoryLockCacheManager` provides a concrete in-process implementation.

## API

### InMemoryLockCacheManager

A concrete implementation of `ILockCacheManager` that stores lock metadata in memory. It enforces the configured `MaxCacheSize` and `TtlSeconds`, evicts expired entries, and tracks hit/miss statistics. This type is instantiated directly and does not require external dependencies.

### async Task<Lock?> GetAsync

Retrieves a cached `Lock` by its unique identifier. Returns the `Lock` instance if it exists and has not expired; otherwise returns `null`. Each successful call increments the `Hits` counter and updates `LastAccessTime` on the cached entry. A call that returns `null` increments `Misses`.

### async Task SetAsync

Inserts or overwrites a `Lock` entry in the cache. If the cache has reached `MaxCacheSize`, the implementation evicts the least-recently-accessed or expired entry before inserting. If `EnableCompression` is `true`, the implementation may compress the stored payload. Throws `ArgumentNullException` when the provided `Lock` is `null`.

### async Task RemoveAsync

Removes a specific `Lock` from the cache by its identifier. If the entry does not exist, the call completes without error. After removal, subsequent `GetAsync` calls for the same identifier return `null`.

### async Task<List<Lock>> GetAllAsync

Returns a list of all non-expired `Lock` entries currently held in the cache. The returned list is a snapshot; modifications to it do not affect the underlying cache. Expired entries that have not yet been evicted are excluded from the result.

### async Task ClearAsync

Purges every entry from the cache immediately, regardless of expiration state. After this call completes, `CachedItems` is zero, and all subsequent `GetAsync` calls return `null` until new entries are added via `SetAsync`.

### CacheStatistics GetStatistics

Returns a snapshot of current cache metrics. The returned `CacheStatistics` object contains `CachedItems`, `Hits`, `Misses`, `HitRate`, and `Timestamp`. Calling this method does not reset counters.

### required Lock Lock

The `Lock` instance that this cache entry represents. Contains the lock’s resource key, owner identifier, acquired timestamp, and duration. This property is required when constructing a cache entry.

### DateTime CachedAt

The UTC timestamp at which this entry was inserted or last updated in the cache. Used internally to calculate expiration relative to `TtlSeconds`.

### DateTime LastAccessTime

The UTC timestamp of the most recent `GetAsync` hit on this entry. Eviction policies may use this value to select candidates when `MaxCacheSize` is exceeded.

### bool IsExpired

Indicates whether the entry has exceeded its time-to-live. Computed as `CachedAt + TtlSeconds < DateTime.UtcNow`. Expired entries are eligible for eviction and are excluded from `GetAsync` and `GetAllAsync` results.

### int TtlSeconds

The time-to-live in seconds for cached entries. An entry becomes `IsExpired` after this duration elapses from `CachedAt`. A value of zero or negative causes immediate expiration.

### int MaxCacheSize

The maximum number of entries the cache holds before eviction is triggered. When a `SetAsync` call would exceed this limit, the implementation removes the least-recently-accessed or expired entry first.

### bool EnableCompression

When `true`, the implementation may apply compression to stored `Lock` data to reduce memory footprint. The default is `false`. Changing this value does not retroactively compress existing entries.

### int CachedItems

The current number of non-expired entries in the cache. This value decreases when entries expire, are explicitly removed, or are evicted due to capacity constraints.

### long Hits

The cumulative number of times `GetAsync` returned a non-null, non-expired entry since the cache was created or counters were last reset.

### long Misses

The cumulative number of times `GetAsync` returned `null` because the requested entry was absent or expired.

### double HitRate

The ratio of `Hits` to total `GetAsync` calls, expressed as a value between 0.0 and 1.0. Computed as `Hits / (Hits + Misses)` when there is at least one call; otherwise returns 0.0.

### DateTime Timestamp

The UTC timestamp at which the statistics snapshot was captured. This is set when `GetStatistics` is called.

## Usage

### Example 1: Basic caching of a distributed lock

```csharp
using DotNetDistributedLock;

var cache = new InMemoryLockCacheManager
{
    TtlSeconds = 60,
    MaxCacheSize = 1000,
    EnableCompression = false
};

// Store a lock after acquiring it
var acquiredLock = new Lock
{
    ResourceKey = "orders:12345",
    OwnerId = "service-instance-1",
    AcquiredAt = DateTime.UtcNow,
    DurationSeconds = 30
};

await cache.SetAsync(acquiredLock);

// Later, check if the lock is still cached
Lock? cached = await cache.GetAsync("orders:12345");
if (cached is not null && !cached.IsExpired)
{
    Console.WriteLine($"Lock held by {cached.OwnerId}");
}
```

### Example 2: Monitoring cache health with statistics

```csharp
using DotNetDistributedLock;

var cache = new InMemoryLockCacheManager
{
    TtlSeconds = 30,
    MaxCacheSize = 500
};

// Simulate some activity
await cache.SetAsync(new Lock { ResourceKey = "a" });
await cache.SetAsync(new Lock { ResourceKey = "b" });
await cache.GetAsync("a"); // hit
await cache.GetAsync("c"); // miss

CacheStatistics stats = cache.GetStatistics();
Console.WriteLine($"Items: {stats.CachedItems}, " +
                  $"HitRate: {stats.HitRate:P2}, " +
                  $"Snapshot at: {stats.Timestamp:O}");

// Clear when shutting down
await cache.ClearAsync();
```

## Notes

- **Thread safety:** `InMemoryLockCacheManager` uses internal synchronisation to ensure that concurrent calls to `GetAsync`, `SetAsync`, `RemoveAsync`, `GetAllAsync`, and `ClearAsync` are safe. `GetStatistics` reads counters atomically but does not lock the entire cache; the snapshot may reflect a momentarily inconsistent state if a mutation is in flight.
- **Expiration is lazy:** Entries are evaluated for expiration on access (`GetAsync`, `GetAllAsync`) and during eviction triggered by `SetAsync`. An expired entry may remain in the underlying storage until one of these operations encounters it. `ClearAsync` removes all entries immediately regardless of expiration.
- **Eviction order:** When `MaxCacheSize` is reached, the implementation evicts the entry with the earliest `LastAccessTime` among non-expired entries. If all entries are expired, they are removed before inserting the new entry.
- **Statistics reset:** The `Hits` and `Misses` counters are not automatically reset. To reset them, create a new `InMemoryLockCacheManager` instance. `HitRate` returns 0.0 when no `GetAsync` calls have been made.
- **Compression:** `EnableCompression` is a hint to the implementation. The default `InMemoryLockCacheManager` stores entries uncompressed regardless of this setting. Derived or alternative implementations may honour it.
- **Null handling:** `SetAsync` throws `ArgumentNullException` when the `Lock` argument is `null`. `GetAsync` and `RemoveAsync` accept a `null` or empty resource key and return `null` or no-op respectively without throwing.
