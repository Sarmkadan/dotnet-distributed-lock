#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Repository;
using System.Text.Json;

namespace SarmKadan.DistributedLock.Backends.SQLite;

/// <summary>
/// SQLite-based implementation of the lock repository for lightweight, file-based locking.
/// </summary>
public sealed class SqliteLockRepository : ILockRepository, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteLockRepository> _logger;
    private readonly SemaphoreSlim _initSemaphore = new(1, 1);
    private bool _initialized = false;

    public SqliteLockRepository(string connectionString, ILogger<SqliteLockRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));

        _connectionString = connectionString;
        _logger.LogInformation("SQLite lock repository initialized with connection string: {ConnectionString}", connectionString);
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        await _initSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_initialized)
            {
                await InitializeSchemaAsync().ConfigureAwait(false);
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
        using (var connection = new SqliteConnection(_connectionString))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS distributed_locks (
                        lock_key TEXT PRIMARY KEY,
                        owner_id TEXT NOT NULL,
                        status INTEGER NOT NULL,
                        acquired_at TEXT NOT NULL,
                        expires_at TEXT NOT NULL,
                        renewed_at TEXT,
                        renewal_count INTEGER NOT NULL DEFAULT 0,
                        duration_seconds INTEGER NOT NULL,
                        metadata TEXT,
                        lock_data TEXT NOT NULL
                    );
                    CREATE INDEX IF NOT EXISTS idx_owner_id ON distributed_locks(owner_id);
                    CREATE INDEX IF NOT EXISTS idx_expires_at ON distributed_locks(expires_at);
                ";
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        _logger.LogInformation("SQLite schema initialized");
    }

    public async Task<bool> AcquireAsync(Lock @lock, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        INSERT OR IGNORE INTO distributed_locks
                        (lock_key, owner_id, status, acquired_at, expires_at, renewal_count, duration_seconds, lock_data)
                        VALUES (@key, @owner, @status, @acquired, @expires, 0, @duration, @data)
                    ";

                    command.Parameters.AddWithValue("@key", @lock.Key);
                    command.Parameters.AddWithValue("@owner", @lock.OwnerId);
                    command.Parameters.AddWithValue("@status", (int)@lock.Status);
                    command.Parameters.AddWithValue("@acquired", @lock.AcquiredAt.ToString("O"));
                    command.Parameters.AddWithValue("@expires", @lock.ExpiresAt.ToString("O"));
                    command.Parameters.AddWithValue("@duration", (int)@lock.Duration.TotalSeconds);
                    command.Parameters.AddWithValue("@data", JsonSerializer.Serialize(@lock));

                    var result = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    @lock.Status = Enums.LockStatus.Acquired;
                    return result > 0;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acquiring lock in SQLite: {LockKey}", @lock.Key);
            throw;
        }
    }

    public async Task<Lock?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT lock_data FROM distributed_locks WHERE lock_key = @key";
                    command.Parameters.AddWithValue("@key", key);

                    using (var reader = await command.ExecuteReaderAsync(cancellationToken))
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
            _logger.LogError(ex, "Error retrieving lock from SQLite: {LockKey}", key);
            throw;
        }
    }

    public async Task<Lock?> GetByKeyAndOwnerAsync(string key, string ownerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var @lock = await GetByKeyAsync(key, cancellationToken).ConfigureAwait(false);
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

    public async Task<bool> UpdateAsync(Lock @lock, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        UPDATE distributed_locks
                        SET status = @status, expires_at = @expires, renewed_at = @renewed,
                            renewal_count = @renewal_count, lock_data = @data
                        WHERE lock_key = @key
                    ";

                    command.Parameters.AddWithValue("@key", @lock.Key);
                    command.Parameters.AddWithValue("@status", (int)@lock.Status);
                    command.Parameters.AddWithValue("@expires", @lock.ExpiresAt.ToString("O"));
                    command.Parameters.AddWithValue("@renewed", @lock.RenewedAt?.ToString("O") ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@renewal_count", @lock.RenewalCount);
                    command.Parameters.AddWithValue("@data", JsonSerializer.Serialize(@lock));

                    var result = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    return result > 0;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating lock in SQLite: {LockKey}", @lock.Key);
            throw;
        }
    }

    public async Task<bool> RenewAsync(string key, string ownerId, TimeSpan newDuration, CancellationToken cancellationToken = default)
    {
        try
        {
            var @lock = await GetByKeyAndOwnerAsync(key, ownerId, cancellationToken).ConfigureAwait(false);
            if (@lock is null)
                return false;

            @lock.Renew(newDuration);
            return await UpdateAsync(@lock, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renewing lock in SQLite: {LockKey}", key);
            throw;
        }
    }

    public async Task<bool> ReleaseAsync(string key, string ownerId, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM distributed_locks WHERE lock_key = @key AND owner_id = @owner";
                    command.Parameters.AddWithValue("@key", key);
                    command.Parameters.AddWithValue("@owner", ownerId);

                    var result = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    return result > 0;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing lock in SQLite: {LockKey}", key);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var @lock = await GetByKeyAsync(key, cancellationToken).ConfigureAwait(false);
        return @lock is not null && !@lock.IsExpired;
    }

    public async Task<IEnumerable<Lock>> GetAllActiveLockAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureInitializedAsync().ConfigureAwait(false);
            var locks = new List<Lock>();

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT lock_data FROM distributed_locks WHERE expires_at > @now";
                    command.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));

                    using (var reader = await command.ExecuteReaderAsync(cancellationToken))
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
            _logger.LogError(ex, "Error retrieving all active locks from SQLite");
            throw;
        }
    }

    public async Task<IEnumerable<Lock>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var allLocks = await GetAllActiveLockAsync(cancellationToken).ConfigureAwait(false);
            return allLocks.Where(l => l.OwnerId == ownerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving locks by owner from SQLite: {OwnerId}", ownerId);
            throw;
        }
    }

    public async Task<int> DeleteExpiredLockAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM distributed_locks WHERE expires_at <= @now";
                    command.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));

                    return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expired locks from SQLite");
            throw;
        }
    }

    public async Task<int> ClearAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM distributed_locks";
                    return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing locks from SQLite");
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask;
    }
}
