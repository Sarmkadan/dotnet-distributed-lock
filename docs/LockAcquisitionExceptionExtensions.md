# LockAcquisitionExceptionExtensions

Extension methods for `LockAcquisitionException` that provide detailed error information, timeout detection, and retry guidance for distributed lock acquisition failures.

## API

### `ToDetailedErrorMessage`

Formats a `LockAcquisitionException` into a human-readable error message including the lock name, resource identifier, and failure details.

```csharp
public static string ToDetailedErrorMessage(this LockAcquisitionException exception)
```

**Returns**
A string containing the formatted error message. Never returns `null`.

**Throws**
`ArgumentNullException` if `exception` is `null`.

---

### `IsTimeoutRelated`

Determines whether a `LockAcquisitionException` represents a timeout-related failure, as opposed to other types of acquisition failures (e.g., deadlocks or permission issues).

```csharp
public static bool IsTimeoutRelated(this LockAcquisitionException exception)
```

**Returns**
`true` if the exception is timeout-related; otherwise, `false`.

**Throws**
`ArgumentNullException` if `exception` is `null`.

---

### `ToLoggableMessage`

Generates a concise, log-safe message from a `LockAcquisitionException` suitable for inclusion in structured logs. The message omits sensitive or variable details that may change between environments.

```csharp
public static string ToLoggableMessage(this LockAcquisitionException exception)
```

**Returns**
A string containing the sanitized log message. Never returns `null`.

**Throws**
`ArgumentNullException` if `exception` is `null`.

---

### `CalculateSuggestedRetryDelay`

Computes a suggested delay (in milliseconds) before retrying a failed lock acquisition, based on the exception’s timeout characteristics and system heuristics.

```csharp
public static int CalculateSuggestedRetryDelay(this LockAcquisitionException exception)
```

**Returns**
An integer representing the suggested delay in milliseconds. Always returns a non-negative value.

**Throws**
`ArgumentNullException` if `exception` is `null`.

## Usage

### Example 1: Logging a detailed error
```csharp
try
{
    await distributedLock.AcquireAsync(lockName, timeout: TimeSpan.FromSeconds(5));
}
catch (LockAcquisitionException ex)
{
    logger.LogError(ex.ToDetailedErrorMessage());
    if (ex.IsTimeoutRelated())
    {
        var delayMs = ex.CalculateSuggestedRetryDelay();
        logger.LogInformation("Retry suggested after {DelayMs}ms", delayMs);
    }
}
```

### Example 2: Retry policy with backoff
```csharp
var retryCount = 0;
while (retryCount < maxRetries)
{
    try
    {
        await distributedLock.AcquireAsync(lockName, timeout: TimeSpan.FromSeconds(5));
        break;
    }
    catch (LockAcquisitionException ex) when (ex.IsTimeoutRelated())
    {
        retryCount++;
        var delayMs = ex.CalculateSuggestedRetryDelay();
        await Task.Delay(delayMs);
    }
}
```

## Notes

- All methods are thread-safe and do not mutate the input `LockAcquisitionException`.
- `CalculateSuggestedRetryDelay` returns a value derived from the exception’s timeout and system heuristics; it does not guarantee success on retry.
- `ToLoggableMessage` intentionally omits variable details (e.g., timestamps or correlation IDs) to ensure consistent log structure across environments.
- Methods assume `LockAcquisitionException` is non-null; passing `null` will throw `ArgumentNullException` as documented.
