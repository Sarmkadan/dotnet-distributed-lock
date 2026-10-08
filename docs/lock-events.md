# Lock Event System

The event system lets the library announce what happens to locks (acquired, released, expired, renewed, failed, contended, errored) and lets other code react to those announcements without coupling to the lock implementation. It consists of four parts:

| Type | Role | Source |
|---|---|---|
| `LockEventBus` (`ILockEventBus`, `InMemoryLockEventBus`) | Publish/subscribe bus with an in-memory event history | `src/Events/LockEventBus.cs` |
| `LockEventPublisher` (`ILockEventPublisher`, `InMemoryLockEventPublisher`, `NoOpLockEventPublisher`) | Simpler delegate-based publisher, plus a no-op variant | `src/Events/LockEventPublisher.cs` |
| `LockEventSubscriber` (abstract base, `LoggingLockEventSubscriber`, `MetricsTrackingEventSubscriber`) | Base class for components that register handlers on a publisher | `src/Events/LockEventSubscriber.cs` |
| `LockEvents` (`LockEvent` base and concrete event records) | The event payload types | `src/Events/LockEvents.cs` |

Helper extensions live in `src/Events/LockEventExtensions.cs` (`LockEventExtensions`) and `src/Events/LockEventSubscriberValidation.cs` (`LockEventSubscriberValidation`).

## Event Types (`LockEvents`)

All events derive from the abstract `LockEvent`.

### Base class `LockEvent`

*   `EventId` (`string`): GUID generated when the instance is created. Read-only.
*   `OccurredAt` (`DateTime`): UTC timestamp, defaults to `DateTime.UtcNow`.
*   `SourceSystem` (`string?`): Origin of the event. The bus sets it to `"InMemoryBus"` if it is still null at publish time.
*   `CorrelationId` (`string?`): Correlation identifier. The bus copies the `correlationId` argument of `PublishAsync` into this property if it is still null.

### Concrete events

| Event | Raised when | Key properties |
|---|---|---|
| `LockAcquiredEvent` | A lock is acquired | `LockId`, `LockName`, `OwnerId`, `ExpiresAt`, `FencingToken` (`ulong`), `Duration`, `Status` (default `LockStatus.Held`) |
| `LockReleasedEvent` | A lock is released before expiry | `LockId`, `LockName`, `OwnerId`, `AcquiredAt`, `ReleasedAt`, `HeldDuration` |
| `LockExpiredEvent` | A lock expires without release | `LockId`, `LockName`, `OwnerId`, `ExpiredAt`, `TotalDuration` |
| `LockRenewedEvent` | The owner renews a lock | `LockId`, `LockName`, `OwnerId`, `PreviousExpiresAt`, `NewExpiresAt`, `RenewedDuration` |
| `LockFailedEvent` | A lock operation fails | `LockId`, `OwnerId`, `Reason` |
| `LockAcquisitionFailedEvent` | An acquisition attempt fails | `LockName`, `RequesterId`, `Reason`, `DetailedError?` |
| `LockContentionEvent` | Several parties compete for the same lock | `LockName`, `ContentionLevel`, `CompetingParties` (`List<string>`), `ContentionDetectedAt` |
| `LockPerformanceEvent` | An operation exceeds a time threshold | `LockId`, `OperationType`, `DurationMs`, `ThresholdMs`, computed `IsSlowerThanThreshold` |
| `LockErrorEvent` | An operation raises an error | `LockId`, `OperationType`, `ErrorMessage`, `Exception?` |

Properties marked `required` in the source must be set in an object initializer (or through the constructors that carry `[SetsRequiredMembers]`).

Helper methods on `LockEventExtensions` operate on any `LockEvent`:

