# SQLite Backend (`SqliteLockRepository`)

`SqliteLockRepository` (`src/Backends/SQLite/SqliteLockRepository.cs`) implements `ILockRepository` on top of a single SQLite database file, using `Microsoft.Data.Sqlite`. It is intended for single-host or embedded deployments where an external Redis or PostgreSQL server is not wanted.

This document describes the schema the repository expects, how to configure it, and how its methods behave. It describes the current code; it does not propose changes.

## When to use it

- One host, or several processes on the same host that share a file system.
- Embedded or development setups with no external lock server.

SQLite locks only coordinate processes that can open the same database file. It is not suitable for coordinating across machines, and file-share (SMB/NFS) locking is not something this backend tests for.

## Configuration

### Registration

The backend is selected through `BackendType.SQLite` in the DI extensions:

```csharp
services.AddDistributedLock(BackendType.SQLite, connectionString);
```

Internally this calls `AddSqliteRepository`, which registers `SqliteLockRepository` as a singleton `ILockRepository`. The logger is resolved as `ILogger<SqliteLockRepository>` from the container.

### Connection string

The connection string is passed unchanged to `new SqliteConnection(...)`. A typical value:

```
Data Source=/var/lib/myapp/locks.db
```

Constructor rules:

- `logger` must not be null (`ArgumentNullException`).
- `connectionString` must not be null, empty, or whitespace (`ArgumentException`).
- The constructor logs the connection string at Information level. Do not put secrets (for example, passwords) into this string if logs are shared.

The repository does not set `Pooling`, `Cache`, `Mode`, `Foreign Keys`, `Journal Mode` (WAL) or `Default Timeout` itself. Those are whatever the connection string says, or the `Microsoft.Data.Sqlite` defaults. Set them in the connection string if you need them, for example `Mode=ReadWriteCreate;Cache=Shared`.

## Schema

The schema is created lazily. Before the first operation, `EnsureInitializedAsync` runs `InitializeSchemaAsync` once per repository instance, guarded by a `SemaphoreSlim`. The statement is idempotent (`IF NOT EXISTS`), so restarting against an existing file is safe.

```sql
CREATE TABLE IF NOT EXISTS distributed_locks (
    lock_key         TEXT    PRIMARY KEY,
    owner_id         TEXT    NOT NULL,
    status           INTEGER NOT NULL,
    acquired_at      TEXT    NOT NULL,
    expires_at       TEXT    NOT NULL,
    renewed_at       TEXT,
    renewal_count    INTEGER NOT NULL DEFAULT 0,
    duration_seconds INTEGER NOT NULL,
    metadata         TEXT,
    lock_data        TEXT    NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_owner_id   ON distributed_locks(owner_id);
CREATE INDEX IF NOT EXISTS idx_expires_at ON distributed_locks(expires_at);
```

### Column notes

| Column | Written by | Notes |
|---|---|---|
| `lock_key` | Acquire | Primary key. One row per key, so at most one lock per key at any time. |
| `owner_id` | Acquire | Used by `ReleaseAsync`, `GetByOwnerAsync` (indirectly), and the owner check in `GetByKeyAndOwnerAsync`. |
| `status` | Acquire, Update | `LockStatus` cast to `int`. |
| `acquired_at` | Acquire | ISO 8601 (`"O"` format). |
| `expires_at` | Acquire, Update | ISO 8601 (`"O"` format). Used for expiry filters and compare-and-delete. |
| `renewed_at` | Update | ISO 8601, or `NULL` if never renewed. |
| `renewal_count` | Acquire (0), Update | Copied from the `Lock` object on update. |
| `duration_seconds` | Acquire | Stored as `(int)TotalSeconds`, so sub-second precision is lost. Written only on insert; not updated by `UpdateAsync` or `RenewAsync`. |
| `metadata` | none | Declared but never written by this repository. Always `NULL`. |
| `lock_data` | Acquire, Update | Full `Lock` object serialized with `System.Text.Json`. This is the source of truth for reads. |

### Timestamp format

All timestamps are stored as strings produced by `DateTime.ToString("O")`. Expiry filters are plain string comparisons in SQL (`expires_at > @now`, `expires_at <= @now`), which is only correct if every value uses the same format and offset. The repository compares against `DateTime.UtcNow.ToString("O")`, so values written from `Lock.ExpiresAt` must be UTC for those filters to be reliable. Passing a local-time `DateTime` will produce values with a different offset and can misorder rows.

### Indexes

- `idx_expires_at` backs the expiry queries (`GetAllActiveLockAsync`, `GetExpiredLocksAsync`, `DeleteExpiredLockAsync`).
- `idx_owner_id` is created but no current query filters on `owner_id` alone. `ReleaseAsync` uses the primary key plus `owner_id`.

## Behavior

Every public method calls `EnsureInitializedAsync` first (except `ExistsAsync`, `GetByKeyAsync`, `GetByKeyAndOwnerAsync` and `ValidateFencingTokenAsync`, which call `GetByKeyAsync` and inherit its initialization). Each operation opens a new `SqliteConnection`, runs one statement, and disposes the connection. There is no connection reuse. All methods log and rethrow on exception.

### Operation summary

