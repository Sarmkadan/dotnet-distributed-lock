# PostgreSQL Backend (`PostgresLockRepository`)

`PostgresLockRepository` (`src/Backends/PostgreSQL/PostgresLockRepository.cs`) implements `ILockRepository` and `IAsyncDisposable` on top of a PostgreSQL table, using `Npgsql` (8.0.4). It is intended for deployments that already run PostgreSQL and want locks in the same database.

This document describes the schema the repository creates, how to configure it, and how its methods behave. It describes the current code; it does not propose changes. Known defects are called out in the relevant sections and in "Known issues" at the end.

## When to use it

- Several processes or hosts that share one PostgreSQL database.
- Systems that already depend on PostgreSQL and do not want another lock server.

Exclusivity of a lock key is enforced by the primary key on the table, so it holds across processes and hosts. The advisory-lock layer described below does not add reliable cross-process serialization (see Known issues).

## Configuration

### Registration

The backend is selected through `BackendType.PostgreSQL` in the DI extensions:

```csharp
services.AddDistributedLock(BackendType.PostgreSQL, connectionString);
```

Internally this calls `AddPostgresRepository`, which registers `PostgresLockRepository` as a singleton `ILockRepository`. The logger is resolved as `ILogger<PostgresLockRepository>` from the container.

Because the registration is a singleton, one instance (and one `SemaphoreSlim` guarding schema creation) is shared by the application.

### Connection string

The connection string is passed unchanged to `new NpgsqlConnection(...)`. A typical value:

```
Host=db.internal;Port=5432;Database=app;Username=app_locks;Password=...;Pooling=true
```

Constructor rules:

- `logger` must not be null (`ArgumentNullException`).
- `connectionString` must not be null, empty, or whitespace (`ArgumentException`).
- The constructor logs the full connection string at Information level, including any password in it. Use a credential source that keeps the password out of the string, or make sure logs are not shared.

The repository does not set `Pooling`, `Maximum Pool Size`, `Command Timeout`, `Application Name`, `SSL Mode` or `Search Path` itself. Those are whatever the connection string says, or the Npgsql defaults. In particular, the schema is created in the default search path (usually `public`); there is no schema option.

The user needs privileges to `CREATE TABLE` and `CREATE INDEX` on first use, unless the table already exists (`CREATE ... IF NOT EXISTS` is a no-op then).

## Schema

The schema is created lazily. Before most operations, `EnsureInitializedAsync` runs `InitializeSchemaAsync` once per repository instance. The initialization is guarded by a `SemaphoreSlim(1, 1)` with a double-check on `_initialized`. The statements are idempotent (`IF NOT EXISTS`), so restarting against an existing database is safe.

```sql
CREATE TABLE IF NOT EXISTS distributed_locks (
    lock_key         TEXT PRIMARY KEY,
    owner_id         TEXT NOT NULL,
    status           INTEGER NOT NULL,
    acquired_at      TIMESTAMP NOT NULL,
    expires_at       TIMESTAMP NOT NULL,
    renewed_at       TIMESTAMP,
    renewal_count    INTEGER NOT NULL DEFAULT 0,
    duration_seconds INTEGER NOT NULL,
    metadata         TEXT,
    lock_data        JSONB NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_owner_id    ON distributed_locks(owner_id);
CREATE INDEX IF NOT EXISTS idx_expires_at  ON distributed_locks(expires_at);
CREATE INDEX IF NOT EXISTS idx_acquired_at ON distributed_locks(acquired_at);
```

Schema creation runs on an ad-hoc connection and logs "PostgreSQL schema initialized" on success. If it fails, the exception propagates and `_initialized` stays `false`, so the next call retries.

### Column notes

| Column | Written by | Notes |
|---|---|---|
| `lock_key` | Acquire | Primary key. One row per key, so at most one lock per key at any time. |
| `owner_id` | Acquire | Used by `ReleaseAsync` (with `lock_key`) and by the owner check in `GetByKeyAndOwnerAsync`. |
| `status` | Acquire, Update | `LockStatus` cast to `int`. |
| `acquired_at` | Acquire | Bound as a `DateTime` parameter into a `TIMESTAMP` (without time zone) column. |
| `expires_at` | Acquire, Update | Same binding as `acquired_at`. Used for expiry filters and compare-and-delete. |
| `renewed_at` | Update | `NULL` if never renewed. |
| `renewal_count` | Acquire (0), Update | Copied from the `Lock` object on update. |
| `duration_seconds` | Acquire | Stored as `(int)TotalSeconds`, so sub-second precision is lost. Written only on insert; not updated by `UpdateAsync` or `RenewAsync`. |
| `metadata` | none | Declared but never written by this repository. Always `NULL`. |
| `lock_data` | Acquire, Update | Full `Lock` object serialized with `System.Text.Json` and cast to `jsonb`. This is the source of truth for reads. |

