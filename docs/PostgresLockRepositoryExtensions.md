# PostgresLockRepositoryExtensions

Extension methods for working with PostgreSQL-backed distributed locks, providing resilient retry logic and convenience queries over the underlying lock repository.

## API

### `AcquireWithRetryAsync`

Attempts to acquire a distributed lock with automatic retry logic. The method will retry according to the configured retry policy if the lock acquisition fails due to transient errors.

- **Parameters**
  - `repository`: The `IPostgresLockRepository` instance to use for lock operations.
  - `resource`: The resource identifier to lock.
  - `owner`: The identifier of the entity attempting to acquire the lock.
  - `expiration`: The duration for which the lock should be held.
  - `retryPolicy`: Optional retry policy to override default behavior.
  - `cancellationToken`: Optional cancellation token.
- **Return value**: `Task<bool>` indicating whether the lock was successfully acquired.
- **Exceptions**: Throws if the underlying repository throws a non-transient exception.

### `ReleaseWithRetryAsync`

Attempts to release a distributed lock with automatic retry logic. The method will retry according to the configured retry policy if the release operation fails due to transient errors.

- **Parameters**
  - `repository`: The `IPostgresLockRepository` instance to use for lock operations.
  - `resource`: The resource identifier of the lock to release.
  - `owner`: The identifier of the entity that currently holds the lock.
  - `retryPolicy`: Optional retry policy to override default behavior.
  - `cancellationToken`: Optional cancellation token.
- **Return value**: `Task<bool>` indicating whether the lock was successfully released.
- **Exceptions**: Throws if the underlying repository throws a non-transient exception.

### `GetActiveLockCountAsync`

Returns the total number of currently active (non-expired) locks in the repository.

- **Parameters**
  - `repository`: The `IPostgresLockRepository` instance to query.
  - `cancellationToken`: Optional cancellation token.
- **Return value**: `Task<int>` representing the count of active locks.
- **Exceptions**: None.

### `HasActiveLocksAsync`

Determines whether there are any currently active (non-expired) locks in the repository.

- **Parameters**
  - `repository`: The `IPostgresLockRepository` instance to query.
  - `cancellationToken`: Optional cancellation token.
- **Return value**: `Task<bool>` indicating whether at least one active lock exists.
- **Exceptions**: None.

### `GetAllLocksByOwnerAsync`

Retrieves all locks currently held by a specific owner.

- **Parameters**
  - `repository`: The `IPostgresLockRepository` instance to query.
  - `owner`: The identifier of the lock owner.
  - `cancellationToken`: Optional cancellation token.
- **Return value**: `Task<IEnumerable<Lock>>` containing all locks owned by the specified owner.
- **Exceptions**: None.

### `GetTotalLockCountAsync`

Returns the total number of locks (both active and expired) in the repository.

- **Parameters**
  - `repository`: The `IPostgresLockRepository` instance to query.
  - `cancellationToken`: Optional cancellation token.
- **Return value**: `Task<int>` representing the total lock count.
- **Exceptions**: None.

### `GetExpiredLockCountAsync`

Returns the number of locks that have expired (past their expiration time) in the repository.

- **Parameters**
  - `repository`: The `IPostgresLockRepository` instance to query.
  - `cancellationToken`: Optional cancellation token.
- **Return value**: `Task<int>` representing the count of expired locks.
- **Exceptions**: None.

### `GetLocksExpiringSoonAsync`

Retrieves all locks that are approaching their expiration time within a default or configurable threshold.

- **Parameters**
  - `repository`: The `IPostgresLockRepository` instance to query.
  - `threshold`: Optional duration indicating how soon before expiration a lock should be considered "expiring soon". Defaults to a reasonable value.
  - `cancellationToken`: Optional cancellation token.
- **Return value**: `Task<IEnumerable<Lock>>` containing locks that will expire within the threshold.
- **Exceptions**: None.

### `GetOldestActiveLockAsync`

Retrieves the oldest currently active (non-expired) lock in the repository.

- **Parameters**
  - `repository`: The `IPostgresLockRepository` instance to query.
  - `cancellationToken`: Optional cancellation token.
- **Return value**: `Task<Lock?>` representing the oldest active lock, or `null` if no active locks exist.
- **Exceptions**: None.

### `GetNewestActiveLockAsync`

Retrieves the newest currently active (non-expired) lock in the repository.

- **Parameters**
  - `repository`: The `IPostgresLockRepository` instance to query.
  - `cancellationToken`: Optional cancellation token.
- **Return value**: `Task<Lock?>` representing the newest active lock, or `null` if no active locks exist.
- **Exceptions**: None.

### `GetAverageLockDurationAsync`

Calculates the average duration for which locks are held in the repository.

- **Parameters**
  - `repository`: The `IPostgresLockRepository` instance to query.
  - `cancellationToken`: Optional cancellation token.
- **Return value**: `Task<double>` representing the average lock duration in seconds, or `0` if no locks exist.
- **Exceptions**: None.

## Usage

### Example 1: Acquiring and releasing a lock with retry
