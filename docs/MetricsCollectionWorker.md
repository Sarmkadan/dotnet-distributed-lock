# MetricsCollectionWorker

The `MetricsCollectionWorker` is a background service responsible for periodically gathering metrics related to distributed lock usage, caching behavior, and custom application data. It stores collected snapshots in memory, provides access to the most recent snapshot, a list of retained snapshots, and an averaged view of the metrics over the retention window. The worker can be started and stopped as part of an `IHostedService` lifecycle and exposes configuration properties to control collection frequency, initial delay, snapshot retention, and verbosity.

## API

### MetricsCollectionWorker()
Creates a new instance of the worker with default configuration values. The instance is not started automatically; call `StartAsync` (inherited from `IHostedService`) to begin metric collection.

**Parameters:** None.  
**Returns:** A new `MetricsCollectionWorker` object.  
**Throws:** None.

### MetricsSnapshot? GetCurrentSnapshot()
Retrieves the most recent metrics snapshot captured by the worker. If no snapshot has been taken yet, the method returns `null`.

**Parameters:** None.  
**None.**  
**Return value:** The latest `MetricsSnapshot` or `null` when no data is available.  
**Throws:** None.

### List<MetricsSnapshot> GetSnapshots()
Returns a read‑only list containing all snapshots currently retained by the worker. The list reflects the snapshots that have not yet expired based on the `SnapshotRetentionSeconds` setting.

**Parameters:****None.**  
**Return value:** A `List<MetricsSnapshot>` containing zero or more snapshots ordered from oldest to newest.  
**Throws:** None.

### MetricsSnapshot? GetAverageMetrics()
Computes and returns an averaged metrics snapshot derived from all retained snapshots. If no snapshots are available, the method returns `null`.

**Parameters:****None.**  
**Return value:** An averaged `MetricsSnapshot` or `null` when there is no data to average.  
**Throws:** None.

### public override async Task StopAsync(CancellationToken cancellationToken)
Stops the background collection loop gracefully. The method waits for any ongoing collection cycle to finish before returning, unless the supplied cancellation token is triggered.

**Parameters:**  
- `cancellationToken`: A token that can be used to request early termination of the stop operation.  
**Return value:** A `Task` that completes when the worker has stopped.  
**Throws:**  
- `OperationCanceledException` if the cancellation token is triggered before the stop operation finishes.  
- Any exception thrown by the underlying timer disposal or resource cleanup propagates as an `AggregateException` wrapped in the returned task.

### DateTime Timestamp
Gets the UTC date and time of the most recent metrics collection cycle. If no collection has occurred yet, the value represents the worker's initialization time.

**Return value:** A `DateTime` indicating when the last snapshot was taken.  
**Throws:** None.

### CacheStatistics? CacheStatistics
Gets or sets the latest cache‑related statistics captured during the most recent collection. The value may be `null` if cache statistics are not enabled or have not yet been collected.

**Return value:** A `CacheStatistics` object or `null`.  
**Throws:** None.

### Dictionary<string, object> CustomMetrics
Gets or sets a dictionary of user‑defined metric key‑value pairs that are included with each snapshot. The dictionary is mutable; changes affect subsequently collected snapshots.

**Return value:** A `Dictionary<string, object>` containing custom metric entries.  
**Throws:** None.

### int InitialDelayMs
Gets or sets the delay in milliseconds before the first metrics collection occurs after the worker starts. The default is implementation‑specific.

**Return value:** The initial delay in milliseconds.  
**Throws:** None.

### int CollectionIntervalMs
Gets or sets the interval in milliseconds between successive metrics collection cycles. Changing this value affects the timing of future collections only.

**Return value:** The collection interval in milliseconds.  
**Throws:** None.

### int SnapshotRetentionSeconds
Gets or sets the number of seconds that a collected snapshot is retained before being automatically discarded. Snapshots older than this threshold are removed from the internal list.

**Return value:** The retention period in seconds.  
**Throws:** None.

### bool VerboseLogging
Gets or sets a flag indicating whether the worker should emit detailed log messages for each collection cycle. When `true`, additional diagnostic information is written to the configured logger.

**Return value:** `true` if verbose logging is enabled; otherwise `false`.  
**Throws:** None.

## Usage

### Starting the worker and accessing the latest snapshot
```csharp
using Microsoft.Extensions.Hosting;
using DotnetDistributedLock.Metrics;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddHostedService<MetricsCollectionWorker>();
    })
    .Build();

await host.StartAsync();

// After the worker has run at least once:
var worker = host.Services.GetRequiredService<MetricsCollectionWorker>();
MetricsSnapshot? latest = worker.GetCurrentSnapshot();

if (latest != null)
{
    Console.WriteLine($"Last collection at {worker.Timestamp:O}");
    Console.WriteLine($"Lock acquisitions: {latest.AcquisitionCount}");
}
```

### Configuring collection interval and retrieving averaged metrics
```csharp
var worker = new MetricsCollectionWorker
{
    InitialDelayMs = 5_000,          // start collecting after 5 seconds
    CollectionIntervalMs = 15_000,   // collect every 15 seconds
    SnapshotRetentionSeconds = 60,   // keep snapshots for 1 minute
    VerboseLogging = true
};

await worker.StartAsync(CancellationToken.None);

// Let.None);

// Simulate some work...
await Task.Delay(TimeSpan.FromSeconds(40));

MetricsSnapshot? average = worker.GetAverageMetrics();
if (average != null)
{
    Console.WriteLine($"Average lock wait time (ms): {average.AverageWaitMs}");
}

await worker.StopAsync(CancellationToken.None);
```

## Notes
- The worker is thread‑safe for concurrent calls to `GetCurrentSnapshot`, `GetSnapshots`, and `GetAverageMetrics`; internal collections are accessed via locks that do not block the collection timer.
- Modifying `InitialDelayMs`, `CollectionIntervalMs`, `SnapshotRetentionSeconds`, or `VerboseLogging` while the worker is running takes effect on the next collection cycle; changes do not affect already‑taken snapshots.
- If `SnapshotRetentionSeconds` is set to zero, snapshots are discarded immediately after being read, causing `GetSnapshots` to always return an empty list.
- The `StopAsync` method will not cancel an in‑progress metrics collection; it waits for the current cycle to finish unless the supplied `CancellationToken` is triggered, in which case the method may return early while the collection task continues to run in the background until it naturally completes.
- Custom metrics stored in `CustomMetrics` are shallow‑copied into each snapshot; subsequent modifications to the dictionary do not alter already‑captured snapshots.