### Timestamp handling

`acquired_at`, `expires_at` and `renewed_at` are `TIMESTAMP WITHOUT TIME ZONE`. The repository does not normalize `DateTime.Kind` before binding. Two consequences:

- Npgsql 8 rejects a `DateTime` with `Kind = Utc` bound to a `timestamp without time zone` parameter in some configurations. Whether this affects a given deployment depends on the `Lock` values it produces. Verify this against a real database before relying on the backend (not verified in this document).
- Expiry filters compare these columns with `now()`, which is `TIMESTAMPTZ`. PostgreSQL converts the `TIMESTAMP` value using the session `TimeZone` setting. If the session time zone differs between writers and readers, expiry results shift by the offset. Pin the time zone in the connection string (`Timezone=UTC`) or at the database/role level.

### Indexes

- `idx_expires_at` backs the expiry queries (`GetAllActiveLockAsync`, `GetExpiredLocksAsync`, `DeleteExpiredLockAsync`).
- `idx_owner_id` is created but no current query filters on `owner_id` alone. `ReleaseAsync` uses the primary key.
- `idx_acquired_at` is created but no query in this repository filters or orders by `acquired_at`.

## Behavior

These methods call `EnsureInitializedAsync` before touching the table: `AcquireAsync`, `UpdateAsync`, `ReleaseAsync`, `GetAllActiveLockAsync`, `GetExpiredLocksAsync`, `DeleteExpiredLockAsync`, `DeleteLockIfExpirationMatchesAsync`, and `ClearAllAsync`. `GetByKeyAsync` does not; it queries directly. Its callers (`GetByKeyAndOwnerAsync`, `ExistsAsync`, `ValidateFencingTokenAsync`, and `RenewAsync` through `GetByKeyAndOwnerAsync`) therefore also skip initialization. `RenewAsync` reaches `UpdateAsync` and so is initialized on the write path. `GetByOwnerAsync` is initialized through `GetAllActiveLockAsync`. In practice, a read before any write can run against a missing table and fail with a PostgreSQL error (`42P01`).

Each operation opens a new `NpgsqlConnection` (drawn from the Npgsql pool) and disposes it when done. All methods log at Error level and rethrow on exception.

### Operation summary

| Method | SQL | Returns `true` when | Expiry checked in SQL? |
|---|---|---|---|
| `AcquireAsync` | Advisory lock, then `INSERT ... ON CONFLICT DO NOTHING` | Row inserted | No (insert only) |
| `GetByKeyAsync` | `SELECT lock_data WHERE lock_key = @key` | Returns `Lock` or `null` | No |
| `GetByKeyAndOwnerAsync` | Via `GetByKeyAsync` | Returns `Lock` if owner matches and `!IsExpired` | Model check |
| `UpdateAsync` | `UPDATE ... WHERE lock_key = @key` | Row updated | No |
| `RenewAsync` | Get, `Renew`, Update | Row updated | Via `GetByKeyAndOwnerAsync` |
| `ReleaseAsync` | Advisory unlock, `DELETE WHERE lock_key AND owner_id` | Row deleted | No |
| `ExistsAsync` | Via `GetByKeyAsync` | Lock found and `!IsExpired` | Model check |
| `GetAllActiveLockAsync` | `SELECT ... WHERE expires_at > now()` | Returns list | Yes |
| `GetByOwnerAsync` | Via `GetAllActiveLockAsync`, filtered in memory | Returns list | Yes |
| `GetExpiredLocksAsync` | `SELECT ... WHERE expires_at <= now()` | Returns list | Yes (inverse) |
| `DeleteExpiredLockAsync` | `DELETE WHERE expires_at <= now()` | Returns row count | Yes |
| `DeleteLockIfExpirationMatchesAsync` | `DELETE WHERE lock_key AND expires_at = @expected` | Row deleted | Compare-and-delete |
| `ClearAllAsync` | `DELETE FROM distributed_locks` | Returns row count | No |
| `ValidateFencingTokenAsync` | Via `GetByKeyAsync` | Lock exists, not expired, and `FencingToken.SequenceNumber == token` | Model check |

