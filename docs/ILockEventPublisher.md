# ILockEventPublisher

Provides a publish-subscribe mechanism for distributed lock lifecycle events, enabling observers to react to lock acquisition, release, renewal, and failure notifications across a distributed system. The abstraction decouples lock infrastructure from diagnostic, metrics, or business logic handlers.

## API

### InMemoryLockEventPublisher

```csharp
public InMemoryLockEventPublisher()
```

Initializes a new instance of the in-memory event publisher. The publisher maintains an internal subscriber registry per event type.

**Throws**  
- `InvalidOperationException` — If the internal dispatcher fails to initialize.

---

### PublishAsync<TEvent> (Instance, Async)

```csharp
public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
    where TEvent : notnull
```

Asynchronously delivers the specified event to all registered subscribers for `TEvent`. Subscribers are invoked sequentially; exceptions from individual subscribers are captured and aggregated.

**Parameters**  
- `@event` — The event instance to publish.  
- `cancellationToken` — Token to cancel the publish operation.

**Returns**  
A `Task` that completes when all subscribers have processed the event.

**Throws**  
- `OperationCanceledException` — If `cancellationToken` is triggered before completion.  
- `AggregateException` — If one or more subscribers throw; contains all collected exceptions.

---

### PublishAsync<TEvent> (Instance, Non-Async Signature)

```csharp
public Task PublishAsync<TEvent>(TEvent @event)
    where TEvent : notnull
```

Synchronously initiates publication of the specified event to all registered subscribers for `TEvent`. Equivalent to calling the async overload with `CancellationToken.None`.

**Parameters**  
- `@event` — The event instance to publish.

**Returns**  
A `Task` representing the asynchronous publish operation.

**Throws**  
- `AggregateException` — If one or more subscribers throw; contains all collected exceptions.

---

### Subscribe<TEvent>

```csharp
public void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
    where TEvent : notnull
```

Registers an asynchronous handler for events of type `TEvent`. The handler receives the event and a cancellation token linked to the publish operation.

**Parameters**  
- `handler` — Async delegate invoked for each published `TEvent`.

**Throws**  
- `ArgumentNullException` — If `handler` is `null`.  
- `InvalidOperationException` — If the subscriber registry is in a disposed state.

---

### Unsubscribe<TEvent>

```csharp
public void Unsubscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
    where TEvent : notnull
```

Removes a previously registered handler for `TEvent`. The handler instance must match the one provided to `Subscribe`.

**Parameters**  
- `handler` — The exact delegate instance to remove.

**Throws**  
- `ArgumentNullException` — If `handler` is `null`.  
- `InvalidOperationException` — If the subscriber registry is in a disposed state.

---

### GetSubscriberCount<TEvent>

```csharp
public int GetSubscriberCount<TEvent>()
    where TEvent : notnull
```

Returns the current number of registered handlers for `TEvent`.

**Returns**  
The count of active subscribers; zero if none are registered.

**Throws**  
- `InvalidOperationException` — If the subscriber registry is in a disposed state.

---

### Publish<TEvent> (Static)

```csharp
public static void Publish<TEvent>(TEvent @event)
    where TEvent : notnull
```

Synchronously publishes an event to the global default publisher instance (resolved via `IServiceProvider` at first call). Blocks until all subscribers complete.

**Parameters**  
- `@event` — The event instance to publish.

**Throws**  
- `InvalidOperationException` — If no default publisher has been configured via `AddLockEventPublisher`.  
- `AggregateException` — If one or more subscribers throw.

---

### PublishFireAndForget<TEvent> (Static)

```csharp
public static void PublishFireAndForget<TEvent>(TEvent @event)
    where TEvent : notnull
```

Enqueues an event for asynchronous publication on the global default publisher without awaiting subscriber completion. Exceptions in subscribers are logged but not propagated.

**Parameters**  
- `@event` — The event instance to publish.

**Throws**  
- `InvalidOperationException` — If no default publisher has been configured via `AddLockEventPublisher`.

---

### AddLockEventPublisher (Static)

```csharp
public static IServiceCollection AddLockEventPublisher(this IServiceCollection services, Action<LockEventPublisherOptions>? configure = null)
```

