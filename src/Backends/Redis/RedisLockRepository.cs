#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using StackExchange.Redis;
using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Repository;
using System.Text.Json;

namespace SarmKadan.DistributedLock.Backends.Redis;

/// <summary>
/// Redis-based implementation of the lock repository with high-performance distributed locking.
/// </summary>
public sealed class RedisLockRepository : ILockRepository, IAsyncDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly ILogger<RedisLockRepository> _logger;
    private readonly string _keyPrefix;

    public RedisLockRepository(string connectionString, ILogger<RedisLockRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));

        _redis = ConnectionMultiplexer.Connect(connectionString);
        _database = _redis.GetDatabase();
        _keyPrefix = Constants.LockConstants.LockKeyPrefix;

        _logger.LogInformation("Redis lock repository initialized with connection string: {ConnectionString}", connectionString);
    }

    public async Task<bool> AcquireAsync(Lock @lock, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = GetRedisKey(@lock.Key);
            var expiry = @lock.ExpiresAt - DateTime.UtcNow;

            // Serialize with pending status first - only update to Acquired after SET NX succeeds
            var value = SerializeLock(@lock);
            var success = await _database.StringSetAsync(key, value, expiry, When.NotExists);

            if (success)
            {
                @lock.Status = Enums.LockStatus.Acquired;
                // Re-serialize with correct status and update the Redis entry
                var updatedValue = SerializeLock(@lock);
                await _database.StringSetAsync(key, updatedValue, expiry, When.Exists);
                _logger.LogDebug("Lock acquired in Redis: {LockKey}", @lock.Key);
            }
            else
            {
                _logger.LogDebug("Failed to acquire lock in Redis (already exists): {LockKey}", @lock.Key);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acquiring lock in Redis: {LockKey}", @lock.Key);
            throw;
        }
    }

    public async Task<Lock?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetRedisKey(key);
            var value = await _database.StringGetAsync(redisKey);

            if (!value.IsNull)
            {
                var @lock = DeserializeLock(value.ToString());
                if (@lock is not null && !@lock.IsExpired)
                    return @lock;

                // Delete expired lock
                await _database.KeyDeleteAsync(redisKey);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving lock from Redis: {LockKey}", key);
            throw;
        }
    }

    public async Task<Lock?> GetByKeyAndOwnerAsync(string key, string ownerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var @lock = await GetByKeyAsync(key, cancellationToken);
            if (@lock is not null && @lock.OwnerId == ownerId && !@lock.IsExpired)
                return @lock;

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving lock by key and owner: {LockKey}, {OwnerId}", key, ownerId);
            throw;
        }
    }

    public async Task<bool> UpdateAsync(Lock @lock, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = GetRedisKey(@lock.Key);
            var value = SerializeLock(@lock);
            var expiry = @lock.ExpiresAt - DateTime.UtcNow;

            await _database.StringSetAsync(key, value, expiry);
            _logger.LogDebug("Lock updated in Redis: {LockKey}", @lock.Key);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating lock in Redis: {LockKey}", @lock.Key);
            throw;
        }
    }

    public async Task<bool> RenewAsync(string key, string ownerId, TimeSpan newDuration, CancellationToken cancellationToken = default)
    {
        try
        {
            var @lock = await GetByKeyAndOwnerAsync(key, ownerId, cancellationToken);
            if (@lock is null)
                return false;

            @lock.Renew(newDuration);
            return await UpdateAsync(@lock, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renewing lock in Redis: {LockKey}", key);
            throw;
        }
    }

    public async Task<bool> ReleaseAsync(string key, string ownerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var @lock = await GetByKeyAsync(key, cancellationToken);
            if (@lock is null || @lock.OwnerId != ownerId)
                return false;

            var redisKey = GetRedisKey(key);
            var deleted = await _database.KeyDeleteAsync(redisKey);
            _logger.LogDebug("Lock released in Redis: {LockKey}", key);
            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing lock in Redis: {LockKey}", key);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var @lock = await GetByKeyAsync(key, cancellationToken);
            return @lock is not null && !@lock.IsExpired;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking lock existence in Redis: {LockKey}", key);
            throw;
        }
    }

    public async Task<IEnumerable<Lock>> GetAllActiveLockAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys = server.Keys(pattern: $"{_keyPrefix}*");
            var locks = new List<Lock>();

            foreach (var key in keys)
            {
                var value = await _database.StringGetAsync(key);
                if (!value.IsNull)
                {
                    var @lock = DeserializeLock(value.ToString());
                    if (@lock is not null && !@lock.IsExpired)
                        locks.Add(@lock);
                }
            }

            return locks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all active locks from Redis");
            throw;
        }
    }

    public async Task<IEnumerable<Lock>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var allLocks = await GetAllActiveLockAsync(cancellationToken);
            return allLocks.Where(l => l.OwnerId == ownerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving locks by owner from Redis: {OwnerId}", ownerId);
            throw;
        }
    }

    public async Task<int> DeleteExpiredLockAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys = server.Keys(pattern: $"{_keyPrefix}*");
            int deletedCount = 0;

            foreach (var key in keys)
            {
                var value = await _database.StringGetAsync(key);
                if (!value.IsNull)
                {
                    var @lock = DeserializeLock(value.ToString());
                    if (@lock is not null && @lock.IsExpired)
                    {
                        await _database.KeyDeleteAsync(key);
                        deletedCount++;
                    }
                }
            }

            _logger.LogInformation("Deleted {DeletedCount} expired locks from Redis", deletedCount);
            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expired locks from Redis");
            throw;
        }
    }

    public async Task<int> ClearAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys = server.Keys(pattern: $"{_keyPrefix}*");
            int count = 0;

            foreach (var key in keys)
            {
                await _database.KeyDeleteAsync(key);
                count++;
            }

            _logger.LogInformation("Cleared {Count} locks from Redis", count);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing locks from Redis");
            throw;
        }
    }

    private string GetRedisKey(string lockKey) => $"{_keyPrefix}{lockKey}";

    private string SerializeLock(Lock @lock)
    {
        return JsonSerializer.Serialize(@lock);
    }

    private Lock? DeserializeLock(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Lock>(json);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializing lock from JSON");
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_redis is not null)
        {
            await _redis.CloseAsync();
            _redis.Dispose();
        }
    }
}
