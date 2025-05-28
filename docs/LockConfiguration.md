# LockConfiguration

Represents the configuration parameters required to acquire and maintain a distributed lock. Instances of this class define the lock key, timing constraints, retry behavior, renewal policy, and optional fencing token support. The class is typically used with a distributed lock provider to control how a lock is obtained and kept alive.

## API

### Properties

- **`public string LockKey`**  
  Gets or sets the unique identifier for the lock. This key is used by the distributed lock provider to distinguish between different locks. Must not be null or empty.

- **`public TimeSpan LockDuration`**  
  Gets or sets the duration for which the lock is held. After this period elapses, the lock is automatically released unless it is renewed. Must be positive.

- **`public TimeSpan AcquisitionTimeout`**  
  Gets or sets the maximum time allowed to acquire the lock. If the lock cannot be obtained within this period, the acquisition attempt fails. Must be non-negative.

- **`public AcquisitionMode AcquisitionMode`**  
  Gets or sets the mode used when acquiring the lock. The `AcquisitionMode` enumeration defines strategies such as `Wait` (block until acquired or timeout) or `Throw` (throw immediately if not available).

- **`public int MaxRetries`**  
  Gets or sets the maximum number of retry attempts when acquiring the lock fails. Must be non-negative.

- **`public TimeSpan RetryInterval`**  
  Gets or sets the delay between retry attempts. Must be non-negative.

- **`public TimeSpan RenewalInterval`**  
  Gets or sets the interval at which the lock is automatically renewed when `AutoRenewal` is enabled. Must be positive and less than `LockDuration`.

- **`public bool AutoRenewal`**  
  Gets or sets a value indicating whether the lock should be automatically renewed before it expires. When `true`, the lock is renewed every `RenewalInterval`.

- **`public bool UseFencingToken`**  
  Gets or sets a value indicating whether a fencing token should be generated and used. Fencing tokens help prevent stale lock holders from interfering with critical sections.

- **`public string? Metadata`**  
  Gets or sets optional metadata associated with the lock. This value is opaque to the lock provider and can be used to store application-specific information (e.g., owner identity, request ID).

### Constructors

- **`public LockConfiguration()`**  
  Initializes a new instance of `LockConfiguration` with default values. All properties are set to their default values (e.g., `LockDuration` defaults to 30 seconds, `AcquisitionTimeout` to 10 seconds, `MaxRetries` to 0, etc.).

- **`public LockConfiguration(string lockKey, TimeSpan lockDuration)`**  
  Initializes a new instance with the specified lock key and lock duration. Other properties are set to their default values.  
  **Throws:** `ArgumentException` if `lockKey` is null or empty, or if `lockDuration` is not positive.

### Methods

- **`public IEnumerable<string> Validate()`**  
  Validates the current configuration and returns a collection of error messages for any invalid property values. If the configuration is valid, the returned collection is empty. Validation checks include:  
  - `LockKey` is not null or empty.  
  - `LockDuration` is positive.  
  - `AcquisitionTimeout` is non-negative.  
  - `MaxRetries` is non-negative.  
  - `RetryInterval` is non-negative.  
  - `RenewalInterval` is positive and less than `LockDuration` when `AutoRenewal` is enabled.  

- **`public override string ToString()`**  
  Returns a string representation of the current configuration, including the lock key and key property values. Useful for logging and debugging.

## Usage

### Example 1: Basic lock acquisition with retries

```csharp
using DistributedLock;

var config = new LockConfiguration("order-123", TimeSpan.FromSeconds(30))
{
    AcquisitionTimeout = TimeSpan.FromSeconds(15),
    MaxRetries = 3,
    RetryInterval = TimeSpan.FromSeconds(2),
    AutoRenewal = true,
    RenewalInterval = TimeSpan.FromSeconds(10)
};

// Validate before use
var errors = config.Validate().ToList();
if (errors.Any())
{
    Console.WriteLine($"Configuration errors: {string.Join(", ", errors)}");
    return;
}

// Acquire the lock (provider-specific)
using var lockHandle = await lockProvider.AcquireAsync(config);
// Critical section...
```

### Example 2: Using fencing tokens and metadata

```csharp
var config = new LockConfiguration("payment-gateway", TimeSpan.FromSeconds(60))
{
    UseFencingToken = true,
    Metadata = "request-abc-123",
    AcquisitionTimeout = TimeSpan.FromSeconds(5),
    AutoRenewal = false
};

if (!config.Validate().Any())
{
    var handle = await lockProvider.AcquireAsync(config);
    Console.WriteLine($"Fencing token: {handle.FencingToken}");
    // Perform payment operation...
    handle.Dispose();
}
```

## Notes

- **Validation:** Always call `Validate()` before using the configuration to ensure all properties are consistent. Invalid configurations may cause runtime exceptions or undefined behavior in the lock provider.
- **Thread safety:** `LockConfiguration` is not thread-safe for concurrent writes. If the same instance is shared across threads, synchronize access or create a new instance per operation. Reading properties after construction is safe.
- **Edge cases:**
  - An empty or null `LockKey` will cause validation errors and should be avoided.
  - Setting `LockDuration` to a value less than or equal to `RenewalInterval` when `AutoRenewal` is enabled will cause validation to fail.
  - A zero `AcquisitionTimeout` means the acquisition attempt will fail immediately if the lock is not available.
  - `MaxRetries` of 0 means no retries are attempted.
- **Fencing tokens:** When `UseFencingToken` is `true`, the lock provider must support token generation. If not supported, the property is ignored or an exception may be thrown at acquisition time.
