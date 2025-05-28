# LockExpiredException

Exception thrown when a distributed lock managed by `dotnet-distributed-lock` expires before being explicitly released.

## API

### `LockKey`
- **Purpose**: Gets the unique identifier of the lock that expired.
- **Type**: `string`
- **Remarks**: This value is never `null` and is set during construction.

### `ExpirationTime`
- **Purpose**: Gets the UTC date and time when the lock was scheduled to expire.
- **Type**: `DateTime`
- **Remarks**: This value reflects the absolute expiration time as determined by the lock manager.

### `LockExpiredException()`
- **Purpose**: Initializes a new instance of the `LockExpiredException` class with default values.
- **Remarks**: Sets `LockKey` to `null` and `ExpirationTime` to `DateTime.MinValue`.

### `LockExpiredException(string lockKey)`
- **Purpose**: Initializes a new instance of the `LockExpiredException` class with a specified lock key.
- **Parameters**:
  - `lockKey`: The unique identifier of the expired lock.
- **Remarks**: Sets `ExpirationTime` to `DateTime.MinValue`.

### `LockExpiredException(string lockKey, DateTime expirationTime)`
- **Purpose**: Initializes a new instance of the `LockExpiredException` class with a specified lock key and expiration time.
- **Parameters**:
  - `lockKey`: The unique identifier of the expired lock.
  - `expirationTime`: The UTC date and time when the lock was scheduled to expire.
- **Remarks**: Both parameters are required and must not be `null` or invalid.

## Usage

### Detecting lock expiration
