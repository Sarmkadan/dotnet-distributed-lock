#nullable enable
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
    private const string QueryPrefix = "query:";
    private const string ConfigPrefix = "config:";
    private const string TagPrefix = "tag:";
    private const string NamePrefix = "name:";
    private const string OwnerPrefix = "owner:";
    private const string ActivePrefix = "active:";
    private const string SystemSuffix = "system";

    private const int LockPrefixLength = 5; // "lock:".Length
    private const int MetricsPrefixLength = 7; // "metrics:".Length
    private const int StatusPrefixLength = 7; // "status:".Length
    private const int QueryPrefixLength = 6; // "query:".Length
    private const int ConfigPrefixLength = 6; // "config:".Length
    private const int TagPrefixLength = 4; // "tag:".Length
    private const int NamePrefixLength = 5; // "name:".Length
    private const int OwnerPrefixLength = 6; // "owner:".Length

    /// <summary>
    /// Generates a cache key for a single lock by ID.
    /// </summary>
    /// <param name="lockId">The unique identifier for the lock.</param>
    public static string GenerateLockKey(string lockId)
    {
        ArgumentException.ThrowIfNullOrEmpty(lockId);

        return string.Create(null, stackalloc char[LockPrefixLength + lockId.Length], $"{Prefix}{lockId}");
    }

    /// <summary>
    /// Generates a cache key for a single lock by ID using span-based allocation.
    /// </summary>
    /// <param name="lockId">The unique identifier for the lock.</param>
    public static string GenerateLockKey(ReadOnlySpan<char> lockId)
    {
        if (lockId.IsEmpty)
            throw new ArgumentException("Lock ID cannot be empty", nameof(lockId));

        return string.Create(null, stackalloc char[LockPrefixLength + lockId.Length], $"{Prefix}{lockId}");
    }

    /// <summary>
    /// Generates a cache key for locks by name pattern.
    /// Useful for finding all locks matching a pattern.
    /// </summary>
    /// <param name="lockName">The lock name pattern to match.</param>
    public static string GenerateLockNameKey(string lockName)
    {
        ArgumentException.ThrowIfNullOrEmpty(lockName);

        return string.Create(null, stackalloc char[LockPrefixLength + NamePrefixLength + lockName.Length], $"{Prefix}{NamePrefix}{lockName}");
    }

    /// <summary>
    /// Generates a cache key for locks by name pattern using span-based allocation.
    /// Useful for finding all locks matching a pattern.
    /// </summary>
    /// <param name="lockName">The lock name pattern to match.</param>
    public static string GenerateLockNameKey(ReadOnlySpan<char> lockName)
    {
        if (lockName.IsEmpty)
            throw new ArgumentException("Lock name cannot be empty", nameof(lockName));

        return string.Create(null, stackalloc char[LockPrefixLength + NamePrefixLength + lockName.Length], $"{Prefix}{NamePrefix}{lockName}");
    }

    /// <summary>
    /// Generates a cache key for metrics of a specific lock.
    /// </summary>
    /// <param name="lockId">The unique identifier for the lock.</param>
    public static string GenerateMetricsKey(string lockId)
    {
        ArgumentException.ThrowIfNullOrEmpty(lockId);

        return string.Create(null, stackalloc char[MetricsPrefixLength + lockId.Length], $"{MetricsPrefix}{lockId}");
    }

    /// <summary>
    /// Generates a cache key for metrics of a specific lock using span-based allocation.
    /// </summary>
    /// <param name="lockId">The unique identifier for the lock.</param>
    public static string GenerateMetricsKey(ReadOnlySpan<char> lockId)
    {
        if (lockId.IsEmpty)
            throw new ArgumentException("Lock ID cannot be empty", nameof(lockId));

        return string.Create(null, stackalloc char[MetricsPrefixLength + lockId.Length], $"{MetricsPrefix}{lockId}");
    }

    /// <summary>
    /// Generates a cache key for system-wide metrics.
    /// </summary>
    public static string GenerateSystemMetricsKey()
    {
        return MetricsPrefix + SystemSuffix;
    }

    /// <summary>
    /// Generates a cache key for lock status.
    /// </summary>
    /// <param name="lockId">The unique identifier for the lock.</param>
    public static string GenerateStatusKey(string lockId)
    {
        ArgumentException.ThrowIfNullOrEmpty(lockId);

        return string.Create(null, stackalloc char[StatusPrefixLength + lockId.Length], $"{StatusPrefix}{lockId}");
    }

    /// <summary>
    /// Generates a cache key for lock status using span-based allocation.
    /// </summary>
    /// <param name="lockId">The unique identifier for the lock.</param>
    public static string GenerateStatusKey(ReadOnlySpan<char> lockId)
    {
        if (lockId.IsEmpty)
            throw new ArgumentException("Lock ID cannot be empty", nameof(lockId));

        return string.Create(null, stackalloc char[StatusPrefixLength + lockId.Length], $"{StatusPrefix}{lockId}");
    }

    /// <summary>
    /// Generates a cache key for a specific owner's locks.
    /// </summary>
    /// <param name="ownerId">The unique identifier for the owner.</param>
    public static string GenerateOwnerLocksKey(string ownerId)
    {
        ArgumentException.ThrowIfNullOrEmpty(ownerId);

        return string.Create(null, stackalloc char[LockPrefixLength + OwnerPrefixLength + ownerId.Length], $"{Prefix}{OwnerPrefix}{ownerId}");
    }

    /// <summary>
    /// Generates a cache key for a specific owner's locks using span-based allocation.
    /// </summary>
    /// <param name="ownerId">The unique identifier for the owner.</param>
    public static string GenerateOwnerLocksKey(ReadOnlySpan<char> ownerId)
    {
        if (ownerId.IsEmpty)
            throw new ArgumentException("Owner ID cannot be empty", nameof(ownerId));

        return string.Create(null, stackalloc char[LockPrefixLength + OwnerPrefixLength + ownerId.Length], $"{Prefix}{OwnerPrefix}{ownerId}");
    }

    /// <summary>
    /// Generates a cache key for all active locks.
    /// </summary>
    public static string GenerateActiveLockKeysPattern()
    {
        return Prefix + ActivePrefix + "*";
    }

    /// <summary>
    /// Generates a hash-based cache key for parameterized queries.
    /// </summary>
    /// <param name="queryName">The name of the query.</param>
    /// <param name="parameters">The query parameters.</param>
    public static string GenerateQueryKey(string queryName, params object?[] parameters)
    {
        ArgumentException.ThrowIfNullOrEmpty(queryName);
        ArgumentNullException.ThrowIfNull(parameters);

        // Use StringBuilder to avoid multiple intermediate string allocations
        var paramBuilder = new StringBuilder();
        for (int i = 0; i < parameters.Length; i++)
        {
            if (i > 0)
                paramBuilder.Append('|');

            var param = parameters[i];
            if (param is null)
                paramBuilder.Append("null");
            else if (param is string str)
                paramBuilder.Append(str);
            else
                paramBuilder.Append(param.ToString());
        }

        var paramStr = paramBuilder.ToString();
        var hash = ComputeSha256Hash($"{queryName}:{paramStr}");
        return string.Create(null, stackalloc char[QueryPrefixLength + 16], $"{QueryPrefix}{hash}");
    }

    /// <summary>
    /// Generates a hash-based cache key for parameterized queries using span-based allocation.
    /// </summary>
    /// <param name="queryName">The name of the query.</param>
    /// <param name="parameters">The query parameters.</param>
    public static string GenerateQueryKey(ReadOnlySpan<char> queryName, ReadOnlySpan<object?> parameters)
    {
        if (queryName.IsEmpty)
            throw new ArgumentException("Query name cannot be empty", nameof(queryName));

        // Use StringBuilder to avoid multiple intermediate string allocations
        var paramBuilder = new StringBuilder();
        for (int i = 0; i < parameters.Length; i++)
        {
            if (i > 0)
                paramBuilder.Append('|');

            var param = parameters[i];
            if (param is null)
                paramBuilder.Append("null");
            else if (param is string str)
                paramBuilder.Append(str);
            else
                paramBuilder.Append(param.ToString());
        }

        var paramStr = paramBuilder.ToString();
        var hash = ComputeSha256Hash($"{queryName}:{paramStr}");
        return string.Create(null, stackalloc char[QueryPrefixLength + 16], $"{QueryPrefix}{hash}");
    }

    /// <summary>
    /// Generates a cache key for a distributed lock configuration.
    /// </summary>
    /// <param name="configName">The configuration name.</param>
    public static string GenerateConfigurationKey(string configName)
    {
        ArgumentException.ThrowIfNullOrEmpty(configName);

        return string.Create(null, stackalloc char[ConfigPrefixLength + configName.Length], $"{ConfigPrefix}{configName}");
    }

    /// <summary>
    /// Generates a cache key for a distributed lock configuration using span-based allocation.
    /// </summary>
    /// <param name="configName">The configuration name.</param>
    public static string GenerateConfigurationKey(ReadOnlySpan<char> configName)
    {
        if (configName.IsEmpty)
            throw new ArgumentException("Config name cannot be empty", nameof(configName));

        return string.Create(null, stackalloc char[ConfigPrefixLength + configName.Length], $"{ConfigPrefix}{configName}");
    }

    /// <summary>
    /// Creates a tag-based cache key for grouping related cache entries.
    /// Useful for bulk invalidation of related caches.
    /// </summary>
    /// <param name="tags">The tags to include in the key.</param>
    public static string GenerateTagKey(params string[] tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        if (tags.Length == 0)
            throw new ArgumentException("At least one tag is required");

        // Use StringBuilder to avoid intermediate string allocations
        var tagBuilder = new StringBuilder();
        for (int i = 0; i < tags.Length; i++)
        {
            if (i > 0)
                tagBuilder.Append(':');
            tagBuilder.Append(tags[i]);
        }

        var tagStr = tagBuilder.ToString();
        return string.Create(null, stackalloc char[TagPrefixLength + tagStr.Length], $"{TagPrefix}{tagStr}");
    }

    /// <summary>
    /// Creates a tag-based cache key for grouping related cache entries using span-based allocation.
    /// Useful for bulk invalidation of related caches.
    /// </summary>
    /// <param name="tags">The tags to include in the key.</param>
    public static string GenerateTagKey(ReadOnlySpan<string> tags)
    {
        if (tags.IsEmpty)
            throw new ArgumentException("At least one tag is required", nameof(tags));

        // Use StringBuilder to avoid intermediate string allocations
        var tagBuilder = new StringBuilder();
        for (int i = 0; i < tags.Length; i++)
        {
            if (i > 0)
                tagBuilder.Append(':');
            tagBuilder.Append(tags[i]);
        }

        var tagStr = tagBuilder.ToString();
        return string.Create(null, stackalloc char[TagPrefixLength + tagStr.Length], $"{TagPrefix}{tagStr}");
    }

    /// <summary>
    /// Extracts lock ID from a cache key.
    /// </summary>
    /// <param name="cacheKey">The cache key to extract from.</param>
    /// <returns>The lock ID, or null if the key doesn't start with the lock prefix.</returns>
    public static string? ExtractLockIdFromKey(string cacheKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(cacheKey);

        if (!cacheKey.StartsWith(Prefix))
            return null;

        return cacheKey[Prefix.Length..];
    }

    /// <summary>
    /// Extracts lock ID from a cache key using span-based parsing.
    /// </summary>
    /// <param name="cacheKey">The cache key to extract from.</param>
    /// <returns>The lock ID span, or empty if the key doesn't start with the lock prefix.</returns>
    public static ReadOnlySpan<char> ExtractLockIdFromKey(ReadOnlySpan<char> cacheKey)
    {
        if (cacheKey.IsEmpty)
            return default;

        if (!cacheKey.StartsWith(Prefix.AsSpan()))
            return default;

        return cacheKey[Prefix.Length..];
    }

    /// <summary>
    /// Determines if a cache key belongs to a lock.
    /// </summary>
    /// <param name="cacheKey">The cache key to check.</param>
    /// <returns>True if the key belongs to a lock; otherwise, false.</returns>
    public static bool IsLockKey(string cacheKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(cacheKey);

        if (!cacheKey.StartsWith(Prefix))
            return false;

        // Manual check for "name:" or "owner:" to avoid string allocations from Contains
        var suffix = cacheKey.AsSpan(Prefix.Length);
        return !suffix.StartsWith(NamePrefix) && !suffix.StartsWith(OwnerPrefix);
    }

    /// <summary>
    /// Determines if a cache key belongs to a lock using span-based allocation.
    /// </summary>
    /// <param name="cacheKey">The cache key to check.</param>
    /// <returns>True if the key belongs to a lock; otherwise, false.</returns>
    public static bool IsLockKey(ReadOnlySpan<char> cacheKey)
    {
        if (cacheKey.IsEmpty)
            return false;

        if (!cacheKey.StartsWith(Prefix.AsSpan()))
            return false;

        // Manual check for "name:" or "owner:" to avoid string allocations from Contains
        var suffix = cacheKey.Slice(Prefix.Length);
        return !suffix.StartsWith(NamePrefix.AsSpan()) && !suffix.StartsWith(OwnerPrefix.AsSpan());
    }

    /// <summary>
    /// Determines if a cache key belongs to metrics.
    /// </summary>
    /// <param name="cacheKey">The cache key to check.</param>
    /// <returns>True if the key belongs to metrics; otherwise, false.</returns>
    public static bool IsMetricsKey(string cacheKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(cacheKey);
        return cacheKey.StartsWith(MetricsPrefix);
    }

    /// <summary>
    /// Determines if a cache key belongs to metrics using span-based allocation.
    /// </summary>
    /// <param name="cacheKey">The cache key to check.</param>
    /// <returns>True if the key belongs to metrics; otherwise, false.</returns>
    public static bool IsMetricsKey(ReadOnlySpan<char> cacheKey)
    {
        if (cacheKey.IsEmpty)
            return false;
        return cacheKey.StartsWith(MetricsPrefix.AsSpan());
    }

    /// <summary>
    /// Computes SHA256 hash of a string for use in cache keys.
    /// Ensures consistent, compact key representation for complex data.
    /// </summary>
    /// <param name="input">The input string to hash.</param>
    /// <returns>The first 16 characters of the SHA256 hash in lowercase hex format.</returns>
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
    /// <param name="lockId">The lock ID.</param>
    /// <param name="ownerId">The owner ID.</param>
    /// <returns>Array of cache keys to invalidate.</returns>
    public static string[] GetKeysByAcquisition(string lockId, string ownerId)
    {
        ArgumentException.ThrowIfNullOrEmpty(lockId);
        ArgumentException.ThrowIfNullOrEmpty(ownerId);

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
    /// <param name="lockId">The lock ID.</param>
    /// <param name="ownerId">The owner ID.</param>
    /// <returns>Array of cache keys to invalidate.</returns>
    public static string[] GetKeysByRelease(string lockId, string ownerId)
    {
        ArgumentException.ThrowIfNullOrEmpty(lockId);
        ArgumentException.ThrowIfNullOrEmpty(ownerId);

        return new[]
        {
            CacheKeyGenerator.GenerateLockKey(lockId),
            CacheKeyGenerator.GenerateOwnerLocksKey(ownerId),
            CacheKeyGenerator.GenerateStatusKey(lockId)
        };
    }
}