// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Caching;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Generates consistent cache keys for distributed lock data.
/// Provides strategies for single locks, lock families, and aggregated data.
/// Ensures consistent key formats across the application for cache coordination.
/// </summary>
public static class CacheKeyGenerator
{
    private const string Prefix = "lock:";
    private const string MetricsPrefix = "metrics:";
    private const string StatusPrefix = "status:";

    /// <summary>
    /// Generates a cache key for a single lock by ID.
    /// </summary>
    public static string GenerateLockKey(string lockId)
    {
        if (string.IsNullOrEmpty(lockId))
            throw new ArgumentException("Lock ID cannot be empty", nameof(lockId));

        return $"{Prefix}{lockId}";
    }

    /// <summary>
    /// Generates a cache key for locks by name pattern.
    /// Useful for finding all locks matching a pattern.
    /// </summary>
    public static string GenerateLockNameKey(string lockName)
    {
        if (string.IsNullOrEmpty(lockName))
            throw new ArgumentException("Lock name cannot be empty", nameof(lockName));

        return $"{Prefix}name:{lockName}";
    }

    /// <summary>
    /// Generates a cache key for metrics of a specific lock.
    /// </summary>
    public static string GenerateMetricsKey(string lockId)
    {
        if (string.IsNullOrEmpty(lockId))
            throw new ArgumentException("Lock ID cannot be empty", nameof(lockId));

        return $"{MetricsPrefix}{lockId}";
    }

    /// <summary>
    /// Generates a cache key for system-wide metrics.
    /// </summary>
    public static string GenerateSystemMetricsKey()
    {
        return $"{MetricsPrefix}system";
    }

    /// <summary>
    /// Generates a cache key for lock status.
    /// </summary>
    public static string GenerateStatusKey(string lockId)
    {
        if (string.IsNullOrEmpty(lockId))
            throw new ArgumentException("Lock ID cannot be empty", nameof(lockId));

        return $"{StatusPrefix}{lockId}";
    }

    /// <summary>
    /// Generates a cache key for a specific owner's locks.
    /// </summary>
    public static string GenerateOwnerLocksKey(string ownerId)
    {
        if (string.IsNullOrEmpty(ownerId))
            throw new ArgumentException("Owner ID cannot be empty", nameof(ownerId));

        return $"{Prefix}owner:{ownerId}";
    }

    /// <summary>
    /// Generates a cache key for all active locks.
    /// </summary>
    public static string GenerateActiveLockKeysPattern()
    {
        return $"{Prefix}active:*";
    }

    /// <summary>
    /// Generates a hash-based cache key for parameterized queries.
    /// </summary>
    public static string GenerateQueryKey(string queryName, params object?[] parameters)
    {
        var paramStr = string.Join("|", parameters.Select(p => p?.ToString() ?? "null"));
        var hash = ComputeSha256Hash($"{queryName}:{paramStr}");
        return $"query:{hash}";
    }

    /// <summary>
    /// Generates a cache key for a distributed lock configuration.
    /// </summary>
    public static string GenerateConfigurationKey(string configName)
    {
        if (string.IsNullOrEmpty(configName))
            throw new ArgumentException("Configuration name cannot be empty", nameof(configName));

        return $"config:{configName}";
    }

    /// <summary>
    /// Creates a tag-based cache key for grouping related cache entries.
    /// Useful for bulk invalidation of related caches.
    /// </summary>
    public static string GenerateTagKey(params string[] tags)
    {
        if (tags == null || tags.Length == 0)
            throw new ArgumentException("At least one tag is required");

        var tagStr = string.Join(":", tags);
        return $"tag:{tagStr}";
    }

    /// <summary>
    /// Extracts lock ID from a cache key.
    /// </summary>
    public static string? ExtractLockIdFromKey(string cacheKey)
    {
        if (!cacheKey.StartsWith(Prefix))
            return null;

        return cacheKey.Substring(Prefix.Length);
    }

    /// <summary>
    /// Determines if a cache key belongs to a lock.
    /// </summary>
    public static bool IsLockKey(string cacheKey)
    {
        return cacheKey.StartsWith(Prefix) && !cacheKey.Contains("name:") && !cacheKey.Contains("owner:");
    }

    /// <summary>
    /// Determines if a cache key belongs to metrics.
    /// </summary>
    public static bool IsMetricsKey(string cacheKey)
    {
        return cacheKey.StartsWith(MetricsPrefix);
    }

    /// <summary>
    /// Computes SHA256 hash of a string for use in cache keys.
    /// Ensures consistent, compact key representation for complex data.
    /// </summary>
    private static string ComputeSha256Hash(string input)
    {
        using (var sha256 = SHA256.Create())
        {
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(hashedBytes).Replace("-", "").ToLowerInvariant().Substring(0, 16);
        }
    }
}

/// <summary>
/// Cache key patterns for wildcard matching.
/// </summary>
public static class CacheKeyPatterns
{
    public const string AllLocks = "lock:*";
    public const string AllMetrics = "metrics:*";
    public const string AllQueries = "query:*";
    public const string MetricsPattern = "metrics:*";
    public const string StatusPattern = "status:*";
}

/// <summary>
/// Predefined cache key sets for common operations.
/// </summary>
public static class CacheKeySets
{
    /// <summary>
    /// Cache keys that should be invalidated when a lock is acquired.
    /// </summary>
    public static string[] GetKeysByAcquisition(string lockId, string ownerId)
    {
        return new[]
        {
            CacheKeyGenerator.GenerateLockKey(lockId),
            CacheKeyGenerator.GenerateOwnerLocksKey(ownerId),
            CacheKeyGenerator.GenerateSystemMetricsKey(),
            CacheKeyGenerator.GenerateStatusKey(lockId)
        };
    }

    /// <summary>
    /// Cache keys that should be invalidated when a lock is released.
    /// </summary>
    public static string[] GetKeysByRelease(string lockId, string ownerId)
    {
        return new[]
        {
            CacheKeyGenerator.GenerateLockKey(lockId),
            CacheKeyGenerator.GenerateOwnerLocksKey(ownerId),
            CacheKeyGenerator.GenerateStatusKey(lockId)
        };
    }
}
