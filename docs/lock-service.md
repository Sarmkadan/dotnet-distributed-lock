# LockService and ILockService

`ILockService` is the contract for distributed lock operations. `LockService` is the default implementation. It sits on top of `ILockRepository`, so the same API works with the in-memory, Redis, PostgreSQL and SQLite backends.

- Interface: `SarmKadan.DistributedLock.Services.ILockService`
- Implementation: `SarmKadan.DistributedLock.Services.LockService` (sealed)
- Registration: `services.AddScoped<ILockService, LockService>()` (done by `AddDistributedLocking`)

## Constructors

```csharp
public LockService(ILockRepository repository, ILogger<LockService> logger);
public LockService(ILockRepository repository, ILogger<LockService> logger, ILockRetryPolicy retryPolicy);
```

- `repository`, `logger` and `retryPolicy` must not be `null` (`ArgumentNullException`).
- The first constructor uses `DefaultLockRetryPolicy` with its default settings.
- Each instance has its own `LockMetrics`. Because the service is registered as scoped, metrics are per scope. Read them with `GetMetrics()`.

## Defaults

| Setting | Value | Source |
|---|---|---|
| Lock duration when `duration` is `null` | 30 s | `LockConstants.DefaultLockTimeout` |
| Acquisition timeout used by `AcquireAsync` | 5 s | `LockConstants.DefaultAcquisitionTimeout` |
| Max retries | 3 | `LockConstants.DefaultMaxRetries` |
| Initial retry delay | 100 ms | `LockConstants.DefaultRetryDelayMilliseconds` |
| Max retry delay | 5000 ms | `LockConstants.MaximumRetryDelayMilliseconds` |

`DefaultLockRetryPolicy` uses exponential backoff: `InitialDelay * 2^attempt`, capped at `MaxDelay`, plus jitter of up to `JitterFactor` (default 0.1). `MaxRetries` must be `>= 0` and `JitterFactor` must be in `[0, 1]`, otherwise the constructor throws `ArgumentOutOfRangeException`.

## API

Every method takes an optional `CancellationToken`.

### `TryAcquireAsync`

```csharp
Task<(bool Success, Lock? Lock, string? ErrorMessage)> TryAcquireAsync(
    string lockKey, string ownerId, TimeSpan? duration = null, CancellationToken cancellationToken = default);
```

Makes a single, non-blocking attempt.

- Success: `(true, lock, null)`. The lock status is `Held`.
- Lock held by someone else: `(false, null, "Lock is currently held by another owner")`.
- Any exception from the repository: `(false, null, ex.Message)`. The exception is logged and not rethrown.

Argument validation runs before the `try` block, so it does throw. `null` throws `ArgumentNullException`. Empty or whitespace throws `ArgumentException`.

### `AcquireAsync`

```csharp
Task<Lock> AcquireAsync(
    string lockKey, string ownerId, TimeSpan? duration = null, CancellationToken cancellationToken = default);
```

Calls `TryAcquireAsync` in a loop with backoff from the retry policy.

- Returns the `Lock` on the first successful attempt.
- Throws `LockAcquisitionException` if all attempts fail, or if the acquisition timeout (5 s) is exceeded before an attempt starts.
- Throws `ArgumentNullException` or `ArgumentException` for invalid `lockKey` or `ownerId`.
- Cancellation: the wait between attempts is `Task.Delay(delay, cancellationToken)`, so cancellation throws `OperationCanceledException`. See the caveat below about attempts.

The acquisition timeout is checked only before each attempt. An attempt that is already running is not interrupted by the timeout.

### `AcquireWithRenewalAsync`

```csharp
Task<LockHandle> AcquireWithRenewalAsync(
    string lockKey, string ownerId, LockAcquisitionOptions? options = null, CancellationToken cancellationToken = default);
```

Acquires the lock with `AcquireAsync` and wraps it in a `LockHandle` that renews it in the background.

- `options` defaults to `new LockAcquisitionOptions()`. Its `Validate()` runs first. `RenewalFraction` must be in `(0.01, 0.99)` and `MaxRenewals` must be `>= 0`. Otherwise it throws `ArgumentException`.
- The lock is acquired with the default duration. The `duration` argument is not part of this method.
- Renewal interval: `Lock.Duration * RenewalFraction`. If that is not positive, it falls back to 1 s.
- Throws `LockAcquisitionException` if the lock cannot be acquired.

