# Redis Backend (`RedisLockRepository`)

`RedisLockRepository` (`src/Backends/Redis/RedisLockRepository.cs`) implements `ILockRepository` on top of StackExchange.Redis. Each lock is one Redis string key holding the JSON-serialized `Lock`, with a Redis TTL equal to the lock's remaining lifetime. It is intended for multi-host deployments that already run Redis.

This document describes the current code. It does not propose changes. For the per-method API reference, see `docs/RedisLockRepository.md`.

## When to use it

- Several hosts or processes must coordinate through one shared Redis instance.
- Redis is already part of the infrastructure and its availability is acceptable for locking.

Redis locks are only as strong as the Redis deployment. Failover to a replica can lose keys that were written but not yet replicated. This repository does not implement Redlock or any multi-node quorum scheme.

## Configuration

### Registration

The backend is selected through `BackendType.Redis`:

```csharp
services.AddDistributedLock(BackendType.Redis, connectionString);
```

Internally `AddRedisRepository` registers `RedisLockRepository` as a singleton `ILockRepository`. The logger is resolved as `ILogger<RedisLockRepository>` from the container.

`RedisLockProvider` is an alternative entry point. It constructs its own `RedisLockRepository` from `DistributedLockOptions.ConnectionString`, wraps it in `LockService` with a `DefaultLockRetryPolicy` built from the `RetryPolicy*` options, and exposes `AcquireAsync`, `TryAcquireAsync`, `ReleaseAsync`, `RenewAsync` and `IsLockedAsync`. Use one or the other; both create a separate multiplexer.

### Connection string

The connection string is passed unchanged to `ConnectionMultiplexer.Connect(...)`, so it uses the StackExchange.Redis configuration format. Typical values:

```
localhost:6379
redis.internal:6379,password=...,ssl=true,abortConnect=false
```

Constructor rules:

- `logger` must not be null (`ArgumentNullException`).
- `connectionString` must not be null, empty, or whitespace (`ArgumentException`).
- The constructor calls `ConnectionMultiplexer.Connect` synchronously. It blocks until the initial connection attempt completes or fails.
- The constructor logs the connection string at Information level. Do not put a password into this string if logs are shared. Prefer a secret source that is not logged, or redact the log line.

Options such as `abortConnect`, `connectTimeout`, `syncTimeout` and `ssl` are not set by the repository. They are whatever the connection string says, or the StackExchange.Redis defaults. Note that the default `abortConnect=true` makes the constructor throw if Redis is unreachable at startup.

The repository does not select a database index. `GetDatabase()` is called without arguments, so the index is the one in the connection string (`,defaultDatabase=N`) or the server default (0).

## Key format

| Item | Value |
|---|---|
| Prefix | `distributed_lock:` (`Constants.LockConstants.LockKeyPrefix`) |
| Redis key | `distributed_lock:` + `lockKey` |
| Redis value | `JsonSerializer.Serialize(Lock)` (`System.Text.Json`) |
| Redis TTL | `Lock.ExpiresAt - DateTime.UtcNow` at write time |

Example: lock key `orders:42` is stored under `distributed_lock:orders:42`.

Notes:

- The prefix is fixed in code. It cannot be changed through configuration. Two applications that share a Redis database and use the same lock keys will contend for the same locks.
- The stored value is the full `Lock` object, including `OwnerId`, `Status`, `ExpiresAt`, `RenewalCount`, and `FencingToken`. Reads depend on this JSON. A corrupt value is logged and treated as absent (see Errors).
- Lock keys are not escaped or validated in this repository. The bulk methods pass the prefix as a `SCAN` glob pattern, so the pattern matches exactly the keys beginning with the prefix; the lock key part is never used as a pattern.

## Behavior

### Acquire

`AcquireAsync` is a two-step write:

1. `SET key value PX <ttl> NX` with the value in `Pending` state. This is the only atomic acquire step.
2. If step 1 succeeded, `Status` is set to `Acquired`, the value is re-serialized, and `SET key value PX <ttl> XX` overwrites it.

Consequences:

- If the second `SET` finds the key gone (for example, TTL expired between the two calls), it returns `false` silently. The method still returns `true`, and the caller holds a lock that no longer exists in Redis.
- Between step 1 and step 2, another reader can observe `Status = Pending`. Callers should not treat `Status` as authoritative while acquisition is in progress.
- `AcquireAsync` does not take over expired locks. An expired key is removed by Redis TTL, so in normal operation it does not block acquisition.
- The method returns `false` when the key already exists. `Status` on the in-memory object is only changed on success.

### Get

`GetByKeyAsync` runs `GET`. If the value exists and deserializes, and `!IsExpired`, it returns the lock. If the value is expired, it calls `KeyDeleteAsync` on that key and returns `null`.

That delete is not conditional. If the owner renewed the lock between the `GET` and the `DEL`, the renewed lock is deleted. In practice the window is small, and Redis TTL normally removes the key first, but the race exists.

`GetByKeyAndOwnerAsync` calls `GetByKeyAsync` and returns the lock only if `OwnerId` matches.

### Update and renew

`UpdateAsync` issues a plain `SET key value PX <ttl>` with no `NX` or `XX` condition. Consequences:

- It always returns `true` when no exception occurs, even if the key did not exist.
- If the lock has expired and its key is gone, `UpdateAsync` recreates it. Callers that need "update only if still held" must check ownership and expiry first.
- It does not check the owner.

`RenewAsync` is read-modify-write:

1. `GetByKeyAndOwnerAsync` (returns `null` if the owner differs or the lock is expired).
2. `Lock.Renew(newDuration)` on the in-memory object (`RenewedAt`, `RenewalCount`, `ExpiresAt` change).
3. `UpdateAsync`.

