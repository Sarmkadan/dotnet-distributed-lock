# LockAcquisitionTests

`LockAcquisitionTests` is a unit test class that validates the behavior of lock acquisition operations, including constructor initialization, retry logic, timing calculations, and configuration validation for distributed locks. It verifies both success and failure paths, ensuring proper state management and error handling.

## API

### Constructor_WithValidArguments_SetsPropertiesCorrectly
Constructs a `LockAcquisitionTests` instance with valid arguments and asserts that all properties are initialized correctly. Validates that the lock key, requester ID, and retry configuration are set as expected.

### Constructor_WithNullOrWhiteSpaceLockKey_ThrowsArgumentException
Constructs a `LockAcquisitionTests` instance with a null or whitespace lock key and verifies that an `ArgumentException` is thrown. Ensures invalid lock keys are rejected early.

### Constructor_WithNullOrWhiteSpaceRequesterId_ThrowsArgumentException
Constructs a `LockAcquisitionTests` instance with a null or whitespace requester ID and verifies that an `ArgumentException` is thrown. Ensures invalid requester IDs are rejected early.

### RecordAttempt_WhenSucceeded_SetsIsSuccessfulAndRecordsAcquiredAt
Records a successful lock acquisition attempt and asserts that `IsSuccessful` is set to `true` and `AcquiredAt` is populated with the current timestamp. Validates state updates on success.

### RecordAttempt_WhenFailed_IncrementsCountAndStoresErrorMessage
Records a failed lock acquisition attempt and asserts that the attempt count is incremented and the error message is stored. Validates error tracking and state consistency.

### CanRetry_WhenAttemptsRemaining_ReturnsTrue
Invokes `CanRetry` when retry attempts remain and asserts that it returns `true`. Validates the retry logic under normal conditions.

### CanRetry_WhenMaxRetriesExhausted_ReturnsFalse
Invokes `CanRetry` after all retry attempts are exhausted and asserts that it returns `false`. Validates the retry exhaustion path.

### CanRetry_AfterSuccessfulAcquisition_ReturnsFalse
Invokes `CanRetry` after a successful lock acquisition and asserts that it returns `false`. Ensures no further retries occur after success.

### AverageAttemptTime_WithMultipleTimedAttempts_ReturnsCorrectAverage
Records multiple timed lock acquisition attempts and asserts that `AverageAttemptTime` returns the correct average duration. Validates timing calculations.

### AverageAttemptTime_WithNoAttempts_ReturnsZero
Invokes `AverageAttemptTime` with no recorded attempts and asserts that it returns `0`. Validates edge case handling for empty state.

### ToString_ContainsLockKeyRequesterIdAndMode
Invokes `ToString` and asserts that the output contains the lock key, requester ID, and lock mode. Validates the string representation is informative.

### Constructor_WithValidKey_SetsDefaultValues
Constructs a `LockAcquisitionTests` instance with a valid key and asserts that default values (e.g., retry count, duration) are set correctly. Validates default initialization.

### Constructor_WithNullOrWhiteSpaceKey_ThrowsArgumentException
Constructs a `LockAcquisitionTests` instance with a null or whitespace key and verifies that an `ArgumentException` is thrown. Ensures invalid keys are rejected early.

### Validate_WithDefaultConfig_ReturnsNoErrors
Validates a default lock configuration and asserts that no validation errors are returned. Validates the happy path for configuration validation.

### Validate_WithAutoRenewalAndRenewalIntervalEqualToDuration_ReturnsError
Validates a lock configuration with auto-renewal enabled and a renewal interval equal to the lock duration, and asserts that a validation error is returned. Ensures invalid auto-renewal configurations are detected.

### Validate_WithNegativeMaxRetries_ReturnsError
Validates a lock configuration with a negative `MaxRetries` value and asserts that a validation error is returned. Ensures invalid retry counts are rejected.

### Validate_WithDurationBelowMinimum_ReturnsError
Validates a lock configuration with a lock duration below the minimum allowed value and asserts that a validation error is returned. Ensures invalid durations are rejected.

### Validate_WithDefaultOptions_ReturnsNoErrors
Validates a lock configuration with default options and asserts that no validation errors are returned. Validates the happy path for default configurations.

### Validate_WithNonInMemoryBackendAndEmptyConnectionString_ReturnsError
Validates a lock configuration with a non-in-memory backend and an empty connection string, and asserts that a validation error is returned. Ensures required connection strings are enforced.

### Validate_WhenRenewalIntervalExceedsLockDuration_ReturnsError
Validates a lock configuration where the renewal interval exceeds the lock duration, and asserts that a validation error is returned. Ensures invalid renewal intervals are rejected.

## Usage