### `RenewAsync`

```csharp
Task<bool> RenewAsync(
    string lockKey, string ownerId, TimeSpan? newDuration = null, CancellationToken cancellationToken = default);
```

Extends the lock for `ownerId` to `newDuration` (default 30 s).

- Returns `true` on success. Returns `false` if the lock does not exist, the owner does not match, or an exception occurred. Exceptions are logged and swallowed.
- Argument validation throws as usual.

### `TryExtendAsync`

```csharp
Task<bool> TryExtendAsync(
    string lockKey, string ownerId, TimeSpan extension, CancellationToken cancellationToken = default);
```

Same as `RenewAsync`, but takes a required `extension` value.

- `extension <= TimeSpan.Zero` throws `ArgumentOutOfRangeException`.
- Returns `false` on a missing lock, owner mismatch, or repository exception.

### `RenewLockAsync`

```csharp
Task<Lock> RenewLockAsync(
    string lockKey, ulong fencingToken, TimeSpan newDuration, CancellationToken cancellationToken = default);
```

Renews the lock only if `fencingToken` is still the latest token issued for `lockKey`. It is the fencing-token-guarded variant of `RenewAsync`. It returns the renewed `Lock`.

Exceptions:
- `SarmKadan.DistributedLock.Exceptions.InvalidFencingTokenException`: the token is stale or invalid.
- `SarmKadan.DistributedLock.Core.Exceptions.LockRenewalFailedException`: the lock is missing, the renewal was rejected by the repository, or the lock is missing after renewal. Note the namespace: it is under `Core.Exceptions`, not `Exceptions`.
- `ArgumentNullException` or `ArgumentException` for an invalid `lockKey`. The `newDuration` value is not validated.

Unlike `RenewAsync`, this method does not swallow errors. Callers must handle the exceptions above.

### `ReleaseAsync`

```csharp
Task<bool> ReleaseAsync(
    string lockKey, string ownerId, CancellationToken cancellationToken = default);
```

Releases the lock held by `ownerId`.

- Returns `true` if released. Returns `false` if the lock does not exist, the owner does not match, or an exception occurred (logged, not rethrown).
- Argument validation throws as usual.

### `GetLockAsync`

```csharp
Task<Lock?> GetLockAsync(string lockKey, CancellationToken cancellationToken = default);
```

Returns the current `Lock`, or `null` if there is none. Repository exceptions are logged and return `null`.

### `IsLockedAsync`

```csharp
Task<bool> IsLockedAsync(string lockKey, CancellationToken cancellationToken = default);
```

Returns `true` if a lock exists for `lockKey`. Repository exceptions are logged and return `false`.

### `GetAllActiveLockAsync`

```csharp
Task<IEnumerable<Lock>> GetAllActiveLockAsync(CancellationToken cancellationToken = default);
```

Returns all active locks. Repository exceptions are logged and return an empty sequence.

Note the method name: it is `GetAllActiveLockAsync` (singular "Lock").

### `GetMetrics`

```csharp
public LockMetrics GetMetrics();
```

Returns the metrics object for this instance (`LockMetrics`). This method is on the class only, not on `ILockService`.

## Usage

### Registration

```csharp
builder.Services.AddDistributedLocking(/* backend and options */);
```

Inject `ILockService` through the constructor. Because it is scoped, do not inject it into a singleton directly.

### Single attempt

```csharp
var (success, lock, error) = await lockService.TryAcquireAsync("invoice:42", "worker-1");
if (!success)
{
    logger.LogInformation("Skipped: {Error}", error);
    return;
}

try
{
    await ProcessInvoiceAsync(42);
}
finally
{
    await lockService.ReleaseAsync("invoice:42", "worker-1");
}
```

### Blocking acquisition with a timeout

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