### Acquire

`AcquireAsync` does the following on one connection:

1. `SELECT pg_advisory_lock(@advisoryLockKey)`. This is a blocking, session-level advisory lock. It has no timeout; only the `CancellationToken` bounds the wait.
2. `INSERT INTO distributed_locks (...) VALUES (...) ON CONFLICT DO NOTHING`. The `lock_data` value is `JsonSerializer.Serialize(lock)` cast to `jsonb`.
3. If the insert did not create a row, `SELECT pg_advisory_unlock(...)` runs. If it did, the advisory lock is intentionally not released here.
4. `Status` is set to `LockStatus.Acquired` on the in-memory object, regardless of the result, and the method returns the insert result.

The `ON CONFLICT DO NOTHING` clause has no target, so any unique violation (in practice the primary key) is ignored. An existing row, expired or not, blocks acquisition until something removes it (`DeleteExpiredLockAsync`, `DeleteLockIfExpirationMatchesAsync`, `ReleaseAsync`, or `ClearAllAsync`). There is no expired-lock takeover in this method.

The advisory key is `lockKey.GetHashCode()` (see Known issues). Because step 1 blocks, a second caller for the same key in the same process waits while the first holder's advisory lock is still held. Given Known issue 2, that wait can be unbounded. Callers that do not want to wait should pass a `CancellationToken` with a timeout.

### Get

`GetByKeyAsync` returns the `Lock` deserialized from `lock_data`, including expired locks. Use `GetByKeyAndOwnerAsync` or `ExistsAsync` if expiry must be respected. `GetByKeyAndOwnerAsync` also verifies that `OwnerId` matches.

### Update and renew

`UpdateAsync` writes `status`, `expires_at`, `renewed_at`, `renewal_count` and `lock_data`. It does not update `owner_id`, `acquired_at`, or `duration_seconds`. It returns `false` if no row matches the key. It does not check the owner; the caller must.

`RenewAsync` is a read-modify-write:

1. `GetByKeyAndOwnerAsync` (returns `null` if the owner differs or the lock is expired, and `RenewAsync` then returns `false`).
2. `Lock.Renew(newDuration)` on the in-memory object.
3. `UpdateAsync`.

The read and the write are separate statements in autocommit mode with no transaction or row lock. Two callers renewing or releasing at the same moment can interleave between steps. Treat the result as best-effort under contention.

### Release

`ReleaseAsync` deletes only when both `lock_key` and `owner_id` match. A non-owner call returns `false` and leaves the row in place. Expired rows are deleted too if the owner matches, even though the lock is no longer valid. The `DELETE` runs on the connection that is opened for this call. The advisory unlock runs afterwards on the same connection (see Known issues for why this does not reliably release the lock taken in `AcquireAsync`).

### Expiry and cleanup