| Method | SQL | Returns `true` when | Expiry checked in SQL? |
|---|---|---|---|
| `AcquireAsync` | `INSERT OR IGNORE` | Row inserted | No (insert only) |
| `GetByKeyAsync` | `SELECT lock_data WHERE lock_key = @key` | Returns `Lock` or `null` | No |
| `GetByKeyAndOwnerAsync` | Via `GetByKeyAsync` | Returns `Lock` if owner matches and `!IsExpired` | Model check (`IsExpired`) |
| `UpdateAsync` | `UPDATE ... WHERE lock_key = @key` | Row updated | No |
| `RenewAsync` | Get, `Renew`, Update | Row updated | Via `GetByKeyAndOwnerAsync` |
| `ReleaseAsync` | `DELETE WHERE lock_key AND owner_id` | Row deleted | No |
| `ExistsAsync` | Via `GetByKeyAsync` | Lock found and `!IsExpired` | Model check |
| `GetAllActiveLockAsync` | `SELECT ... WHERE expires_at > @now` | Returns list | Yes |
| `GetByOwnerAsync` | Via `GetAllActiveLockAsync`, filtered in memory | Returns list | Yes |
| `GetExpiredLocksAsync` | `SELECT ... WHERE expires_at <= @now` | Returns list | Yes (inverse) |
| `DeleteExpiredLockAsync` | `DELETE WHERE expires_at <= @now` | Returns row count | Yes |
| `DeleteLockIfExpirationMatchesAsync` | `DELETE WHERE lock_key AND expires_at = @expected` | Row deleted | Compare-and-delete |
| `ClearAllAsync` | `DELETE FROM distributed_locks` | Returns row count | No |
| `ValidateFencingTokenAsync` | Via `GetByKeyAsync` | Lock exists, not expired, and `FencingToken.SequenceNumber == token` | Model check |

### Acquire

`AcquireAsync` uses `INSERT OR IGNORE`. If `lock_key` already exists, SQLite silently skips the insert, the affected-row count is 0, and the method returns `false`. There is no separate expired-lock takeover in this method: an expired row that still exists blocks acquisition until something removes it (`DeleteExpiredLockAsync`, `DeleteLockIfExpirationMatchesAsync`, `ReleaseAsync`, or `ClearAllAsync`). Callers that need takeover of expired locks must perform that cleanup first.

On success the method also sets `@lock.Status = LockStatus.Acquired` on the in-memory object, regardless of the return value. Note that this assignment happens after the insert, so it is also applied when the insert was ignored and the method returns `false`.

### Get

`GetByKeyAsync` returns the stored `Lock` deserialized from `lock_data`, including expired locks. Use `GetByKeyAndOwnerAsync` or `ExistsAsync` if expiry must be respected.

### Update and renew

`UpdateAsync` writes `status`, `expires_at`, `renewed_at`, `renewal_count` and `lock_data`. It does not update `owner_id`, `acquired_at`, or `duration_seconds`. It returns `false` if no row matches the key. It does not check owner; the caller must.

`RenewAsync` is a read-modify-write:

1. `GetByKeyAndOwnerAsync` (returns `null` if the owner differs or the lock is expired).
2. `Lock.Renew(newDuration)` on the in-memory object.
3. `UpdateAsync`.

The read and the write are separate statements with no transaction. Two processes renewing or releasing at the same moment can interleave between steps. Treat the result as best-effort under contention.

### Release

`ReleaseAsync` deletes only when both `lock_key` and `owner_id` match. A non-owner call returns `false` and leaves the row in place. Expired rows are deleted too if the owner matches, even though the lock is no longer valid.

### Expiry and cleanup

Expiry is determined by comparing `expires_at` to the current UTC time, as described above. Nothing runs cleanup automatically inside this repository. To remove stale rows, call `DeleteExpiredLockAsync` periodically, or use `DeleteLockIfExpirationMatchesAsync` to remove one specific row only if its `expires_at` still equals the value you read. That compare-and-delete guard protects against removing a lock that was renewed after you read it.

`expires_at` equality in the compare-and-delete relies on identical string formatting. Pass the same `DateTime` value that was read from the row (kind and precision included).

### Fencing tokens

`ValidateFencingTokenAsync` reads the lock and compares `FencingToken.SequenceNumber` with the supplied token, cast to `long`. This repository does not persist a dedicated fencing column; the sequence number lives inside `lock_data`. A token value above `long.MaxValue` will not match.

### Concurrency and transactions

- Each statement runs on its own connection in autocommit mode. There are no explicit transactions.
- `INSERT OR IGNORE` on the primary key is the only atomic acquire step. Read-then-write sequences (`RenewAsync`, `ValidateFencingTokenAsync`, `GetByOwnerAsync`) are not atomic.
- Concurrent writers from different processes rely on SQLite's file locking. The repository does not set a busy timeout, so the `Microsoft.Data.Sqlite` default applies. If you see `SQLITE_BUSY` under load, add a `Default Timeout` to the connection string (not tested in this repository).
- `DisposeAsync` does not close any connection, because the repository opens and closes connections per call.

### Errors

All methods catch `Exception`, log at Error level with the lock key where applicable, and rethrow the original exception. No method converts SQLite errors into domain exceptions. Callers should expect `Microsoft.Data.Sqlite.SqliteException` and `System.Text.Json.JsonException` (if `lock_data` is corrupt).

## Operational notes

- Back up the file with SQLite's online backup API or with the database closed. Copying a live file without its `-wal` and `-shm` companions (if WAL is enabled via the connection string) can produce an inconsistent copy.
- A stale file left by a crashed process will hold locks until `expires_at` passes and cleanup runs. Clear it deliberately with `ClearAllAsync` only when no owner is active.
- Put the database on local disk. Network file systems do not reliably honor SQLite's locking.

## Related

- `docs/RedisLockRepository.md` and `docs/PostgresLockRepositoryExtensions.md` for the other backends.
- `docs/SqliteLockRepository.md` for the per-method API reference.
- `docs/FencingToken.md` for the fencing token model.
