# AdvancedConcurrencyTestsExtensions

Provides extension methods for testing and verifying the behavior of distributed lock implementations under concurrent access scenarios. These methods facilitate assertions about lock acquisition, state verification, and performance characteristics in high-contention environments.

## API

### `GetLockStateAsync`

Retrieves the current state of a lock by its identifier.

- **Parameters**:
  - `ILockRepository repository`: The repository to query for the lock state.
  - `string lockId`: The unique identifier of the lock to inspect.
- **Return value**: A `Task<Lock?>` resolving to the `Lock` instance if it exists, or `null` if no such lock is held.
- **Exceptions**: Throws `ArgumentNullException` if `repository` or `lockId` is `null`.

### `CountActiveLocksAsync`

Counts the number of currently active (held) locks in the repository.

- **Parameters**:
  - `ILockRepository repository`: The repository to query for active locks.
- **Return value**: A `Task<int>` representing the number of active locks.
- **Exceptions**: Throws `ArgumentNullException` if `repository` is `null`.

### `MeasureAcquisitionTimeAsync`

Measures the time taken to acquire a lock, including retries if applicable, and returns the result along with the elapsed duration.

- **Parameters**:
  - `ILockProvider provider`: The provider used to attempt lock acquisition.
  - `string lockId`: The identifier of the lock to acquire.
  - `TimeSpan timeout`: Maximum time to spend attempting acquisition.
  - `TimeSpan? retryDelay`: Optional delay between retry attempts. If `null`, no retries are performed.
- **Return value**: A `Task<(bool Acquired, Lock? Lock, TimeSpan ElapsedTime)>` where:
  - `Acquired`: Indicates whether the lock was successfully acquired.
  - `Lock`: The acquired lock, if successful.
  - `ElapsedTime`: Total time spent attempting acquisition.
- **Exceptions**: Throws `ArgumentNullException` if `provider` or `lockId` is `null`. Throws `ArgumentOutOfRangeException` if `timeout` is negative.

### `VerifyMetricsAsync`

Validates that the lock provider's internal metrics (e.g., acquisition attempts, success rate) are consistent with observed behavior.

- **Parameters**:
  - `ILockProvider provider`: The provider whose metrics are to be verified.
  - `int expectedAttempts`: The expected number of acquisition attempts.
  - `int expectedSuccesses`: The expected number of successful acquisitions.
- **Return value**: A `Task<bool>` resolving to `true` if metrics match expectations, otherwise `false`.
- **Exceptions**: Throws `ArgumentNullException` if `provider` is `null`. Throws `ArgumentOutOfRangeException` if `expectedAttempts` or `expectedSuccesses` is negative.

### `AcquireWithRetryAsync`

Attempts to acquire a lock with automatic retries on failure, returning detailed outcome information.

- **Parameters**:
  - `ILockProvider provider`: The provider used to attempt lock acquisition.
  - `string lockId`: The identifier of the lock to acquire.
  - `int maxAttempts`: Maximum number of acquisition attempts.
  - `TimeSpan retryDelay`: Delay between retry attempts.
  - `TimeSpan timeout`: Maximum total time to spend attempting acquisition.
- **Return value**: A `Task<(bool Acquired, Lock? Lock, int Attempts)>` where:
  - `Acquired`: Indicates whether the lock was successfully acquired.
  - `Lock`: The acquired lock, if successful.
  - `Attempts`: The number of attempts made.
- **Exceptions**: Throws `ArgumentNullException` if `provider` or `lockId` is `null`. Throws `ArgumentOutOfRangeException` if `maxAttempts` is less than 1, or if `retryDelay` or `timeout` is negative.

### `VerifyLockRepositoryConsistencyAsync`

Checks the internal consistency of the lock repository, ensuring no orphaned or inconsistent lock states exist.

- **Parameters**:
  - `ILockRepository repository`: The repository to verify.
- **Return value**: A `Task<bool>` resolving to `true` if the repository is consistent, otherwise `false`.
- **Exceptions**: Throws `ArgumentNullException` if `repository` is `null`.

## Usage

### Example 1: Measuring lock acquisition time under contention
