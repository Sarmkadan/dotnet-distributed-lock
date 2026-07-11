# LockExtensions

Extension methods for working with distributed locks in .NET, providing safe access to lock state and renewal operations.

## API

### `IsActive`

Determines whether the current lock is active (held by the current process).

- **Return value**: `true` if the lock is active; otherwise, `false`.
- **Exceptions**: Throws `InvalidOperationException` if the lock is not available or has been released.

### `IsAvailable`

Determines whether the lock is available for acquisition.

- **Return value**: `true` if the lock is available; otherwise, `false`.
- **Exceptions**: Throws `InvalidOperationException` if the lock state cannot be determined.

### `GetRemainingTime`

Gets the remaining time until the lock expires.

- **Return value**: A `TimeSpan` representing the remaining time. Returns `TimeSpan.Zero` if the lock has no expiration or has already expired.
- **Exceptions**: Throws `InvalidOperationException` if the lock is not available or has been released.

### `SafeRenew`

Attempts to renew the lock if it is still active and within the renewal window.

- **Return value**: `true` if the renewal was successful; otherwise, `false`.
- **Exceptions**: Throws `InvalidOperationException` if the lock is not available or has been released.

## Usage

### Basic lock state check
