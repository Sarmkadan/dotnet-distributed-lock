# ILockEventBus

`ILockEventBus` is an interface for managing event-based communication in distributed locking scenarios. It enables publishing and subscribing to lock-related events (e.g., lock acquired, lock released) in a decoupled manner, supporting both synchronous and asynchronous event handling. Implementations typically provide in-memory or distributed event bus functionality for coordinating lock state changes across components or services.

## API

### `InMemoryLockEventBus`
A concrete implementation of `ILockEventBus` that stores events and subscriptions in memory. Suitable for single-instance applications or testing. Events are not persisted across application restarts.

---

### `Task PublishAsync<TEvent>(TEvent @event)`
Publishes an event to all subscribers asynchronously.  
**Parameters**:  
- `event`: The event instance to publish.  
**Returns**: A `Task` representing the asynchronous operation.  
**Exceptions**:  
- `ArgumentNullException`: Thrown when `@event` is null.  

---

### `void Subscribe<TEvent>(Action<TEvent> handler)`
Registers a synchronous handler for events of type `TEvent`.  
**Parameters**:  
- `handler`: The action to invoke when an event is published.  
**Exceptions**:  
- `ArgumentNullException`: Thrown when `handler` is null.  

---

### `void Subscribe<TEvent>(Func<TEvent, Task> handler)`
Registers an asynchronous handler for events of type `TEvent`.  
**Parameters**:  
- `handler`: The async function to invoke when an event is published.  
**Exceptions**:  
- `ArgumentNullException`: Thrown when `handler` is null.  

---

### `Task<IDisposable> SubscribeAsync<TEvent>(Func<TEvent, Task> handler)`
Registers an asynchronous handler and returns an `IDisposable` for unsubscribing.  
**Parameters**:  
- `handler`: The async function to invoke when an event is published.  
**Returns**: An `IDisposable` that removes the subscription when disposed.  
**Exceptions**:  
- `ArgumentNullException`: Thrown when `handler` is null.  

---

### `int GetSubscriberCount<TEvent>()`
Returns the number of active subscribers for events of type `TEvent`.  
**Returns**: The count of subscribers.  

---

### `List<TEvent> GetEventHistory<TEvent>(int count)`
Retrieves the most recent `count` events of type `TEvent`.  
**Parameters**:  
- `count`: The maximum number of events to retrieve.  
**Returns**: A list of historical events.  

---

### `List<TEvent> GetEventHistory<TEvent>(TimeSpan timespan)`
Retrieves events of type `TEvent` that occurred within the specified `timespan`.  
**Parameters**:  
- `timespan`: The time window for retrieving events.  
**Returns**: A list of historical events.  

---

### `List<LockEvent> GetEventsByCorrelation(string correlationId)`
Retrieves all events associated with a specific correlation ID.  
**Parameters**:  
- `correlationId`: The identifier for correlating related events.  
**Returns**: A list of `LockEvent` instances.  

---

### `EventSubscription`
Represents a subscription to events. Used to manage or inspect subscription details.  

---

### `void Dispose()`
Releases resources used by the event bus. Should be called when the bus is no longer needed.  

---

### `static void Publish<TEvent>(TEvent @event)`
Publishes an event synchronously using a static context.  
**Parameters**:  
- `event`: The event instance to publish.  
**Exceptions**:  
- `ArgumentNullException`: Thrown when `@event` is null.  

---

### `static void PublishFireAndForget<TEvent>(TEvent @event)`
Publishes an event without waiting for subscribers to process it.  
**Parameters**:  
- `event`: The event instance to publish.  
**Exceptions**:  
- `ArgumentNullException`: Thrown when `@event` is null.  

---

### `static void AddLockEventBus(this IServiceCollection services)`
Registers `ILockEventBus` and its dependencies in the DI container.  
**Parameters**:  
- `services`: The `IServiceCollection` to configure.  

## Usage

### Example 1: Publishing and Subscribing to Lock Events
```csharp
public class LockService
{
    private readonly ILockEventBus _eventBus;

    public LockService(ILockEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public async Task AcquireLockAsync(string resourceId)
    {
        // Simulate lock acquisition logic
        var evt = new LockAcquiredEvent(resourceId, DateTime.UtcNow);
        await _eventBus.PublishAsync(evt);
    }
}

public class LockEventHandler
{
    public void Handle(LockAcquiredEvent evt)
    {
        Console.WriteLine($"Lock acquired for {evt.ResourceId}");
    }

    public async Task HandleAsync(LockReleasedEvent evt)
    {
        await Task.Delay(100); // Simulate async work
        Console.WriteLine($"Lock released for {evt.ResourceId}");
    }
}

// Registration in Startup.cs
services.AddLockEventBus();
services.AddSingleton<LockEventHandler>();
services.AddSingleton<LockService>();
```

### Example 2: Retrieving Event History
```csharp
public class LockMonitor
{
    private readonly ILockEventBus _eventBus;

    public LockMonitor(ILockEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public List<LockAcquiredEvent> GetRecentEvents(int count)
    {
        return _eventBus.GetEventHistory<LockAcquiredEvent>(count);
    }

    public List<LockEvent> GetCorrelatedEvents(string correlationId)
    {
        return _eventBus.GetEventsByCorrelation(correlationId);
    }
}
```

## Notes

- **Thread Safety**: Implementations of `ILockEventBus` are expected to be thread-safe. Concurrent calls to `PublishAsync`, `Subscribe`, and `GetEventHistory` should not cause race conditions.
- **Event History Retention**: The in-memory implementation retains events indefinitely unless explicitly configured otherwise. Production implementations may require bounded retention policies.
- **Static Methods**: `Publish` and `PublishFireAndForget` rely on a static default instance. Avoid using them in scenarios requiring explicit lifecycle management.
- **Subscription Management**: Subscribers registered via `SubscribeAsync` must dispose of their subscriptions to prevent memory leaks. The `InMemoryLockEventBus` does not automatically clean up stale subscriptions.
- **Correlation IDs**: Use `GetEventsByCorrelation` to trace event sequences in distributed workflows. Correlation IDs should be consistently applied across related lock operations.