try
{
    var @lock = await lockService.AcquireAsync("report:daily", "worker-1", TimeSpan.FromMinutes(2), cts.Token);
    try
    {
        await BuildReportAsync();
    }
    finally
    {
        await lockService.ReleaseAsync("report:daily", "worker-1");
    }
}
catch (LockAcquisitionException ex)
{
    logger.LogWarning("Could not lock {Key} after {Retries} retries", ex.LockKey, ex.RetryCount);
}
```

### Automatic renewal

```csharp
await using var handle = await lockService.AcquireWithRenewalAsync(
    "import:nightly",
    "worker-1",
    new LockAcquisitionOptions { RenewalFraction = 0.33, MaxRenewals = 3 });

await RunLongImportAsync(handle.DisposalToken);
// Disposing the handle stops renewal and releases the lock.
```

Check `handle.IsValid` or `handle.RenewalFailedToken` during long work. Renewal failures are signalled there, not by exception.

### Fencing token renewal

```csharp
var renewed = await lockService.RenewLockAsync("job:sync", fencingToken, TimeSpan.FromMinutes(1));
```

Catch `InvalidFencingTokenException` and stop the work. Another owner has taken over the resource.

## Errors

| Situation | Method behavior |
|---|---|
| `lockKey` or `ownerId` is `null` | `ArgumentNullException` (all methods) |
| `lockKey` or `ownerId` is empty or whitespace | `ArgumentException` (all methods) |
| Lock held by someone else, `TryAcquireAsync` | `(false, null, "Lock is currently held by another owner")` |
| Lock held by someone else, `AcquireAsync` / `AcquireWithRenewalAsync` | `LockAcquisitionException` after retries |
| Acquisition timeout (5 s) reached | `LockAcquisitionException` |
| Repository exception, `TryAcquireAsync` | `(false, null, ex.Message)` |
| Repository exception, `RenewAsync`, `TryExtendAsync`, `ReleaseAsync` | `false` |
| Repository exception, `GetLockAsync`, `IsLockedAsync`, `GetAllActiveLockAsync` | `null`, `false`, or empty sequence |
| `TryExtendAsync` with `extension <= 0` | `ArgumentOutOfRangeException` |
| `AcquireWithRenewalAsync` with invalid options | `ArgumentException` |
| `DefaultLockRetryPolicy` with invalid arguments | `ArgumentOutOfRangeException` (constructor) |
| Stale fencing token, `RenewLockAsync` | `InvalidFencingTokenException` |
| Lock missing or renewal rejected, `RenewLockAsync` | `Core.Exceptions.LockRenewalFailedException` |
| Cancellation | `OperationCanceledException` from `Task.Delay` or the repository |

Exception types:
- `LockAcquisitionException` (`SarmKadan.DistributedLock.Exceptions`): properties `LockKey`, `Timeout`, `RetryCount`.
- `InvalidFencingTokenException` (`SarmKadan.DistributedLock.Exceptions`): properties `ProvidedToken`, `CurrentToken`.
- `LockRenewalFailedException` (`SarmKadan.DistributedLock.Core.Exceptions`): property `LockId`.
- All of the above derive from `DistributedLockException`.

## Caveats

- **Swallowed backend errors.** `TryAcquireAsync`, `RenewAsync`, `TryExtendAsync`, `ReleaseAsync`, `GetLockAsync`, `IsLockedAsync` and `GetAllActiveLockAsync` catch repository exceptions and return a neutral value. A backend outage looks like "not locked" or "release failed". Check the logs, or the `false` result, before assuming the state is known.
- **Cancellation inside a try.** `TryAcquireAsync` catches every exception, including `OperationCanceledException`, and returns `(false, null, message)`. Callers that pass a token should check `cancellationToken.IsCancellationRequested` themselves. `AcquireAsync` does not check the token between attempts. It is passed to the repository and to `Task.Delay`, and a cancelled attempt is reported as a failed attempt and retried until the loop ends.
- **Release by owner only.** `ReleaseAsync` and `RenewAsync` match on `ownerId`. Use the same string for the whole lifetime of the lock.
- **Default duration.** `AcquireAsync` and `AcquireWithRenewalAsync` without a duration use 30 s. For long work, pass a duration or use `AcquireWithRenewalAsync`.
- **Interface XML comments.** The `ILockService` comments for `TryAcquireAsync` and some other methods say `ArgumentNullException` only. Empty or whitespace input throws `ArgumentException`. The behavior above is what the code does.
