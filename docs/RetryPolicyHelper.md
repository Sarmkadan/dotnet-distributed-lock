# RetryPolicyHelper

A utility class providing configurable retry policies for transient fault handling in distributed scenarios. It supports both synchronous and asynchronous retry execution with exponential backoff strategies, allowing resilient execution of operations that may temporarily fail due to transient conditions such as network issues or temporary unavailability.

## API

### `ExecuteWithRetryAsync<T>`

Executes the provided asynchronous operation with retry logic using the default or configured retry policy.

- **Parameters**
  - `func` (Func<Task<T>>): The asynchronous operation to execute.
- **Return Value**
  - Task<T>: The result of the operation if successful.
- **Exceptions**
  - Throws `RetryFailedException` if all retry attempts are exhausted without success.

### `ExecuteWithRetry<T>`

Executes the provided synchronous operation with retry logic using the default or configured retry policy.

- **Parameters**
  - `func` (Func<T>): The synchronous operation to execute.
- **Return Value**
  - T: The result of the operation if successful.
- **Exceptions**
  - Throws `RetryFailedException` if all retry attempts are exhausted without success.

### `ExecuteWithLinearRetryAsync<T>`

Executes the provided asynchronous operation with a linear retry strategy, where each retry occurs after a fixed delay.

- **Parameters**
  - `func` (Func<Task<T>>): The asynchronous operation to execute.
  - `delayMs` (int): The fixed delay in milliseconds between retries.
  - `maxRetries` (int): The maximum number of retry attempts.
- **Return Value**
  - Task<T>: The result of the operation if successful.
- **Exceptions**
  - Throws `RetryFailedException` if all retry attempts are exhausted without success.

### `CreatePolicy`

Creates a new retry policy with the specified configuration.

- **Parameters**
  - `maxRetries` (int): The maximum number of retry attempts.
  - `initialDelayMs` (int): The initial delay in milliseconds before the first retry.
  - `backoffMultiplier` (double): The multiplier applied to the delay after each retry attempt.
- **Return Value**
  - RetryPolicy: A configured retry policy instance.

### `MaxRetries` (property)

Gets or sets the maximum number of retry attempts allowed.

- **Type**
  - int

### `InitialDelayMs` (property)

Gets or sets the initial delay in milliseconds before the first retry.

- **Type**
  - int

### `BackoffMultiplier` (property)

Gets or sets the multiplier applied to the delay after each retry attempt.

- **Type**
  - double

## Usage
