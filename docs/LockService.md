# LockService

A service for managing distributed locks in .NET applications, enabling safe coordination across multiple processes or machines by acquiring, renewing, and releasing locks with asynchronous operations.

## API

### `LockService`

The primary service class for managing distributed locks.

### `TryAcquireAsync(string resource, string owner, TimeSpan? expiry = null, CancellationToken cancellationToken = default)`

Attempts to acquire a lock on the specified resource asynchronously.

- **Parameters**
  - `resource`: The identifier of the resource to lock.
  - `owner`: A unique identifier for the entity attempting to acquire the lock.
  - `expiry`: Optional duration after which the lock expires if not renewed. Defaults to a system-defined value.
  - `cancellationToken`: A token to monitor for cancellation requests.
- **Return Value**: A tuple containing:
  - `Success`: `true` if the lock was acquired; otherwise, `false`.
  - `Lock`: The acquired lock if successful; otherwise, `null`.
  - `ErrorMessage`: An error message if the operation failed; otherwise, `null`.
- **Exceptions**: Throws `ArgumentException` if `resource` or `owner` is null or whitespace.

### `AcquireAsync(string resource, string owner, TimeSpan? expiry = null, CancellationToken cancellationToken = default)`

Acquires a lock on the specified resource asynchronously, blocking until the lock is obtained or the operation is cancelled.

- **Parameters**
  - `resource`: The identifier of the resource to lock.
  - `owner`: A unique identifier for the entity attempting to acquire the lock.
  - `expiry`: Optional duration after which the lock expires if not renewed. Defaults to a system-defined value.
  - `cancellationToken`: A token to monitor for cancellation requests.
- **Return Value**: The acquired `Lock`.
- **Exceptions**:
  - Throws `ArgumentException` if `resource` or `owner` is null or whitespace.
  - Throws `OperationCanceledException` if the operation is cancelled via `cancellationToken`.
  - Throws `InvalidOperationException` if the lock cannot be acquired due to contention or system errors.

### `RenewAsync(string resource, string owner, CancellationToken cancellationToken = default)`

Renews an existing lock asynchronously without changing its owner.

- **Parameters**
  - `resource`: The identifier of the locked resource.
  - `owner`: The current owner of the lock.
  - `cancellationToken`: A token to monitor for cancellation requests.
- **Return Value**: `true` if the lock was renewed; otherwise, `false`.
- **Exceptions**:
  - Throws `ArgumentException` if `resource` or `owner` is null or whitespace.
  - Throws `InvalidOperationException` if the lock does not exist or is not owned by the specified `owner`.

### `RenewLockAsync(Lock @lock, CancellationToken cancellationToken = default)`

Renews an existing lock asynchronously using a `Lock` instance.

- **Parameters**
  - `@lock`: The `Lock` instance to renew.
  - `cancellationToken`: A token to monitor for cancellation requests.
- **Return Value**: The renewed `Lock`.
- **Exceptions**:
  - Throws `ArgumentNullException` if `@lock` is `null`.
  - Throws `InvalidOperationException` if the lock does not exist or is not owned by the current context.

### `ReleaseAsync(string resource, string owner, CancellationToken cancellationToken = default)`

Releases an existing lock asynchronously.

- **Parameters**
  - `resource`: The identifier of the locked resource.
  - `owner`: The current owner of the lock.
  - `cancellationToken`: A token to monitor for cancellation requests.
- **Return Value**: `true` if the lock was released; otherwise, `false`.
- **Exceptions**:
  - Throws `ArgumentException` if `resource` or `owner` is null or whitespace.
  - Throws `InvalidOperationException` if the lock does not exist or is not owned by the specified `owner`.

### `GetLockAsync(string resource, CancellationToken cancellationToken = default)`

Retrieves the current lock for a resource asynchronously.

- **Parameters**
  - `resource`: The identifier of the resource to query.
  - `cancellationToken`: A token to monitor for cancellation requests.
- **Return Value**: The `Lock` instance if it exists; otherwise, `null`.
- **Exceptions**: Throws `ArgumentException` if `resource` is null or whitespace.

### `IsLockedAsync(string resource, CancellationToken cancellationToken = default)`

Checks whether a resource is currently locked asynchronously.

- **Parameters**
  - `resource`: The identifier of the resource to check.
  - `cancellationToken`: A token to monitor for cancellation requests.
- **Return Value**: `true` if the resource is locked; otherwise, `false`.
- **Exceptions**: Throws `ArgumentException` if `resource` is null or whitespace.

### `GetAllActiveLockAsync(CancellationToken cancellationToken = default)`

Retrieves all currently active locks asynchronously.

- **Parameters**
  - `cancellationToken`: A token to monitor for cancellation requests.
- **Return Value**: An enumerable of all active `Lock` instances.

### `GetMetrics()`

Retrieves current metrics about the lock service.

- **Return Value**: A `LockMetrics` instance containing operational statistics such as lock count, acquisition rate, and failure counts.

## Usage

### Basic Lock Acquisition and Release