The read and the write are separate commands with no `WATCH`, transaction, or Lua script. Another client can release or re-acquire the lock between steps 1 and 3, and the unconditional `SET` in step 3 will then overwrite that state. Treat renewal under contention as best-effort.

### Release

`ReleaseAsync` performs `GET`, checks `OwnerId`, then `DEL`. The `DEL` is not conditional on the value that was checked. If the lock expires and another owner acquires the same key between the `GET` and the `DEL`, the new owner's lock is deleted. Redis has no atomic compare-and-delete in this code path.

A non-owner call returns `false` and leaves the key in place.

### Expiry and cleanup

Redis TTL is the primary expiry mechanism. Expired keys disappear without any call from this repository. The `IsExpired` checks in the read paths are a second guard for clock skew and for values written with a TTL that has already passed.

`DeleteExpiredLockAsync` and `GetExpiredLocksAsync` use `SCAN` over `distributed_lock:*`. Because Redis TTL removes expired keys, these methods usually find nothing to delete. They are useful mainly for keys whose TTL was not applied correctly.

`DeleteLockIfExpirationMatchesAsync` is a true compare-and-delete:

1. `GET` the key and check that `ExpiresAt` equals the expected value.
2. Start a transaction with `Condition.StringEqual(key, valueRead)` and `DEL key`.
3. Execute. The delete is applied only if the value is still byte-for-byte identical.

If the holder renewed the lock in between, the serialized value changes, the condition fails, and nothing is deleted. The return value is the transaction result. Note that the `GET` in step 1 and the transaction in step 2 are separate round trips; the condition is what makes the delete safe.

### Fencing tokens

`ValidateFencingTokenAsync` reads the lock via `GetByKeyAsync` and compares `FencingToken.SequenceNumber` with the supplied value cast to `long`. The token lives inside the JSON value, so there is no separate Redis key for it. A token above `long.MaxValue` will not match.

### Bulk and enumeration methods

`GetAllActiveLockAsync`, `GetByOwnerAsync`, `GetExpiredLocksAsync`, `DeleteExpiredLockAsync` and `ClearAllAsync` all:

- take the first endpoint from `_redis.GetEndPoints()` and call `server.Keys(pattern: "distributed_lock:*")`;
- issue one `GET` (or `DEL`) per key, sequentially, with no pipelining.

Consequences:

- Cost grows linearly with the number of lock keys. Each call is O(N) round trips.
- Only one endpoint is scanned. In a Redis Cluster deployment, keys on other shards are not visible to these methods.
- The `SCAN`-based iteration is not a consistent snapshot. Keys created or deleted during the scan may or may not be seen.
- `ClearAllAsync` returns the number of keys it attempted to delete, not the number the server actually deleted. Keys that expire during the scan are still counted.
- `GetByOwnerAsync` loads all active locks and filters in memory.

### Existence

`ExistsAsync` calls `GetByKeyAsync` and returns `true` if the lock is present and not expired. It does not check the TTL directly.

### Concurrency summary

| Operation | Atomic in Redis? | Notes |
|---|---|---|
| Acquire | Yes for the `NX` step | The follow-up `XX` write is a separate command. |
| Get | Single `GET` | Expired-key `DEL` is unconditional. |
| Update | Single `SET` | Unconditional; can recreate a missing key. |
| Renew | No | Read-modify-write. |
| Release | No | Check-then-`DEL`. |
| DeleteLockIfExpirationMatches | Yes for the delete | Guarded by `StringEqual` condition inside `MULTI/EXEC`. |
| Bulk methods | No | `SCAN` plus per-key commands, one endpoint only. |

## Validation helper

`RedisLockRepositoryValidation.Validate()` calls `ExistsAsync("validation_dummy_key")` without awaiting and inspects `IsFaulted` immediately. The call almost always has not completed at that point, so the check rarely reports anything. The method is therefore not a reliable health check. For connectivity checks, use `IDatabase.PingAsync()` directly or the health-monitoring workers.

## Disposal

`DisposeAsync` calls `CloseAsync()` and then `Dispose()` on the `ConnectionMultiplexer`. The repository owns the multiplexer, so disposing the repository closes the connection for every operation in progress. Do not share one repository across a shutdown boundary without accounting for that.

`RedisLockProvider` disposes its repository in `Dispose`/`DisposeAsync`.

## Errors

All public methods catch `Exception`, log at Error level with the lock key where applicable, and rethrow. Expected exceptions include:

- `RedisConnectionException`, `RedisTimeoutException` (StackExchange.Redis);
- `ArgumentException` from the constructor on a bad connection string;
- `System.Text.Json.JsonException` is caught inside `DeserializeLock`, logged, and turned into `null`. A corrupt value is therefore treated as "no lock" by readers, which can allow a second acquire on a key whose value is unreadable. The `NX` check still sees the key as existing, so acquisition is blocked until the TTL expires or the key is removed.

## Operational notes

- Set a Redis `maxmemory-policy` that does not evict lock keys (for example `noeviction` or a volatile policy that evicts only keys without TTL). Evicting a lock key silently releases the lock.
- Use a persistence setting appropriate to your tolerance for losing locks on restart (AOF with `everysec` or `always`, or none if you accept that restarts release all locks).
- Keep `ExpiresAt` in UTC. TTLs are computed from `DateTime.UtcNow`, so local-time values produce incorrect TTLs.
- Monitor for keys with no TTL under `distributed_lock:*`. Such keys are never expired by Redis.

## Related

- `docs/RedisLockRepository.md` for the per-method API reference.
- `docs/sqlite-backend.md` for the SQLite backend, which uses the same behavior sections.
- `docs/FencingToken.md` for the fencing token model.
- `docs/DEPLOYMENT.md` for deployment notes.
