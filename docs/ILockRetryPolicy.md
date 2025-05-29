# ILockRetryPolicy

Defines a retry policy for acquiring distributed locks, allowing customizable backoff strategies with jitter to reduce contention in distributed systems.

## API

### `MaxRetries`
- **Purpose**: Gets the maximum number of retry attempts before giving up.
- **Type**: `int`
- **Remarks**: Must be non-negative. A value of `0` indicates no retries (single attempt only).

### `InitialDelay`
- **Purpose**: Gets the initial delay before the first retry attempt.
- **Type**: `TimeSpan`
- **Remarks**: Must be non-negative. Represents the starting backoff duration.

### `MaxDelay`
- **Purpose**: Gets the maximum delay allowed between retry attempts.
- **Type**: `TimeSpan`
- **Remarks**: Must be non-negative and greater than or equal to `InitialDelay`. Caps the exponential backoff growth.

### `JitterFactor`
- **Purpose**: Gets the jitter factor (0.0 to 1.0) applied to each delay to randomize retry timing.
- **Type**: `double`
- **Remarks**: Must be in the range `[0.0, 1.0]`. A value of `0.0` disables jitter.

### `DefaultLockRetryPolicy`
- **Purpose**: Provides a default instance of `ILockRetryPolicy` with sensible defaults.
- **Type**: `ILockRetryPolicy`
- **Remarks**: Default values are `MaxRetries = 5`, `InitialDelay = TimeSpan.FromMilliseconds(100)`, `MaxDelay = TimeSpan.FromSeconds(10)`, and `JitterFactor = 0.2`.

### `GetDelay(int attempt)`
- **Purpose**: Computes the delay for the given retry attempt using exponential backoff with jitter.
- **Parameters**:
  - `attempt`: The current retry attempt number (0-based).
- **Return Value**: `TimeSpan` representing the computed delay.
- **Throws**: `ArgumentOutOfRangeException` if `attempt` is negative.
- **Remarks**: The delay is calculated as `Min(InitialDelay * 2^attempt * (1 ± JitterFactor), MaxDelay)`. The jitter is randomly applied within the specified factor.

## Usage

### Example 1: Custom Policy with High Retries
