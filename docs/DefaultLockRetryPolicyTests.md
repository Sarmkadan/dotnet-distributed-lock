# DefaultLockRetryPolicyTests
The `DefaultLockRetryPolicyTests` class is a test suite designed to verify the correctness of the `DefaultLockRetryPolicy` class. It provides a comprehensive set of test cases to ensure that the retry policy behaves as expected under various scenarios, including default and custom settings, exponential backoff, and jitter.

## API
* `Constructor_WithDefaults_SetsReasonableValues`: Verifies that the constructor sets reasonable default values when no custom settings are provided.
* `Constructor_WithCustomValues_SetsCustomValues`: Verifies that the constructor sets custom values when provided.
* `Constructor_WithNegativeMaxRetries_ThrowsArgumentOutOfRangeException`: Verifies that the constructor throws an `ArgumentOutOfRangeException` when the maximum number of retries is negative.
* `Constructor_WithInvalidJitterFactor_ThrowsArgumentOutOfRangeException`: Verifies that the constructor throws an `ArgumentOutOfRangeException` when the jitter factor is invalid.
* `Constructor_WithValidJitterFactor_DoesNotThrow`: Verifies that the constructor does not throw an exception when the jitter factor is valid.
* `GetDelay_ForFirstAttempt_ReturnsInitialDelayWithJitter`: Verifies that the `GetDelay` method returns the initial delay with jitter for the first attempt.
* `GetDelay_ExponentialBackoff_IncreasesWithEachAttempt`: Verifies that the `GetDelay` method increases the delay exponentially with each attempt.
* `GetDelay_CapsByMaxDelay`: Verifies that the `GetDelay` method caps the delay by the maximum delay.
* `GetDelay_WithNoJitter_ProducesConsistentValues`: Verifies that the `GetDelay` method produces consistent values when no jitter is applied.
* `GetDelay_WithJitter_ProducesDifferentValues`: Verifies that the `GetDelay` method produces different values when jitter is applied.
* `GetDelay_RespectsBounds`: Verifies that the `GetDelay` method respects the bounds of the delay.

## Usage
The following examples demonstrate how to use the `DefaultLockRetryPolicyTests` class:
```csharp
// Example 1: Verifying default settings
var policy = new DefaultLockRetryPolicy();
Assert.AreEqual(3, policy.MaxRetries);
Assert.AreEqual(100, policy.InitialDelay);
Assert.AreEqual(500, policy.MaxDelay);

// Example 2: Verifying custom settings
var customPolicy = new DefaultLockRetryPolicy(5, 200, 1000);
Assert.AreEqual(5, customPolicy.MaxRetries);
Assert.AreEqual(200, customPolicy.InitialDelay);
Assert.AreEqual(1000, customPolicy.MaxDelay);
```

## Notes
The `DefaultLockRetryPolicyTests` class is designed to be thread-safe, as it does not rely on any shared state. However, it is essential to note that the `GetDelay` method may produce different results when jitter is applied, even if the same input parameters are used. Additionally, the `Constructor_WithNegativeMaxRetries_ThrowsArgumentOutOfRangeException` and `Constructor_WithInvalidJitterFactor_ThrowsArgumentOutOfRangeException` tests highlight the importance of providing valid input parameters to the constructor to avoid exceptions.
