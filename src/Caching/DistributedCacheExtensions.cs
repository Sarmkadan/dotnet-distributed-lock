// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Caching;

using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

/// <summary>
/// Extension methods for IDistributedCache for simplified usage with locks.
/// Provides generic methods with JSON serialization for type-safe caching.
/// </summary>
public static class DistributedCacheExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Gets a cached value of type T from distributed cache.
    /// Returns null if key doesn't exist or value cannot be deserialized.
    /// </summary>
    public static async Task<T?> GetAsJsonAsync<T>(
        this IDistributedCache cache,
        string key,
        CancellationToken cancellationToken = default) where T : class
    {
        if (string.IsNullOrEmpty(key))
            return null;

        try
        {
            var bytes = await cache.GetAsync(key, cancellationToken);

            if (bytes == null || bytes.Length == 0)
                return null;

            var json = System.Text.Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Sets a value in distributed cache, serializing it to JSON.
    /// </summary>
    public static async Task SetAsJsonAsync<T>(
        this IDistributedCache cache,
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default) where T : class
    {
        if (string.IsNullOrEmpty(key) || value == null)
            return;

        try
        {
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);

            var options = new DistributedCacheEntryOptions();
            if (expiration.HasValue && expiration.Value > TimeSpan.Zero)
            {
                options.AbsoluteExpirationRelativeToNow = expiration;
            }

            await cache.SetAsync(key, bytes, options, cancellationToken);
        }
        catch
        {
            // Log but don't throw - cache failures shouldn't break the application
        }
    }

    /// <summary>
    /// Gets or creates a cached value using a factory function.
    /// Automatically handles serialization and expiration.
    /// </summary>
    public static async Task<T?> GetOrCreateAsync<T>(
        this IDistributedCache cache,
        string key,
        Func<Task<T?>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default) where T : class
    {
        // Try to get from cache
        var cached = await cache.GetAsJsonAsync<T>(key, cancellationToken);
        if (cached != null)
            return cached;

        // Not in cache, create via factory
        var value = await factory();

        if (value != null)
        {
            await cache.SetAsJsonAsync(key, value, expiration, cancellationToken);
        }

        return value;
    }

    /// <summary>
    /// Removes one or more keys from distributed cache.
    /// Silently succeeds if keys don't exist.
    /// </summary>
    public static async Task RemoveAsync(
        this IDistributedCache cache,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!string.IsNullOrEmpty(key))
            {
                try
                {
                    await cache.RemoveAsync(key);
                }
                catch
                {
                    // Ignore removal errors
                }
            }
        }
    }

    /// <summary>
    /// Checks if a key exists in the cache.
    /// </summary>
    public static async Task<bool> ExistsAsync(
        this IDistributedCache cache,
        string key,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        try
        {
            var bytes = await cache.GetAsync(key, cancellationToken);
            return bytes != null && bytes.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sets a key to expire at a specific time.
    /// </summary>
    public static async Task SetExpirationAsync(
        this IDistributedCache cache,
        string key,
        DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        var expiration = expiresAt.ToUniversalTime() - DateTime.UtcNow;

        if (expiration > TimeSpan.Zero)
        {
            var bytes = await cache.GetAsync(key, cancellationToken);

            if (bytes != null)
            {
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration
                };

                await cache.SetAsync(key, bytes, options, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Invalidates multiple cache keys matching a pattern (where supported).
    /// </summary>
    public static async Task InvalidatePatternAsync(
        this IDistributedCache cache,
        string pattern,
        CancellationToken cancellationToken = default)
    {
        // Note: This is a conceptual method. Implementation depends on the cache provider.
        // Redis supports pattern-based deletion via Scan operations.
        // Other providers may not support this and would need custom implementation.
        await Task.CompletedTask;
    }

    /// <summary>
    /// Caches a value with a sliding expiration window.
    /// The expiration extends each time the value is accessed.
    /// </summary>
    public static async Task SetWithSlidingExpirationAsync<T>(
        this IDistributedCache cache,
        string key,
        T value,
        TimeSpan slidingExpiration,
        CancellationToken cancellationToken = default) where T : class
    {
        if (string.IsNullOrEmpty(key) || value == null)
            return;

        try
        {
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);

            var options = new DistributedCacheEntryOptions
            {
                SlidingExpiration = slidingExpiration
            };

            await cache.SetAsync(key, bytes, options, cancellationToken);
        }
        catch
        {
            // Silently fail
        }
    }
}