Expiry is evaluated against `now()` on the database server (or against the model's `IsExpired` for single-row reads). Nothing runs cleanup automatically. To remove stale rows, call `DeleteExpiredLockAsync` periodically, or use `DeleteLockIfExpirationMatchesAsync` to remove one row only if its `expires_at` still equals the value you read. That compare-and-delete guard protects against removing a lock that was renewed after you read it.

`DeleteLockIfExpirationMatchesAsync` binds `expectedExpiresAt` as a `DateTime` parameter. Pass the same value that was read from the row, with the same `Kind` and precision. It throws `ArgumentException` if `key` is null or empty.

### Fencing tokens

`ValidateFencingTokenAsync` reads the lock and compares `FencingToken.SequenceNumber` with the supplied token, cast to `long`. There is no dedicated fencing column; the sequence number lives inside `lock_data`. A token above `long.MaxValue` will not match.

### Concurrency and transactions

- Each statement runs on its own connection in autocommit mode, except that `AcquireAsync` and `ReleaseAsync` issue two statements on one connection. There are no explicit transactions.
- The primary-key `INSERT ... ON CONFLICT DO NOTHING` is the atomic acquire step for ownership. Read-then-write sequences (`RenewAsync`, `ValidateFencingTokenAsync`, `GetByOwnerAsync`) are not atomic.
- Advisory locks are session-level. They are tied to the physical pooled connection, not to the `Lock` or to the caller.
- `DisposeAsync` does nothing; it awaits `Task.CompletedTask`. The repository holds no connection, but the `SemaphoreSlim` is not disposed.

### Errors

All methods catch `Exception`, log at Error level with the lock key where applicable, and rethrow the original exception. No method converts PostgreSQL errors into domain exceptions. Callers should expect `Npgsql.NpgsqlException` (and its subclass `PostgresException`), `System.Text.Json.JsonException` (if `lock_data` is corrupt), and `OperationCanceledException` when a token is cancelled.

### Extension methods

`PostgresLockRepositoryExtensions` (`src/Backends/PostgreSQL/PostgresLockRepositoryExtensions.cs`) adds retry and reporting helpers on top of the repository:

- `AcquireWithRetryAsync` and `ReleaseWithRetryAsync` retry the base call. `AcquireWithRetryAsync` throws `ArgumentOutOfRangeException` if `maxRetries <= 0`; the default delay is 100 ms.
- `GetActiveLockCountAsync`, `HasActiveLocksAsync`, `GetTotalLockCountAsync`, `GetExpiredLockCountAsync`, `GetLocksExpiringSoonAsync`, `GetOldestActiveLockAsync`, `GetNewestActiveLockAsync`, `GetAverageLockDurationAsync`, `GetAllLocksByOwnerAsync`, and the other helpers in the same file query the table directly with their own `NpgsqlConnection`.

Several helpers read the private `_connectionString` and `_logger` fields of `PostgresLockRepository` through reflection (`GetConnectionString`, `GetLogger`). Renaming those fields breaks the extensions at runtime, not at compile time.

## Operational notes

- Keep the database clock and session time zone consistent with the application (see Timestamp handling).
- A crashed holder leaves its row in place until `expires_at` passes and cleanup runs. Its advisory lock may also remain held on a pooled connection until that connection is closed or reset (see Known issues). Clear the table deliberately with `ClearAllAsync` only when no owner is active.
- `ClearAllAsync` and `DeleteExpiredLockAsync` remove rows only. They do not release advisory locks.
- `docker-init-postgres.sql` in the repository root creates a different table (`locks`, with `key` and `created_at` columns) and does not match the schema above. Do not use it to initialize the database for this backend; the repository creates `distributed_locks` itself.

## Known issues

These are defects in the current code, documented here so that callers do not depend on the behavior described above as intended.

1. **Advisory key is not stable across processes.** `GetAdvisoryLockKey` returns `lockKey.GetHashCode()`. In .NET Core and later, `string.GetHashCode()` is randomized per process. Two processes compute different advisory keys for the same lock name, so the advisory lock does not serialize them. Exclusivity still holds because of the primary key, but the advisory layer gives inconsistent blocking (same process: waits; different processes: no waiting).
2. **Advisory locks are not released on the acquiring session.** On a successful acquire, the advisory lock is kept on the connection, which is then returned to the Npgsql pool still holding it. `ReleaseAsync` runs its unlock on whichever pooled connection it receives. Unless that is the same physical connection, `pg_advisory_unlock` returns `false` and the lock stays held. Subsequent same-process acquires for that key can block in `pg_advisory_lock` with no timeout.
3. **Timestamp kind and time zone are not normalized** (see Timestamp handling). This is a deployment risk, not confirmed here.
4. **`AcquireAsync` does not return `false` quickly under same-process contention.** It blocks on the advisory lock first (see Acquire).

Fixing these requires a deliberate change to the code (for example, a stable key derived from a hash such as SHA-256 or `hashtextextended`, and transaction-scoped locks via `pg_try_advisory_xact_lock` or `pg_advisory_xact_lock`). Those changes are out of scope for this document.

## Related

- `docs/RedisLockRepository.md` and `docs/SqliteLockRepository.md` for the other backends.
- `docs/PostgresLockRepositoryExtensions.md` for the extension method reference.
- `docs/sqlite-backend.md` for the SQLite backend guide, which uses the same structure.
- `docs/FencingToken.md` for the fencing token model.