Registers the in-memory `ILockEventPublisher` implementation and configures the global default publisher for static `Publish`/`PublishFireAndForget` methods.

**Parameters**  
- `services` — The service collection to augment.  
- `configure` — Optional callback to customize publisher options (e.g., error handling policy, dispatcher concurrency).

**Returns**  
The same `IServiceCollection` for chaining.

**Throws**  
- `ArgumentNullException` — If `services` is `null`.

## Usage

### Example 1: Dependency Injection and Event Handling

```csharp
// Program.cs / Startup.cs
services.AddLockEventPublisher(options =>
{
    options.SubscriberErrorPolicy = SubscriberErrorPolicy.ContinueOnError;
    options.MaxConcurrentSubscribers = Environment.ProcessorCount;
});

// Consumer component
public sealed class LockMetricsCollector : IHostedService
{
    private readonly ILockEventPublisher _publisher;
    private readonly Meter _meter = new("DistributedLock.Metrics");
    private readonly Counter<long> _acquired;
    private readonly Counter<long> _released;

    public LockMetricsCollector(ILockEventPublisher publisher)
    {
        _publisher = publisher;
        _acquired = _meter.CreateCounter<long>("lock.acquired");
        _released = _meter.CreateCounter<long>("lock.released");
    }

    public Task StartAsync(CancellationToken ct)
    {
        _publisher.Subscribe<LockAcquiredEvent>(async (evt, token) =>
        {
            _acquired.Add(1, new KeyValuePair<string, object?>("resource", evt.ResourceId));
            await Task.CompletedTask;
        });

        _publisher.Subscribe<LockReleasedEvent>(async (evt, token) =>
        {
            _released.Add(1, new KeyValuePair<string, object?>("resource", evt.ResourceId));
            await Task.CompletedTask;
        });

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

### Example 2: Fire-and-Forget Telemetry from Synchronous Code

```csharp
public sealed class OrderService
{
    private readonly IDistributedLock _lock;

    public OrderService(IDistributedLock distributedLock)
    {
        _lock = distributedLock;
    }

    public async Task ProcessOrderAsync(Order order)
    {
        var lockHandle = await _lock.TryAcquireAsync($"order-{order.Id}", TimeSpan.FromSeconds(30));
        if (lockHandle is null)
        {
            // Static fire-and-forget: no await, no exception propagation
            ILockEventPublisher.PublishFireAndForget(new LockAcquisitionFailedEvent
            {
                ResourceId = $"order-{order.Id}",
                Reason = "Timeout"
            });
            throw new ConcurrencyException("Could not acquire order lock");
        }

        try
        {
            await FulfillOrderAsync(order);
        }
        finally
        {
            await lockHandle.ReleaseAsync();
        }
    }
}
```

## Notes

- **Thread Safety**: All instance methods (`Subscribe`, `Unsubscribe`, `PublishAsync`, `GetSubscriberCount`) are thread-safe. The internal subscriber registry uses lock-free reads and copy-on-write semantics for writes.
- **Subscriber Exceptions**: By default, exceptions from subscribers are aggregated into an `AggregateException` thrown by `PublishAsync`. Configure `SubscriberErrorPolicy.ContinueOnError` to suppress propagation and log instead.
- **Static Publisher Lifetime**: The static `Publish`/`PublishFireAndForget` methods resolve the default publisher from a lazily-initialized `IServiceProvider`. Ensure `AddLockEventPublisher` is called during application startup before any static publish occurs.
- **Fire-and-Forget Guarantees**: `PublishFireAndForget` enqueues work to the thread pool. If the process terminates before subscribers run, events are lost. Use `PublishAsync` for critical-path notifications.
- **Generic Constraints**: All generic methods require `TEvent : notnull` (reference types or non-nullable value types). Nullable value types (`TEvent?`) are not accepted.
- **Disposal**: `InMemoryLockEventPublisher` implements `IAsyncDisposable`. Call `DisposeAsync` to clear subscribers and release resources; subsequent calls to instance methods throw `InvalidOperationException`.
- **Duplicate Subscriptions**: Registering the same delegate instance twice via `Subscribe` results in two distinct entries; both will be invoked. Use `Unsubscribe` with the exact delegate to remove one registration.
