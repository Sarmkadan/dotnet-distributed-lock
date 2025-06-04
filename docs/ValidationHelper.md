# ValidationHelper

`ValidationHelper` provides a centralized set of static validation methods and an instance-based error accumulator for validating inputs and state related to distributed locking operations. It covers lock names, durations, renewal intervals, fencing tokens, owner identifiers, expiry checks, API keys, HTTP headers, and lock configuration objects. The instance members `IsValid` and `Errors` allow collecting multiple validation failures before acting on them.

## API

### Static Methods

#### `ValidateLockName`
```csharp
public static void ValidateLockName(string lockName)
```
Validates that a lock name is not null, empty, or composed solely of whitespace, and that it conforms to any length or character restrictions imposed by the underlying lock provider.  
**Parameters:** `lockName` — the lock identifier to validate.  
**Throws:** `ArgumentException` or a derived validation exception if the name is invalid.

#### `ValidateDuration`
```csharp
public static void ValidateDuration(TimeSpan duration)
```
Ensures that a lock duration is a positive value and does not exceed the maximum allowed duration.  
**Parameters:** `duration` — the lock lifetime to validate.  
**Throws:** `ArgumentOutOfRangeException` if the duration is zero, negative, or exceeds the configured maximum.

#### `ValidateRenewalInterval`
```csharp
public static void ValidateRenewalInterval(TimeSpan renewalInterval, TimeSpan duration)
```
Validates that the renewal interval is positive and logically consistent with the overall lock duration (e.g., shorter than the duration, respecting a minimum ratio).  
**Parameters:** `renewalInterval` — the period between automatic renewals; `duration` — the total lock duration.  
**Throws:** `ArgumentOutOfRangeException` if the interval is invalid relative to the duration.

#### `ValidateFencingToken`
```csharp
public static void ValidateFencingToken(long fencingToken)
```
Checks that a fencing token is a non-negative integer, ensuring it can serve as a monotonically increasing guard against stale lock holders.  
**Parameters:** `fencingToken` — the token to validate.  
**Throws:** `ArgumentOutOfRangeException` if the token is negative.

#### `ValidateOwnerId`
```csharp
public static void ValidateOwnerId(string ownerId)
```
Verifies that an owner identifier is present and meets format requirements (non-null, non-empty, and within length limits).  
**Parameters:** `ownerId` — the unique identifier of the lock owner.  
**Throws:** `ArgumentException` if the owner ID is invalid.

#### `ValidateLockNotExpired`
```csharp
public static void ValidateLockNotExpired(DateTime acquiredAt, TimeSpan duration)
```
Determines whether a lock has already expired based on its acquisition time and duration.  
**Parameters:** `acquiredAt` — the UTC timestamp when the lock was obtained; `duration` — the lock’s lifetime.  
**Throws:** `InvalidOperationException` if the lock is expired.

#### `ValidateApiKey`
```csharp
public static void ValidateApiKey(string apiKey)
```
Validates that an API key is present and conforms to the expected format for the backend provider.  
**Parameters:** `apiKey` — the API key string.  
**Throws:** `ArgumentException` if the key is missing or malformed.

#### `ThrowIfAnyErrors`
```csharp
public static void ThrowIfAnyErrors(ValidationHelper helper)
```
Aggregates all errors collected in a `ValidationHelper` instance and throws a single exception if any exist.  
**Parameters:** `helper` — a `ValidationHelper` instance whose `Errors` list is inspected.  
**Throws:** `AggregateException` (or a project-specific validation exception) containing all accumulated error messages.

#### `ValidateHeaders`
```csharp
public static void ValidateHeaders(IReadOnlyDictionary<string, string> headers)
```
Checks that required HTTP headers for lock provider communication are present and have valid values.  
**Parameters:** `headers` — a dictionary of header name-value pairs.  
**Throws:** `ArgumentException` if required headers are missing or contain invalid values.

#### `TryParseAs<T>`
```csharp
public static bool TryParseAs<T>(string value, out T result)
```
Attempts to parse a string into a specified type using registered converters, returning success or failure without throwing.  
**Parameters:** `value` — the string to parse; `result` — the parsed value on success, default on failure.  
**Returns:** `true` if parsing succeeded; otherwise `false`.

#### `ValidateLockConfiguration`
```csharp
public static ValidationResult ValidateLockConfiguration(LockConfiguration configuration)
```
Performs comprehensive validation of a `LockConfiguration` object, checking all its properties (name, duration, renewal, etc.) and returning a structured result.  
**Parameters:** `configuration` — the lock configuration to validate.  
**Returns:** A `ValidationResult` indicating success or listing all validation failures.

### Instance Members

#### `IsValid`
```csharp
public bool IsValid { get; }
```
Indicates whether the current `ValidationHelper` instance has accumulated any errors. Returns `true` when the `Errors` list is empty.

#### `Errors`
```csharp
public List<string> Errors { get; }
```
The list of error messages collected during validation operations performed on this instance. Each entry describes a specific validation failure.

## Usage

### Example 1: Accumulating multiple errors before throwing
```csharp
var helper = new ValidationHelper();

try { ValidationHelper.ValidateLockName(helper, ""); }
catch (ArgumentException ex) { helper.Errors.Add(ex.Message); }

try { ValidationHelper.ValidateDuration(helper, TimeSpan.Zero); }
catch (ArgumentOutOfRangeException ex) { helper.Errors.Add(ex.Message); }

if (!helper.IsValid)
{
    ValidationHelper.ThrowIfAnyErrors(helper);
}
```
This pattern collects individual validation failures into the `Errors` list and defers the exception until all checks have run, giving the caller a complete picture of what went wrong.

### Example 2: Validating a lock configuration before acquisition
```csharp
var config = new LockConfiguration
{
    Name = "resource:order:12345",
    Duration = TimeSpan.FromSeconds(30),
    RenewalInterval = TimeSpan.FromSeconds(10)
};

ValidationResult result = ValidationHelper.ValidateLockConfiguration(config);
if (!result.IsValid)
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"Configuration error: {error}");
    }
    return;
}

// Proceed with lock acquisition using validated configuration
await distributedLock.AcquireAsync(config);
```
Here `ValidateLockConfiguration` checks all interdependent settings at once, preventing invalid configurations from reaching the lock provider.

## Notes

- **Static vs. instance usage:** The static methods throw immediately on the first violation. To batch multiple checks, create a `ValidationHelper` instance, catch exceptions from the static methods, and add their messages to `Errors`. Call `ThrowIfAnyErrors` to raise a single aggregate exception.
- **Thread safety:** Instance members (`IsValid`, `Errors`) are not thread-safe. A `ValidationHelper` instance should be used within a single thread or synchronized externally if shared across threads.
- **`TryParseAs<T>`:** This method does not modify the `Errors` list and does not throw. It is a pure utility for type conversion attempts, typically used when parsing header values or provider-specific tokens.
- **`ValidateLockNotExpired`:** Uses the system clock at the moment of invocation. Callers should be aware that a lock that is valid at check time may expire before the subsequent operation if the duration is very short.
- **`ValidateHeaders`:** The set of required headers depends on the lock provider implementation. Consult the provider documentation for the exact header names and expected formats.
- **`ValidateLockConfiguration`:** Returns a `ValidationResult` rather than throwing, making it suitable for configuration UIs or startup validation where immediate exceptions are undesirable.
