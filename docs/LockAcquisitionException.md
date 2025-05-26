# LockAcquisitionException

The `LockAcquisitionException` is thrown when an attempt to acquire a distributed lock fails after the specified timeout and retry attempts. This exception provides details about the lock key, timeout duration, and number of retry attempts made before failing.

## API

### `public string LockKey`
Gets the key associated with the lock that could not be acquired.

- **Purpose**: Identifies the lock resource for which acquisition was attempted.
- **Return Value**: A non-null string representing the lock key.
- **Throws**: Never throws.

### `public TimeSpan Timeout`
Gets the timeout duration after which the lock acquisition attempt was aborted.

- **Purpose**: Indicates the maximum time allowed for acquiring the lock.
- **Return Value**: A `TimeSpan` representing the timeout duration.
- **Throws**: Never throws.

### `public int RetryCount`
Gets the number of retry attempts made before throwing this exception.

- **Purpose**: Indicates how many times the system attempted to acquire the lock before failing.
- **Return Value**: An integer representing the retry count.
- **Throws**: Never throws.

### `public LockAcquisitionException()`
Initializes a new instance of the `LockAcquisitionException` class with default values.

- **Purpose**: Constructs an exception with no specific details.
- **Parameters**: None.
- **Return Value**: A new `LockAcquisitionException` instance.
- **Throws**: Never throws.

### `public LockAcquisitionException(string message)`
Initializes a new instance of the `LockAcquisitionException` class with a specified error message.

- **Purpose**: Constructs an exception with a custom error message.
- **Parameters**:
  - `message` (string): A description of the error.
- **Return Value**: A new `LockAcquisitionException` instance.
- **Throws**: Never throws.

## Usage

### Example 1: Handling a Lock Acquisition Failure