*   `IsAcquisitionSuccessful()`: true for a `LockAcquiredEvent` with `Status == LockStatus.Acquired`.
*   `IsFailure()`: true for `LockFailedEvent` or `LockAcquisitionFailedEvent`.
*   `GetLockId()`, `GetOwnerId()`, `GetFencingToken()`, `GetExpirationTime()`, `GetDuration()`: read common fields across event types.
*   `IsRelatedToLock(lockName)`, `IsWithinTimeRange(start, end)`: filtering helpers.
*   `ToLogString(includeTimestamp)`: formats the event for logs, including `SourceSystem` and `CorrelationId` when set.
*   `ToFailureEvent(reason)`: converts an event into a `LockFailedEvent`.

## LockEventBus

### Interface `ILockEventBus`

*   `Task PublishAsync<TEvent>(TEvent @event, string? correlationId = null)`
*   `void Subscribe<TEvent>(Action<TEvent> handler)`
*   `void Subscribe<TEvent>(Func<TEvent, Task> handler)`
*   `Task<IDisposable> SubscribeAsync<TEvent>(Func<TEvent, Task> handler)`
*   `int GetSubscriberCount<TEvent>()`

### `InMemoryLockEventBus`

Constructor: `InMemoryLockEventBus(ILogger<InMemoryLockEventBus> logger, int maxHistorySize = 10000)`.

Behavior of `PublishAsync`:

1.  Sets `CorrelationId` and `SourceSystem` if they are null.
2.  Adds the event to an in-memory history. When the history exceeds `maxHistorySize`, the oldest entries are removed.
3.  Looks up the handlers for the exact runtime type `TEvent`. If none exist, it logs at Debug level and returns.
4.  Invokes each handler. `Func<TEvent, Task>` handlers are collected and awaited together with `Task.WhenAll`. `Action<TEvent>` handlers run synchronously. An exception thrown by a handler is logged as an error and does not stop the other handlers.

Other members:

*   `GetEventHistory<TEvent>()` and `GetEventHistory<TEvent>(predicate)` return matching events from history.
*   `GetEventsByCorrelation(correlationId)` returns all history entries with that `CorrelationId`.
*   `SubscribeAsync` registers the handler and returns an `IDisposable`. **Disposing it does not unsubscribe the handler.** The internal `EventSubscription.Dispose` is empty because `ConcurrentBag` does not support removal. Treat the returned object as a no-op until this is changed.

### Extensions (`LockEventBusExtensions`)

*   `Publish(this ILockEventBus, event)`: synchronous wrapper over `PublishAsync` (blocks with `GetAwaiter().GetResult()`).
*   `PublishFireAndForget(this ILockEventBus, event)`: starts `PublishAsync` and discards the task. Exceptions from the task are not observed.
*   `AddLockEventBus(this IServiceCollection, int maxHistorySize = 10000)`: registers `ILockEventBus` as a singleton.

## LockEventPublisher

### Interface `ILockEventPublisher`

*   `Task PublishAsync<TEvent>(TEvent @event)`
*   `void Subscribe<TEvent>(Func<TEvent, Task> handler)`
*   `void Unsubscribe<TEvent>()`

### `InMemoryLockEventPublisher`

Constructor: `InMemoryLockEventPublisher(ILogger<InMemoryLockEventPublisher> logger)`.

*   Handlers are stored per event type in a `Dictionary<Type, List<Delegate>>` guarded by a lock.
*   `PublishAsync` copies the handler list under the lock, invokes every handler, and awaits all of them with `Task.WhenAll`. Errors are logged. Any exception from `Task.WhenAll` is also logged and not rethrown.
*   Only `Func<TEvent, Task>` handlers are invoked. A handler of another delegate type is logged as a warning and skipped.
*   `Unsubscribe<TEvent>()` clears all handlers for that event type. It cannot remove a single handler.
*   `GetSubscriberCount<TEvent>()` is a public diagnostic method on the class. It is not part of `ILockEventPublisher`.

Unlike `InMemoryLockEventBus`, this publisher does not store history and does not set `SourceSystem` or `CorrelationId`.

### `NoOpLockEventPublisher`

Discards every publish and ignores subscriptions. Use it when events are disabled or in tests that do not care about events.

