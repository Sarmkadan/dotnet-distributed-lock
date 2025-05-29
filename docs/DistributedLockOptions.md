# DistributedLockOptions

`DistributedLockOptions` is a configuration class that provides settings for initializing and customizing the behavior of distributed locks in a .NET application. It allows fine-grained control over lock acquisition, renewal, retry policies, backend selection, and monitoring to suit various distributed coordination scenarios.

## API

### `BackendType BackendType`
Gets or sets the type of backend used to store and manage distributed locks. This determines which distributed coordination service (e.g., Redis, SQL Server, ZooKeeper) will be used to acquire and release locks. This property must be set before initializing a lock manager.

### `string ConnectionString`
Gets or sets the connection string used to connect to the backend service (e.g., Redis connection string, SQL Server connection string). The format and required parameters depend on the selected `BackendType`. This property must be set when `BackendType` is not `InMemory`.

### `TimeSpan DefaultLockDuration`
Gets or sets the default duration for which a lock is held if not explicitly specified during acquisition. This value defines how long the lock remains valid before automatic expiration. Must be greater than zero. Defaults to `TimeSpan.FromSeconds(30)`.

### `TimeSpan DefaultAcquisitionTimeout`
Gets or sets the default maximum time to wait when attempting to acquire a lock. If the lock cannot be acquired within this period, an exception is thrown. Must be non-negative. Defaults to `TimeSpan.FromSeconds(10)`.

### `TimeSpan DefaultRenewalInterval`
Gets or sets the default interval at which active locks are automatically renewed to prevent expiration. This helps maintain lock ownership during long-running operations. Must be positive. Defaults to `TimeSpan.FromSeconds(10)`.

### `int DefaultMaxRetries`
Gets or sets the default maximum number of retry attempts when lock acquisition fails transiently. Used in conjunction with `DefaultRetryDelayMs` to configure retry behavior. Must be non-negative. Defaults to `3`.

### `int DefaultRetryDelayMs`
Gets or sets the default delay in milliseconds between retry attempts when lock acquisition fails. Used with `DefaultMaxRetries` to define exponential backoff or fixed delay strategies. Must be non-negative. Defaults to `200`.

### `AcquisitionMode DefaultAcquisitionMode`
Gets or sets the default mode used when acquiring locks. Determines whether locks are acquired optimistically, pessimistically, or in a blocking manner. Defaults to `AcquisitionMode.Pessimistic`.

### `bool EnableAutoRenewal`
Gets or sets a value indicating whether automatic renewal of acquired locks is enabled. When `true`, locks are periodically renewed in the background to prevent expiration during long operations. Defaults to `true`.

### `bool UseFencingTokens`
Gets or sets a value indicating whether fencing tokens are used to prevent deadlocks and ensure linearizable lock releases. When enabled, each lock acquisition returns a unique token that must be provided during release to validate ownership. Defaults to `false`.

### `TimeSpan MonitoringInterval`
Gets or sets the interval at which the lock manager monitors and cleans up expired or stale locks. This helps prevent accumulation of orphaned locks in the backend. Must be positive. Defaults to `TimeSpan.FromMinutes(1)`.

### `int MaxConcurrentLocks`
Gets or sets the maximum number of concurrent locks that can be held by a single client or across the system, depending on backend support. Used to prevent resource exhaustion. A value of `0` indicates no limit. Defaults to `0`.

### `bool EnableMetrics`
Gets or sets a value indicating whether runtime metrics (e.g., lock acquisition time, retry counts) are collected and exposed. When enabled, metrics can be used for monitoring and diagnostics. Defaults to `false`.

### `bool EnableLogging`
Gets or sets a value indicating whether verbose logging is enabled for lock operations (e.g., acquisition, renewal, release). Useful for debugging but may impact performance. Defaults to `false`.

### `int RetryPolicyMaxRetries`
Gets or sets the maximum number of retry attempts for transient failures during lock operations (e.g., backend connectivity issues). Overrides `DefaultMaxRetries` when set. Must be non-negative.

### `int RetryPolicyInitialDelayMs`
Gets or sets the initial delay in milliseconds before the first retry attempt during transient failures. Used in conjunction with exponential backoff strategies. Must be non-negative. Defaults to `100`.

### `int RetryPolicyMaxDelayMs`
Gets or sets the maximum delay in milliseconds between retry attempts during transient failures. Limits the upper bound of exponential backoff. Must be greater than or equal to `RetryPolicyInitialDelayMs`. Defaults to `5000`.

### `double RetryPolicyJitterFactor`
Gets or sets a jitter factor (between `0.0` and `1.0`) applied to retry delays to avoid thundering herd effects. A value of `0.1` means delays are randomized by ±10%. Defaults to `0.0`.

### `IEnumerable<string> Validate()`
Validates the current configuration and returns a list of validation errors. Each error describes a configuration issue (e.g., missing `ConnectionString`, invalid `TimeSpan` values). Returns an empty collection if all settings are valid. This method does not modify the object state.

## Usage

### Example 1: Basic Redis-backed lock with auto-renewal
