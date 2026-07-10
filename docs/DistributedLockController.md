# DistributedLockController

A controller for managing distributed locks in a .NET application, enabling coordination across multiple processes or nodes. It provides endpoints to acquire, release, renew, and inspect locks with fencing token support for safety in distributed systems.

## API

### Properties

#### `public DistributedLockController`
Initializes a new instance of the `DistributedLockController` with required configuration. The controller requires `LockName`, `DurationSeconds`, and `AutoRenew` to be set before use.

#### `public required string LockName`
Gets or sets the name of the lock to acquire or manage. Must be a non-empty string.

#### `public required int DurationSeconds`
Gets or sets the duration in seconds for which the lock should be held if acquired. Must be a positive integer.

#### `public bool AutoRenew`
Gets or sets whether the lock should be automatically renewed before expiration. Defaults to `false`.

#### `public int? RenewalIntervalSeconds`
Gets or sets the interval in seconds at which the lock should be renewed when `AutoRenew` is `true`. Must be a positive integer if set.

---

### Methods

#### `public async Task<ActionResult<LockAcquisitionResponse>> AcquireLock()`
Attempts to acquire the distributed lock.

- **Returns**: An `ActionResult<LockAcquisitionResponse>` containing a response with `Success`, `LockId`, `FencingToken`, `AcquiredAt`, and `ExpiresAt` if successful.
- **Throws**: May throw if the lock name or duration is invalid, or if the underlying lock store fails.

#### `public async Task<ActionResult<OperationResponse>> ReleaseLock()`
Releases the acquired distributed lock.

- **Returns**: An `ActionResult<OperationResponse>` containing a response with `Success`.
- **Throws**: May throw if the lock is not currently held or if the lock store fails.

#### `public async Task<ActionResult<LockRenewalResponse>> RenewLock()`
Renews the lock's expiration time.

- **Returns**: An `ActionResult<LockRenewalResponse>` containing a response with `Success`, `ExpiresAt`, and `RemainingSeconds`.
- **Throws**: May throw if the lock is not held or if the lock store fails.

#### `public async Task<ActionResult<LockStatusResponse>> GetLockStatus()`
Retrieves the current status of the lock.

- **Returns**: An `ActionResult<LockStatusResponse>` containing a response with `Name`, `IsActive`, and `ExpiresAt`.
- **Throws**: May throw if the lock store fails.

---

### Response Types

#### `public LockAcquisitionResponse`
- **Success** (`bool`): Indicates whether the lock was successfully acquired.
- **LockId** (`string`): A unique identifier for the acquired lock.
- **FencingToken** (`ulong`): A monotonically increasing token to prevent stale locks.
- **AcquiredAt** (`DateTime`): The timestamp when the lock was acquired.
- **ExpiresAt** (`DateTime`): The timestamp when the lock will expire.

#### `public OperationResponse`
- **Success** (`bool`): Indicates whether the operation completed successfully.

#### `public LockRenewalResponse`
- **Success** (`bool`): Indicates whether the lock was successfully renewed.
- **ExpiresAt** (`DateTime`): The new expiration timestamp of the lock.
- **RemainingSeconds** (`int`): The number of seconds remaining until the lock expires.

#### `public LockStatusResponse`
- **Name** (`string`): The name of the lock.
- **IsActive** (`bool`): Indicates whether the lock is currently active.
- **ExpiresAt** (`DateTime`): The expiration timestamp of the lock.

---

## Usage

### Example 1: Acquire and Release a Lock