### Extensions (`LockEventPublisherExtensions`)

*   `Publish(this ILockEventPublisher, event)`: synchronous wrapper.
*   `PublishFireAndForget(this ILockEventPublisher, event)`: discards the task.
*   `AddLockEventPublisher(this IServiceCollection)`: registers `ILockEventPublisher` as `InMemoryLockEventPublisher` (singleton).

## LockEventSubscriber

`LockEventSubscriber` is an abstract base class. A subclass implements:

```csharp
public abstract Task RegisterAsync(ILockEventPublisher publisher);
```

The base class stores an `ILogger` and provides `HandleEventAsync<TEvent>(event, handler)`, which runs a handler and logs any exception instead of propagating it.

Provided implementations:

*   `LoggingLockEventSubscriber`: subscribes to acquired, released, expired, renewed, acquisition-failed, contention and error events and writes one log line per event. Log level is Information for success events, Warning for acquisition failure and contention, and Error for errors.
*   `MetricsTrackingEventSubscriber`: subscribes to acquired, released, acquisition-failed and contention events. It keeps process-wide counters (`Interlocked`) and writes per-lock records to an `IMetricsStore`. If no store is passed, it uses a private `InMemoryMetricsStore`. `GetMetrics()` returns an `EventMetrics` snapshot (`Acquisitions`, `Releases`, `Failures`, `ContentionEvents`, `Timestamp`).

Registration helpers (`LockEventSubscriberExtensions`):

*   `AddLockEventSubscribers(this IServiceCollection)`: registers `LoggingLockEventSubscriber` and `MetricsTrackingEventSubscriber` as singletons.
*   `InitializeLockEventSubscribersAsync(this IServiceProvider)`: resolves `ILockEventPublisher` and calls `RegisterAsync` on each subscriber that is registered. Call it once at startup.

Validation helpers (`LockEventSubscriberValidation`) provide `Validate`, `IsValid`, and `EnsureValid` overloads for subscribers, `MetricsTrackingEventSubscriber`, and `EventMetrics`.

## Usage

### Subscribing with the bus

```csharp
var bus = serviceProvider.GetRequiredService<ILockEventBus>();

bus.Subscribe<LockAcquiredEvent>(e =>
{
    Console.WriteLine($"{e.LockName} acquired by {e.OwnerId}");
});

await bus.PublishAsync(new LockAcquiredEvent
{
    LockId = "orders:42",
    LockName = "orders:42",
    OwnerId = "worker-1",
    ExpiresAt = DateTime.UtcNow.AddSeconds(30),
    FencingToken = 7
}, correlationId: "req-123");
```

### Writing a custom subscriber

```csharp
public sealed class AlertingSubscriber : LockEventSubscriber
{
    public AlertingSubscriber(ILogger<AlertingSubscriber> logger) : base(logger) { }

    public override Task RegisterAsync(ILockEventPublisher publisher)
    {
        publisher.Subscribe<LockErrorEvent>(e => HandleEventAsync(e, OnError));
        return Task.CompletedTask;
    }

    private Task OnError(LockErrorEvent e)
    {
        // Forward to an alerting system here.
        return Task.CompletedTask;
    }
}
```

## Notes and Limitations

*   The library registers `ILockEventBus` (`InMemoryLockEventBus`) in `ServiceCollectionExtensions`. `ILockEventPublisher` is not registered by the core setup. Call `AddLockEventPublisher()` (or `AddLockEventSubscribers()` together with a publisher registration) if you use the publisher path.
*   Both implementations are in-process only. For multiple application instances, forward events to a message broker yourself.
*   `InMemoryLockEventBus` keeps up to `maxHistorySize` events in memory. Under heavy load this history grows to that limit and is not persisted.
*   `SubscribeAsync` returns a disposable that does not unsubscribe (see above).
*   `PublishFireAndForget` does not observe exceptions.
*   Handler order is not guaranteed. Handlers run concurrently for `Func<TEvent, Task>` registrations.
