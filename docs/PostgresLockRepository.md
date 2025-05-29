# PostgresLockRepository

A repository implementation for managing distributed locks in PostgreSQL. It provides asynchronous operations for acquiring, renewing, releasing, and querying locks, with support for ownership validation, expiration handling, and fencing token checks.

## API

### `PostgresLockRepository`

Initializes a new instance of the `PostgresLockRepository` with the specified PostgreSQL connection and optional schema name.

**Parameters**
- `connection`: The active `NpgsqlConnection` to use for database operations.
- `schema`: Optional schema name where lock tables reside. Defaults to `public`.

**Remarks**
- The connection is not disposed by this class; ownership remains with the caller.
- Throws `ArgumentNullException` if `connection` is `null`.

---

### `async Task<bool> AcquireAsync`

Attempts to acquire a lock for the given key with an optional owner identifier and expiration time.

**Parameters**
- `key`: The unique identifier for the lock.
- `owner`: Optional owner identifier (e.g., process or thread ID). If `null`, defaults to an empty string.
- `expiration`: The time at which the lock expires if not renewed.
- `cancellationToken`: Optional token to monitor for cancellation requests.

**Return Value**
- `true` if the lock was successfully acquired; `false` if the lock already exists.

**Remarks**
- Throws `ArgumentNullException` if `key` is `null`.
- Throws `PostgresException` or `NpgsqlException` on database errors.

---

### `async Task<Lock?> GetByKeyAsync`

Retrieves the lock associated with the specified key, if it exists.

**Parameters**
- `key`: The unique identifier of the lock to retrieve.
- `cancellationToken`: Optional token to monitor for cancellation requests.

**Return Value**
- The `Lock` instance if found; otherwise `null`.

**Remarks**
- Throws `ArgumentNullException` if `key` is `null`.
- Throws `PostgresException` or `NpgsqlException` on database errors.

---
### `async Task<Lock?> GetByKeyAndOwnerAsync`

Retrieves the lock associated with the specified key and owner, if it exists.

**Parameters**
- `key`: The unique identifier of the lock to retrieve.
- `owner`: The owner identifier to match.
- `cancellationToken`: Optional token to monitor for cancellation requests.

**Return Value**
- The `Lock` instance if found; otherwise `null`.

**Remarks**
- Throws `ArgumentNullException` if `key` or `owner` is `null`.
- Throws `PostgresException` or `NpgsqlException` on database errors.

---
### `async Task<bool> UpdateAsync`

Updates an existing lock with new expiration time and optional owner.

**Parameters**
- `key`: The unique identifier of the lock to update.
- `owner`: The new owner identifier. If `null`, retains the current owner.
- `expiration`: The new expiration time for the lock.
- `cancellationToken`: Optional token to monitor for cancellation requests.

**Return Value**
- `true` if the lock was updated; `false` if the lock did not exist.

**Remarks**
- Throws `ArgumentNullException` if `key` is `null`.
- Throws `PostgresException` or `NpgsqlException` on database errors.

---
### `async Task<bool> RenewAsync`

Renews an existing lock by extending its expiration time.

**Parameters**
- `key`: The unique identifier of the lock to renew.
- `expiration`: The new expiration time.
- `cancellationToken`: Optional token to monitor for cancellation requests.

**Return Value**
- `true` if the lock was renewed; `false` if the lock did not exist.

**Remarks**
- Throws `ArgumentNullException` if `key` is `null`.
- Throws `PostgresException` or `NpgsqlException` on database errors.

---
### `async Task<bool> ReleaseAsync`

Releases (removes) an existing lock.

**Parameters**
- `key`: The unique identifier of the lock to release.
- `cancellationToken`: Optional token to monitor for cancellation requests.

**Return Value**
- `true` if the lock was released; `false` if the lock did not exist.

**Remarks**
- Throws `ArgumentNullException` if `key` is `null`.
- Throws `PostgresException` or `NpgsqlException` on database errors.

---
### `async Task<bool> ExistsAsync`

Checks whether a lock with the specified key currently exists.

**Parameters**
- `key`: The unique identifier of the lock to check.
- `cancellationToken`: Optional token to monitor for cancellation requests.

**Return Value**
- `true` if the lock exists; otherwise `false`.

**Remarks**
- Throws `ArgumentNullException` if `key` is `null`.
- Throws `PostgresException` or `NpgsqlException` on database errors.

---
### `async Task<IEnumerable<Lock>> GetAllActiveLockAsync`

Retrieves all locks that have not yet expired.

**Parameters**
- `cancellationToken`: Optional token to monitor for cancellation requests.

**Return Value**
- An enumerable of `Lock` instances representing active locks.

**Remarks**
- Throws `PostgresException` or `NpgsqlException` on database errors.

---
### `async Task<IEnumerable<Lock>> GetByOwnerAsync`

Retrieves all locks currently owned by the specified owner.

**Parameters**
- `owner`: The owner identifier to filter by.
- `cancellationToken`: Optional token to monitor for cancellation requests.

**Return Value**
- An enumerable of `Lock` instances owned by the specified owner.

**Remarks**
- Throws `ArgumentNullException` if `owner` is `null`.
- Throws `PostgresException` or `NpgsqlException` on database errors.

---
### `async Task<int> DeleteExpiredLockAsync`

Removes all locks that have expired as of the current time.

**Parameters**
- `cancellationToken`: Optional token to monitor for cancellation requests.

**Return Value**
- The number of locks deleted.

**Remarks**
- Throws `PostgresException` or `NpgsqlException` on database errors.

---
### `async Task<int> ClearAllAsync`

Removes all locks from the repository, regardless of expiration or ownership.

**Parameters**
- `cancellationToken`: Optional token to monitor for cancellation requests.

**Return Value**
- The number of locks deleted.

**Remarks**
- Throws `PostgresException` or `NpgsqlException` on database errors.

---
### `async Task<bool> ValidateFencingTokenAsync`

Validates that the provided fencing token matches the current lock's fencing token, if the lock exists.

**Parameters**
- `key`: The unique identifier of the lock to validate.
- `fencingToken`: The expected fencing token value.
- `cancellationToken`: Optional token to monitor for cancellation requests.

**Return Value**
- `true` if the lock exists and its fencing token matches; otherwise `false`.

**Remarks**
- Throws `ArgumentNullException` if `key` is `null`.
- Throws `PostgresException` or `NpgsqlException` on database errors.

---
### `ValueTask DisposeAsync`

Asynchronously releases any resources held by the repository.

**Remarks**
- This method is idempotent. Subsequent calls have no effect.
- Does not close or dispose the underlying `NpgsqlConnection`.

## Usage

### Example 1: Acquiring and Releasing a Lock
