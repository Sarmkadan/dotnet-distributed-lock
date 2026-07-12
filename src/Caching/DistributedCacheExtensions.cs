#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Caching;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

/// <summary>
/// Extension methods for <see cref="IDistributedCache"/> that provide type-safe JSON serialization
/// and common caching patterns for distributed lock scenarios.
/// </summary>
public static class DistributedCacheExtensions
{
private static readonly JsonSerializerOptions _jsonOptions = new()
{
PropertyNameCaseInsensitive = true
};

/// <summary>
/// Gets a cached value of type <typeparamref name="T"/> from distributed cache.
/// Returns null if key doesn't exist, value cannot be deserialized, or if any error occurs.
/// </summary>
/// <typeparam name="T">The type of value to deserialize.</typeparam>
/// <param name="cache">The distributed cache instance.</param>
/// <param name="key">The cache key.</param>
/// <param name="cancellationToken">A cancellation token.</param>
/// <returns>The deserialized value, or null if not found or on error.</returns>
/// <exception cref="ArgumentNullException"><paramref name="cache"/> or <paramref name="key"/> is null.</exception>
/// <exception cref="ArgumentException"><paramref name="key"/> is empty or whitespace.</exception>
public static async Task<T?> GetAsJsonAsync<T>(
this IDistributedCache cache,
[NotNull] string key,
CancellationToken cancellationToken = default) where T : class
{
ArgumentNullException.ThrowIfNull(cache);
ArgumentException.ThrowIfNullOrWhiteSpace(key);

try
{
var bytes = await cache.GetAsync(key, cancellationToken);

if (bytes is null || bytes.Length == 0)
return null;

var json = System.Text.Encoding.UTF8.GetString(bytes);
return JsonSerializer.Deserialize<T>(json, _jsonOptions);
}
catch
{
// Swallow exceptions to maintain cache-as-fallback semantics
return null;
}
}

/// <summary>
/// Sets a value in distributed cache, serializing it to JSON.
/// </summary>
/// <typeparam name="T">The type of value to serialize.</typeparam>
/// <param name="cache">The distributed cache instance.</param>
/// <param name="key">The cache key.</param>
/// <param name="value">The value to cache.</param>
/// <param name="expiration">Optional expiration time span. If null or zero, no expiration is set.</param>
/// <param name="cancellationToken">A cancellation token.</param>
/// <exception cref="ArgumentNullException"><paramref name="cache"/> or <paramref name="key"/> is null.</exception>
/// <exception cref="ArgumentException"><paramref name="key"/> is empty or whitespace.</exception>
public static async Task SetAsJsonAsync<T>(
this IDistributedCache cache,
[NotNull] string key,
[DisallowNull] T value,
TimeSpan? expiration = null,
CancellationToken cancellationToken = default) where T : class
{
ArgumentNullException.ThrowIfNull(cache);
ArgumentException.ThrowIfNullOrWhiteSpace(key);
ArgumentNullException.ThrowIfNull(value);

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
/// <typeparam name="T">The type of value to get/create.</typeparam>
/// <param name="cache">The distributed cache instance.</param>
/// <param name="key">The cache key.</param>
/// <param name="factory">Factory function to create value if not in cache.</param>
/// <param name="expiration">Optional expiration time span.</param>
/// <param name="cancellationToken">A cancellation token.</param>
/// <returns>The cached or newly created value, or null if creation fails.</returns>
/// <exception cref="ArgumentNullException"><paramref name="cache"/>, <paramref name="key"/>, or <paramref name="factory"/> is null.</exception>
/// <exception cref="ArgumentException"><paramref name="key"/> is empty or whitespace.</exception>
public static async Task<T?> GetOrCreateAsync<T>(
this IDistributedCache cache,
[NotNull] string key,
[NotNull] Func<Task<T?>> factory,
TimeSpan? expiration = null,
CancellationToken cancellationToken = default) where T : class
{
ArgumentNullException.ThrowIfNull(cache);
ArgumentException.ThrowIfNullOrWhiteSpace(key);
ArgumentNullException.ThrowIfNull(factory);

// Try to get from cache
var cached = await cache.GetAsJsonAsync<T>(key, cancellationToken);
if (cached is not null)
return cached;

// Not in cache, create via factory
var value = await factory();

if (value is not null)
{
await cache.SetAsJsonAsync(key, value, expiration, cancellationToken);
}

return value;
}

/// <summary>
/// Removes one or more keys from distributed cache.
/// Silently succeeds if keys don't exist.
/// </summary>
/// <param name="cache">The distributed cache instance.</param>
/// <param name="keys">The cache keys to remove.</param>
/// <exception cref="ArgumentNullException"><paramref name="cache"/> or <paramref name="keys"/> is null.</exception>
public static async Task RemoveAsync(
this IDistributedCache cache,
[NotNull] params string[] keys)
{
ArgumentNullException.ThrowIfNull(cache);
ArgumentNullException.ThrowIfNull(keys);

foreach (var key in keys)
{
if (!string.IsNullOrWhiteSpace(key))
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
/// <param name="cache">The distributed cache instance.</param>
/// <param name="key">The cache key to check.</param>
/// <param name="cancellationToken">A cancellation token.</param>
/// <returns>True if key exists and has a non-empty value; otherwise false.</returns>
/// <exception cref="ArgumentNullException"><paramref name="cache"/> or <paramref name="key"/> is null.</exception>
/// <exception cref="ArgumentException"><paramref name="key"/> is empty or whitespace.</exception>
public static async Task<bool> ExistsAsync(
this IDistributedCache cache,
[NotNull] string key,
CancellationToken cancellationToken = default)
{
ArgumentNullException.ThrowIfNull(cache);
ArgumentException.ThrowIfNullOrWhiteSpace(key);

try
{
var bytes = await cache.GetAsync(key, cancellationToken);
return bytes is not null && bytes.Length > 0;
}
catch
{
return false;
}
}

/// <summary>
/// Sets a key to expire at a specific time.
/// </summary>
/// <param name="cache">The distributed cache instance.</param>
/// <param name="key">The cache key.</param>
/// <param name="expiresAt">The absolute expiration time.</param>
/// <param name="cancellationToken">A cancellation token.</param>
/// <exception cref="ArgumentNullException"><paramref name="cache"/> or <paramref name="key"/> is null.</exception>
/// <exception cref="ArgumentException"><paramref name="key"/> is empty or whitespace.</exception>
public static async Task SetExpirationAsync(
this IDistributedCache cache,
[NotNull] string key,
DateTime expiresAt,
CancellationToken cancellationToken = default)
{
ArgumentNullException.ThrowIfNull(cache);
ArgumentException.ThrowIfNullOrWhiteSpace(key);

var expiration = expiresAt.ToUniversalTime() - DateTime.UtcNow;

if (expiration > TimeSpan.Zero)
{
var bytes = await cache.GetAsync(key, cancellationToken);

if (bytes is not null)
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
/// <see cref="IDistributedCache"/> does not expose key enumeration, so this method
/// validates its arguments and completes without removing entries. Use a
/// provider-specific API (for example Redis SCAN plus DEL) when pattern
/// invalidation is required.
/// </summary>
/// <param name="cache">The distributed cache instance.</param>
/// <param name="pattern">The pattern to match keys against.</param>
/// <param name="cancellationToken">A cancellation token.</param>
/// <exception cref="ArgumentNullException"><paramref name="cache"/> or <paramref name="pattern"/> is null.</exception>
/// <exception cref="ArgumentException"><paramref name="pattern"/> is empty or whitespace.</exception>
public static Task InvalidatePatternAsync(
this IDistributedCache cache,
[NotNull] string pattern,
CancellationToken cancellationToken = default)
{
ArgumentNullException.ThrowIfNull(cache);
ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

// Note: This is a conceptual method. Implementation depends on the cache provider.
// Redis supports pattern-based deletion via Scan operations.
// Other providers may not support this and would need custom implementation.
return Task.CompletedTask;
}

/// <summary>
/// Caches a value with a sliding expiration window.
/// The expiration extends each time the value is accessed.
/// </summary>
/// <typeparam name="T">The type of value to cache.</typeparam>
/// <param name="cache">The distributed cache instance.</param>
/// <param name="key">The cache key.</param>
/// <param name="value">The value to cache.</param>
/// <param name="slidingExpiration">The sliding expiration period.</param>
/// <param name="cancellationToken">A cancellation token.</param>
/// <exception cref="ArgumentNullException"><paramref name="cache"/>, <paramref name="key"/>, or <paramref name="value"/> is null.</exception>
/// <exception cref="ArgumentException"><paramref name="key"/> is empty or whitespace.</exception>
public static async Task SetWithSlidingExpirationAsync<T>(
this IDistributedCache cache,
[NotNull] string key,
[DisallowNull] T value,
TimeSpan slidingExpiration,
CancellationToken cancellationToken = default) where T : class
{
ArgumentNullException.ThrowIfNull(cache);
ArgumentException.ThrowIfNullOrWhiteSpace(key);
ArgumentNullException.ThrowIfNull(value);

if (slidingExpiration <= TimeSpan.Zero)
{
throw new ArgumentOutOfRangeException(nameof(slidingExpiration), "Sliding expiration must be positive.");
}

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