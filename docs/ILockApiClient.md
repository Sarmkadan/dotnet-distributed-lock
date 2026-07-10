# ILockApiClient

The `ILockApiClient` interface defines a client for interacting with a distributed lock service over HTTP. It provides methods to acquire, release, renew, and check the status of distributed locks, enabling coordination across multiple application instances.

## API

### `public HttpLockApiClient`
A concrete implementation of `ILockApiClient` that communicates with a lock service via HTTP. The client is initialized with required configuration for lock operations.

### `public async Task<LockResponse?> AcquireLockAsync`
Attempts to acquire a distributed lock with the specified name and duration.

- **Parameters**: None.
- **Return value**: A `LockResponse` containing `Success`, `LockId`, `FencingToken`, `AcquiredAt`, and `ExpiresAt` if the lock was acquired; otherwise `null`.
- **Exceptions**: Throws if the HTTP request fails or the response cannot be deserialized.

### `public async Task ReleaseLockAsync`
Releases a previously acquired distributed lock.

- **Parameters**: None.
- **Return value**: A `Task` representing the asynchronous operation.
- **Exceptions**: Throws if the HTTP request fails or the lock is not held.

### `public async Task<RenewLockResponse?> RenewLockAsync`
Extends the duration of an acquired lock.

- **Parameters**: None.
- **Return value**: A `RenewLockResponse` containing `Success` and `ExpiresAt` if the renewal succeeded; otherwise `null`.
- **Exceptions**: Throws if the HTTP request fails or the lock is not held.

### `public async Task<LockStatusResponse?> GetLockStatusAsync`
Checks the status of a lock, including whether it is active and remaining duration.

- **Parameters**: None.
- **Return value**: A `LockStatusResponse` containing `Name`, `IsActive`, `RemainingSeconds`, and `LockId` if the lock exists; otherwise `null`.
- **Exceptions**: Throws if the HTTP request fails or the lock does not exist.

### `public required string LockName`
Gets or sets the name of the lock to acquire or manage. Must be provided before calling `AcquireLockAsync`.

### `public required int DurationSeconds`
Gets or sets the duration in seconds for which the lock should be held. Must be provided before calling `AcquireLockAsync`.

### `public bool AutoRenew`
Gets or sets whether the lock should automatically renew its duration before expiration. Defaults to `false`.

### `public int? RenewalIntervalSeconds`
Gets or sets the interval in seconds at which the lock should be renewed if `AutoRenew` is `true`. Must be greater than zero if `AutoRenew` is enabled.

## Usage

### Example 1: Basic Lock Acquisition and Release
