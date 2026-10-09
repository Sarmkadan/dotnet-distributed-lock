#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using Microsoft.Extensions.Logging;
using Npgsql;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Repository;
using System.Text.Json;

namespace SarmKadan.DistributedLock.Backends.PostgreSQL;

/// <summary>
/// PostgreSQL-based implementation of the lock repository for SQL-based distributed locking.
/// </summary>
public sealed class PostgresLockRepository : ILockRepository, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresLockRepository> _logger;
    private readonly SemaphoreSlim _initSemaphore = new(1, 1);
    private bool _initialized = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresLockRepository"/> class.
    /// The database schema is created lazily on the first operation, not in the constructor.
    /// </summary>
    /// <param name="connectionString">The PostgreSQL connection string. Must not be null, empty, or whitespace.</param>
    /// <param name="logger">The logger used for diagnostics.</param>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="connectionString"/> is null, empty, or whitespace.</exception>
    public PostgresLockRepository(string connectionString, ILogger<PostgresLockRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));

        _connectionString = connectionString;
        _logger.LogInformation("PostgreSQL lock repository initialized with connection string: {ConnectionString}", connectionString);
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        await _initSemaphore.WaitAsync();
        try
        {
            if (!_initialized)
            {
                await InitializeSchemaAsync();
                _initialized = true;
            }
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    private async Task InitializeSchemaAsync()
    {
        await using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS distributed_locks (
                        lock_key TEXT PRIMARY KEY,
                        owner_id TEXT NOT NULL,
                        status INTEGER NOT NULL,
                        acquired_at TIMESTAMP NOT NULL,
                        expires_at TIMESTAMP NOT NULL,
                        renewed_at TIMESTAMP,
                        renewal_count INTEGER NOT NULL DEFAULT 0,
                        duration_seconds INTEGER NOT NULL,
                        metadata TEXT,
                        lock_data JSONB NOT NULL
                    );

                    CREATE INDEX IF NOT EXISTS idx_owner_id ON distributed_locks(owner_id);
                    CREATE INDEX IF NOT EXISTS idx_expires_at ON distributed_locks(expires_at);
                    CREATE INDEX IF NOT EXISTS idx_acquired_at ON distributed_locks(acquired_at);
                ";

                await command.ExecuteNonQueryAsync();
            }
        }

        _logger.LogInformation("PostgreSQL schema initialized");
    }

    /// <summary>
    /// Attempts to acquire a lock by inserting a row into the <c>distributed_locks</c> table.
    /// A PostgreSQL session-level advisory lock is taken for the duration of the attempt and is released
    /// immediately if the row insert does not succeed.
    /// </summary>
    /// <param name="lock">The lock to acquire. Its <see cref="Lock.Status"/> is set to <see cref="Enums.LockStatus.Acquired"/> on return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> if the row was inserted; <see langword="false"/> if a lock with the same key already exists.
    /// </returns>
    /// <exception cref="NpgsqlException">A database error occurred.</exception>
    public async Task<bool> AcquireAsync(Lock @lock, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureInitializedAsync();

            await using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken);

                // Acquire PostgreSQL session-level advisory lock
                var advisoryLockKey = GetAdvisoryLockKey(@lock.Key);
                await using (var advisoryCommand = connection.CreateCommand())
                {
                    advisoryCommand.CommandText = "SELECT pg_advisory_lock(@advisoryLockKey)";
                    advisoryCommand.Parameters.AddWithValue("@advisoryLockKey", advisoryLockKey);
                    await advisoryCommand.ExecuteNonQueryAsync(cancellationToken);
                }

                bool acquiredTableLock = false;
                try
                {
                    await using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                            INSERT INTO distributed_locks
                            (lock_key, owner_id, status, acquired_at, expires_at, renewal_count, duration_seconds, lock_data)
                            VALUES (@key, @owner, @status, @acquired, @expires, 0, @duration, @data::jsonb)
                            ON CONFLICT DO NOTHING
                        ";

                        command.Parameters.AddWithValue("@key", @lock.Key);
                        command.Parameters.AddWithValue("@owner", @lock.OwnerId);
                        command.Parameters.AddWithValue("@status", (int)@lock.Status);
                        command.Parameters.AddWithValue("@acquired", @lock.AcquiredAt);
                        command.Parameters.AddWithValue("@expires", @lock.ExpiresAt);
                        command.Parameters.AddWithValue("@duration", (int)@lock.Duration.TotalSeconds);
                        command.Parameters.AddWithValue("@data", JsonSerializer.Serialize(@lock));

                        var result = await command.ExecuteNonQueryAsync(cancellationToken);
                        acquiredTableLock = result > 0;
                    }
                }
                finally
                {
                    if (!acquiredTableLock)
                    {
                        // If table lock not acquired, release the advisory lock immediately
                        await using (var advisoryReleaseCommand = connection.CreateCommand())
                        {
                            advisoryReleaseCommand.CommandText = "SELECT pg_advisory_unlock(@advisoryLockKey)";
                            advisoryReleaseCommand.Parameters.AddWithValue("@advisoryLockKey", advisoryLockKey);
                            await advisoryReleaseCommand.ExecuteNonQueryAsync(cancellationToken);
                        }
                    }
                }

                @lock.Status = Enums.LockStatus.Acquired;
                return acquiredTableLock;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acquiring lock in PostgreSQL: {LockKey}", @lock.Key);
            throw;
        }
    }

    /// <summary>
    /// Retrieves a lock by its key, regardless of whether it has expired.
    /// </summary>
    /// <param name="key">The lock key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The deserialized lock if a row exists; otherwise, <see langword="null"/>.</returns>
    /// <exception cref="NpgsqlException">A database error occurred.</exception>
    public async Task<Lock?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureInitializedAsync();

            await using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT lock_data FROM distributed_locks WHERE lock_key = @key";
                    command.Parameters.AddWithValue("@key", key);

                    await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                    {
                        if (await reader.ReadAsync(cancellationToken))
                        {
                            var json = reader.GetString(0);
                            var @lock = JsonSerializer.Deserialize<Lock>(json);
                            return @lock;
                        }
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving lock from PostgreSQL: {LockKey}", key);
            throw;
        }
    }

    /// <summary>
    /// Retrieves a lock by key only if it is owned by the specified owner and has not expired.
    /// </summary>
    /// <param name="key">The lock key.</param>
    /// <param name="ownerId">The lock owner identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The lock if it exists, belongs to <paramref name="ownerId"/>, and is not expired; otherwise, <see langword="null"/>.</returns>
    /// <exception cref="NpgsqlException">A database error occurred.</exception>
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
            _logger.LogError(ex, "Error retrieving lock by key and owner: {LockKey}", key);
            throw;
        }
    }

    /// <summary>
    /// Persists the status, expiration, renewal data, and serialized state of an existing lock.
    /// </summary>
    /// <param name="lock">The lock to update. Identified by its <see cref="Lock.Key"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> if a row was updated; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="NpgsqlException">A database error occurred.</exception>
    public async Task<bool> UpdateAsync(Lock @lock, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureInitializedAsync();

            await using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        UPDATE distributed_locks
                        SET status = @status, expires_at = @expires, renewed_at = @renewed,
                            renewal_count = @renewal_count, lock_data = @data::jsonb
                        WHERE lock_key = @key
                    ";

                    command.Parameters.AddWithValue("@key", @lock.Key);
                    command.Parameters.AddWithValue("@status", (int)@lock.Status);
                    command.Parameters.AddWithValue("@expires", @lock.ExpiresAt);
                    command.Parameters.AddWithValue("@renewed", (object?)@lock.RenewedAt ?? DBNull.Value);
                    command.Parameters.AddWithValue("@renewal_count", @lock.RenewalCount);
                    command.Parameters.AddWithValue("@data", JsonSerializer.Serialize(@lock));

                    var result = await command.ExecuteNonQueryAsync(cancellationToken);
                    return result > 0;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating lock in PostgreSQL: {LockKey}", @lock.Key);
            throw;
        }
    }

    /// <summary>
    /// Extends the expiration of a lock held by the specified owner.
    /// The lock is loaded via <see cref="GetByKeyAndOwnerAsync"/>, renewed in memory, and then saved with <see cref="UpdateAsync"/>.
    /// </summary>
    /// <param name="key">The lock key.</param>
    /// <param name="ownerId">The lock owner identifier.</param>
    /// <param name="newDuration">The new duration applied to the lock.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> if the lock was renewed; <see langword="false"/> if it does not exist,
    /// is owned by another owner, or has expired.
    /// </returns>
    /// <exception cref="NpgsqlException">A database error occurred.</exception>
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
            _logger.LogError(ex, "Error renewing lock in PostgreSQL: {LockKey}", key);
            throw;
        }
    }

    /// <summary>
    /// Releases a lock held by the specified owner by deleting its row and unlocking the associated advisory lock.
    /// </summary>
    /// <param name="key">The lock key.</param>
    /// <param name="ownerId">The lock owner identifier. Only a lock with this owner is deleted.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> if a row was deleted; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="NpgsqlException">A database error occurred.</exception>
    public async Task<bool> ReleaseAsync(string key, string ownerId, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureInitializedAsync();

            await using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken);

                var releasedTableLock = false;
                try
                {
                    await using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM distributed_locks WHERE lock_key = @key AND owner_id = @owner";
                        command.Parameters.AddWithValue("@key", key);
                        command.Parameters.AddWithValue("@owner", ownerId);

                        var result = await command.ExecuteNonQueryAsync(cancellationToken);
                        releasedTableLock = result > 0;
                    }
                }
                finally
                {
                    // Always attempt to release the advisory lock if it was acquired by this session.
                    // PostgreSQL advisory locks are session-level, so they are automatically released on connection close,
                    // but explicit unlock is good practice and necessary if the connection is reused.
                    var advisoryLockKey = GetAdvisoryLockKey(key);
                    await using (var advisoryReleaseCommand = connection.CreateCommand())
                    {
                        advisoryReleaseCommand.CommandText = "SELECT pg_advisory_unlock(@advisoryLockKey)";
                        advisoryReleaseCommand.Parameters.AddWithValue("@advisoryLockKey", advisoryLockKey);
                        await advisoryReleaseCommand.ExecuteNonQueryAsync(cancellationToken);
                    }
                }

                return releasedTableLock;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing lock in PostgreSQL: {LockKey}", key);
            throw;
        }
    }

    /// <summary>
    /// Determines whether a non-expired lock exists for the specified key.
    /// </summary>
    /// <param name="key">The lock key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> if a lock exists and has not expired; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="NpgsqlException">A database error occurred.</exception>
    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var @lock = await GetByKeyAsync(key, cancellationToken);
        return @lock is not null && !@lock.IsExpired;
    }

    /// <summary>
    /// Retrieves all locks whose expiration is in the future.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of active locks. The list is empty if none exist.</returns>
    /// <exception cref="NpgsqlException">A database error occurred.</exception>
    public async Task<IEnumerable<Lock>> GetAllActiveLockAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureInitializedAsync();
            var locks = new List<Lock>();

            await using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT lock_data FROM distributed_locks WHERE expires_at > now()";

                    await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            var json = reader.GetString(0);
                            if (JsonSerializer.Deserialize<Lock>(json) is Lock @lock)
                                locks.Add(@lock);
                        }
                    }
                }
            }

            return locks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all active locks from PostgreSQL");
            throw;
        }
    }

    /// <summary>
    /// Retrieves all active (non-expired) locks owned by the specified owner.
    /// The owner filter is applied in memory over the result of <see cref="GetAllActiveLockAsync"/>.
    /// </summary>
    /// <param name="ownerId">The lock owner identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of locks owned by <paramref name="ownerId"/>. The collection is empty if none match.</returns>
    /// <exception cref="NpgsqlException">A database error occurred.</exception>
    public async Task<IEnumerable<Lock>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var allLocks = await GetAllActiveLockAsync(cancellationToken);
            return allLocks.Where(l => l.OwnerId == ownerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving locks by owner from PostgreSQL: {OwnerId}", ownerId);
            throw;
        }
    }

    /// <summary>
    /// Deletes all locks whose expiration is at or before the current database time.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of deleted rows.</returns>
    /// <exception cref="NpgsqlException">A database error occurred.</exception>
    public async Task<int> DeleteExpiredLockAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureInitializedAsync();

            await using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM distributed_locks WHERE expires_at <= now()";
                    return await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expired locks from PostgreSQL");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Lock>> GetExpiredLocksAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureInitializedAsync();
            var locks = new List<Lock>();

            await using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT lock_data FROM distributed_locks WHERE expires_at <= now()";

                    await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            var json = reader.GetString(0);
                            if (JsonSerializer.Deserialize<Lock>(json) is Lock @lock)
                                locks.Add(@lock);
                        }
                    }
                }
            }

            return locks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expired locks from PostgreSQL");
            throw;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null or empty.</exception>
    public async Task<bool> DeleteLockIfExpirationMatchesAsync(string key, DateTime expectedExpiresAt, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        try
        {
            await EnsureInitializedAsync();

            await using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM distributed_locks WHERE lock_key = @key AND expires_at = @expected";
                    command.Parameters.AddWithValue("@key", key);
                    command.Parameters.AddWithValue("@expected", expectedExpiresAt);

                    var result = await command.ExecuteNonQueryAsync(cancellationToken);
                    return result > 0;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting lock with compare-and-delete guard from PostgreSQL: {LockKey}", key);
            throw;
        }
    }

    /// <summary>
    /// Deletes every lock row from the <c>distributed_locks</c> table.
    /// This does not release the PostgreSQL advisory locks held by other sessions.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of deleted rows.</returns>
    /// <exception cref="NpgsqlException">A database error occurred.</exception>
    public async Task<int> ClearAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureInitializedAsync();

            await using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM distributed_locks";
                    return await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing locks from PostgreSQL");
            throw;
        }
    }

    /// <summary>
    /// Checks whether the given fencing token is the current token of a non-expired lock.
    /// </summary>
    /// <param name="key">The lock key.</param>
    /// <param name="fencingToken">The fencing token sequence number to validate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> if the lock exists, has not expired, and its sequence number matches; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="NpgsqlException">A database error occurred.</exception>
    public async Task<bool> ValidateFencingTokenAsync(string key, ulong fencingToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var @lock = await GetByKeyAsync(key, cancellationToken);
            return @lock is not null && !@lock.IsExpired && @lock.FencingToken?.SequenceNumber == (long)fencingToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating fencing token for lock: {LockKey}", key);
            throw;
        }
    }

    /// <summary>
    /// Releases resources used by the repository. Connections are opened and disposed per operation,
    /// so this method currently has no resources to release.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> that completes immediately.</returns>
    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask;
    }

    private int GetAdvisoryLockKey(string lockKey)
    {
        // Use a consistent hashing strategy for the advisory lock key
        // A simple string hash code should be sufficient for advisory locks
        // as collisions are handled by the database internally.
        return lockKey.GetHashCode();
    }
}
