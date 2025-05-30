# LockCleanupWorker

A background worker that periodically scans and removes expired distributed locks from the backing store. It is designed to run as a singleton within a distributed application to prevent lock accumulation and resource leaks.

## API

### `public LockCleanupWorker`

Initializes a new instance of the `LockCleanupWorker` with default settings.
The worker will use the globally registered `IDistributedLockProvider` to query and remove expired locks.

### `public Task RunCleanupOnceAsync()`

Performs a single cleanup pass immediately.
Scans the lock store for entries older than `MinimumExpiredDuration`, removes them in batches of size `BatchSize`, and returns a `Task` representing the operation.

- **Return value**: A `Task` that completes when the cleanup pass finishes.

### `public override async Task StopAsync()`

Stops the periodic cleanup loop gracefully.
Any ongoing cleanup pass is allowed to complete, and subsequent calls to `RunCleanupOnceAsync()` will throw `ObjectDisposedException`.

- **Return value**: A `Task` that completes when the worker has stopped.

### `public int InitialDelayMs`

Gets or sets the delay in milliseconds before the first cleanup pass starts after `RunCleanupOnceAsync()` or the worker is started.
Defaults to 5000 ms.

- **Range**: Must be ≥ 0; otherwise throws `ArgumentOutOfRangeException`.

### `public int CleanupIntervalMs`

Gets or sets the interval in milliseconds between automatic cleanup passes.
Defaults to 60000 ms.

- **Range**: Must be ≥ 0; otherwise throws `ArgumentOutOfRangeException`.

### `public int BatchSize`

Gets or sets the maximum number of expired locks to remove in a single cleanup pass.
Defaults to 100.

- **Range**: Must be ≥ 1; otherwise throws `ArgumentOutOfRangeException`.

### `public bool VerboseLogging`

Gets or sets whether detailed logging is enabled during cleanup operations.
When `true`, emits log messages for each batch processed and any errors encountered.
Defaults to `false`.

### `public TimeSpan MinimumExpiredDuration`

Gets or sets the minimum age a lock must have to be considered expired and eligible for removal.
Defaults to 30 seconds.

- **Range**: Must be ≥ `TimeSpan.Zero`; otherwise throws `ArgumentOutOfRangeException`.

## Usage

### Basic usage with default settings
```csharp
var worker = new LockCleanupWorker();
await worker.RunCleanupOnceAsync();
```

### Customized worker with periodic execution
```csharp
var worker = new LockCleanupWorker
{
    InitialDelayMs = 10_000,
    CleanupIntervalMs = 300_000,
    BatchSize = 200,
    MinimumExpiredDuration = TimeSpan.FromMinutes(2),
    VerboseLogging = true
};

await worker.StartAsync(CancellationToken.None);
await Task.Delay(TimeSpan.FromMinutes(5));
await worker.StopAsync();
```

## Notes

- The worker is thread-safe for concurrent calls to `RunCleanupOnceAsync()` and property accessors.
- If `StopAsync()` is invoked while a cleanup pass is running, the running pass is allowed to complete before the worker fully stops.
- Property setters validate ranges immediately; changes take effect on the next cleanup pass or after `InitialDelayMs` if the worker is running.
- If the underlying `IDistributedLockProvider` throws during cleanup, the error is logged (when `VerboseLogging` is `true`) and the pass continues with the next batch.
