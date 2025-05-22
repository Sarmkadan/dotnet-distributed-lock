#nullable enable

// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using System.Data;
using System.Text.Json;
using Npgsql;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using SarmKadan.DistributedLock.Models;

namespace SarmKadan.DistributedLock.Backends.PostgreSQL;

/// <summary>
/// Extension methods for <see cref="PostgresLockRepository"/> providing additional convenience and batch operations.
/// </summary>
public static class PostgresLockRepositoryExtensions
{
    /// <summary>
    /// Attempts to acquire a lock with automatic retry logic for transient failures.
    /// </summary>
    /// <param name="repository">The lock repository instance.</param>
    /// <param name="lock">The lock to acquire.</param>
    /// <param name="maxRetries">Maximum number of retry attempts.</param>
    /// <param name="retryDelay">Delay between retry attempts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if lock was acquired, false otherwise.</returns>
    public static async Task<bool> AcquireWithRetryAsync(
        this PostgresLockRepository repository,
        Lock @lock,
        int maxRetries = 3,
        TimeSpan? retryDelay = null,
        CancellationToken cancellationToken = default)
    {
        retryDelay ??= TimeSpan.FromMilliseconds(100);

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                return await repository.AcquireAsync(@lock, cancellationToken);
            }
            catch (Exception ex) when (attempt < maxRetries - 1)
            {
                // Log warning for transient failures
                repository.LogAcquisitionWarning(@lock.Key, attempt, maxRetries, ex);
                await Task.Delay(retryDelay.Value, cancellationToken);
            }
        }

        return false;
    }

    /// <summary>
    /// Attempts to release a lock with automatic retry logic for transient failures.
    /// </summary>
    /// <param name="repository">The lock repository instance.</param>
    /// <param name="key">The lock key.</param>
    /// <param name="ownerId">The owner identifier.</param>
    /// <param name="maxRetries">Maximum number of retry attempts.</param>
    /// <param name="retryDelay">Delay between retry attempts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if lock was released, false otherwise.</returns>
    public static async Task<bool> ReleaseWithRetryAsync(
        this PostgresLockRepository repository,
        string key,
        string ownerId,
        int maxRetries = 3,
        TimeSpan? retryDelay = null,
        CancellationToken cancellationToken = default)
    {
        retryDelay ??= TimeSpan.FromMilliseconds(100);

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                return await repository.ReleaseAsync(key, ownerId, cancellationToken);
            }
            catch (Exception ex) when (attempt < maxRetries - 1)
            {
                // Log warning for transient failures
                repository.LogReleaseWarning(key, ownerId, attempt, maxRetries, ex);
                await Task.Delay(retryDelay.Value, cancellationToken);
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the count of active locks matching the specified criteria.
    /// </summary>
    /// <param name="repository">The lock repository instance.</param>
    /// <param name="ownerId">Optional owner identifier to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Count of active locks.</returns>
    public static async Task<int> GetActiveLockCountAsync(
        this PostgresLockRepository repository,
        string? ownerId = null,
        CancellationToken cancellationToken = default)
    {
        var activeLocks = ownerId is null
            ? await repository.GetAllActiveLockAsync(cancellationToken)
            : await repository.GetByOwnerAsync(ownerId, cancellationToken);

        return activeLocks.Count();
    }

    /// <summary>
    /// Checks if any locks with the specified owner are currently active.
    /// </summary>
    /// <param name="repository">The lock repository instance.</param>
    /// <param name="ownerId">The owner identifier to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if owner has active locks, false otherwise.</returns>
    public static async Task<bool> HasActiveLocksAsync(
        this PostgresLockRepository repository,
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var count = await repository.GetActiveLockCountAsync(ownerId, cancellationToken);
        return count > 0;
    }

    /// <summary>
    /// Gets all locks (including expired) for the specified owner.
    /// </summary>
    /// <param name="repository">The lock repository instance.</param>
    /// <param name="ownerId">The owner identifier.</param>
    /// <param name="includeExpired">Whether to include expired locks.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of locks.</returns>
    public static async Task<IEnumerable<Lock>> GetAllLocksByOwnerAsync(
        this PostgresLockRepository repository,
        string ownerId,
        bool includeExpired = false,
        CancellationToken cancellationToken = default)
    {
        if (includeExpired)
        {
            // For expired locks, we need to query the database directly
            var locks = new List<Lock>();

            await using (var connection = new NpgsqlConnection(repository.GetConnectionString()))
            {
                await connection.OpenAsync(cancellationToken);
                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT lock_data FROM distributed_locks WHERE owner_id = @owner";
                    command.Parameters.AddWithValue("@owner", ownerId);

                    await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            var json = reader.GetString(0);
                            var @lock = JsonSerializer.Deserialize<Lock>(json);
                            if (@lock is not null)
                            {
                                locks.Add(@lock);
                            }
                        }
                    }
                }
            }

            return locks;
        }
        else
        {
            // Use existing method for active locks
            return await repository.GetByOwnerAsync(ownerId, cancellationToken);
        }
    }

    /// <summary>
    /// Gets the total count of locks in the repository.
    /// </summary>
    /// <param name="repository">The lock repository instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total count of locks.</returns>
    public static async Task<int> GetTotalLockCountAsync(
        this PostgresLockRepository repository,
        CancellationToken cancellationToken = default)
    {
        await using (var connection = new NpgsqlConnection(repository.GetConnectionString()))
        {
            await connection.OpenAsync(cancellationToken);
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM distributed_locks";
                return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            }
        }
    }

    /// <summary>
    /// Gets the count of expired locks that need cleanup.
    /// </summary>
    /// <param name="repository">The lock repository instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Count of expired locks.</returns>
    public static async Task<int> GetExpiredLockCountAsync(
        this PostgresLockRepository repository,
        CancellationToken cancellationToken = default)
    {
        await using (var connection = new NpgsqlConnection(repository.GetConnectionString()))
        {
            await connection.OpenAsync(cancellationToken);
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM distributed_locks WHERE expires_at <= now()";
                return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            }
        }
    }

    /// <summary>
    /// Gets all locks that are about to expire within the specified time window.
    /// </summary>
    /// <param name="repository">The lock repository instance.</param>
    /// <param name="timeWindow">Time window for upcoming expiration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of locks that will expire soon.</returns>
    public static async Task<IEnumerable<Lock>> GetLocksExpiringSoonAsync(
        this PostgresLockRepository repository,
        TimeSpan timeWindow,
        CancellationToken cancellationToken = default)
    {
        var threshold = DateTimeOffset.UtcNow.Add(timeWindow);

        var locks = new List<Lock>();

        await using (var connection = new NpgsqlConnection(repository.GetConnectionString()))
        {
            await connection.OpenAsync(cancellationToken);
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT lock_data
                    FROM distributed_locks
                    WHERE expires_at > now()
                      AND expires_at <= @threshold";
                command.Parameters.AddWithValue("@threshold", threshold.UtcDateTime);

                await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var json = reader.GetString(0);
                        if (JsonSerializer.Deserialize<Lock>(json) is Lock @lock)
                        {
                            locks.Add(@lock);
                        }
                    }
                }
            }
        }

        return locks;
    }

    /// <summary>
    /// Gets the oldest active lock by acquisition time.
    /// </summary>
    /// <param name="repository">The lock repository instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The oldest active lock, or null if none exist.</returns>
    public static async Task<Lock?> GetOldestActiveLockAsync(
        this PostgresLockRepository repository,
        CancellationToken cancellationToken = default)
    {
        await using (var connection = new NpgsqlConnection(repository.GetConnectionString()))
        {
            await connection.OpenAsync(cancellationToken);
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT lock_data
                    FROM distributed_locks
                    WHERE expires_at > now()
                    ORDER BY acquired_at ASC
                    LIMIT 1";

                await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                {
                    if (await reader.ReadAsync(cancellationToken))
                    {
                        var json = reader.GetString(0);
                        return JsonSerializer.Deserialize<Lock>(json);
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the newest active lock by acquisition time.
    /// </summary>
    /// <param name="repository">The lock repository instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newest active lock, or null if none exist.</returns>
    public static async Task<Lock?> GetNewestActiveLockAsync(
        this PostgresLockRepository repository,
        CancellationToken cancellationToken = default)
    {
        await using (var connection = new NpgsqlConnection(repository.GetConnectionString()))
        {
            await connection.OpenAsync(cancellationToken);
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT lock_data
                    FROM distributed_locks
                    WHERE expires_at > now()
                    ORDER BY acquired_at DESC
                    LIMIT 1";

                await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                {
                    if (await reader.ReadAsync(cancellationToken))
                    {
                        var json = reader.GetString(0);
                        return JsonSerializer.Deserialize<Lock>(json);
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the average lock duration across all active locks.
    /// </summary>
    /// <param name="repository">The lock repository instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Average lock duration in seconds, or 0 if no active locks.</returns>
    public static async Task<double> GetAverageLockDurationAsync(
        this PostgresLockRepository repository,
        CancellationToken cancellationToken = default)
    {
        await using (var connection = new NpgsqlConnection(repository.GetConnectionString()))
        {
            await connection.OpenAsync(cancellationToken);
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT AVG(duration_seconds)
                    FROM distributed_locks
                    WHERE expires_at > now()";

                var result = await command.ExecuteScalarAsync(cancellationToken);
                return result != DBNull.Value ? Convert.ToDouble(result) : 0;
            }
        }
    }

    private static void LogAcquisitionWarning(
        this PostgresLockRepository repository,
        string lockKey,
        int attempt,
        int maxRetries,
        Exception ex)
    {
        var logger = repository.GetLogger();
        logger.LogWarning(
            ex,
            "Failed to acquire lock {LockKey} (attempt {Attempt}/{MaxRetries}), retrying...",
            lockKey,
            attempt + 1,
            maxRetries);
    }

    private static void LogReleaseWarning(
        this PostgresLockRepository repository,
        string lockKey,
        string ownerId,
        int attempt,
        int maxRetries,
        Exception ex)
    {
        var logger = repository.GetLogger();
        logger.LogWarning(
            ex,
            "Failed to release lock {LockKey} owned by {OwnerId} (attempt {Attempt}/{MaxRetries}), retrying...",
            lockKey,
            ownerId,
            attempt + 1,
            maxRetries);
    }

    private static ILogger<PostgresLockRepository> GetLogger(this PostgresLockRepository repository)
    {
        // Helper to access the logger from the repository
        // This uses reflection to access the private field
        var field = typeof(PostgresLockRepository).GetField(
            "_logger",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (ILogger<PostgresLockRepository>)(field?.GetValue(repository) ?? NullLogger<PostgresLockRepository>.Instance);
    }

    private static string GetConnectionString(this PostgresLockRepository repository)
    {
        // Helper to access the connection string from the repository
        var field = typeof(PostgresLockRepository).GetField(
            "_connectionString",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (string)field?.GetValue(repository)!;
    }
}