#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Events;

/// <summary>
/// Publisher for distributing lock events to registered subscribers.
/// Implements a simple pub-sub pattern for decoupled event handling.
/// Supports both synchronous and asynchronous event delivery.
/// </summary>
public interface ILockEventPublisher
{
    Task PublishAsync<TEvent>(TEvent @event) where TEvent : LockEvent;
    void Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : LockEvent;
    void Unsubscribe<TEvent>() where TEvent : LockEvent;
}

/// <summary>
/// In-memory event publisher using delegates.
/// Suitable for single-instance deployments. For distributed systems,
/// consider using a message broker (RabbitMQ, Kafka, etc).
/// </summary>
public sealed class InMemoryLockEventPublisher : ILockEventPublisher
{
    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();
    private readonly ILogger<InMemoryLockEventPublisher> _logger;
    private readonly object _lockObject = new();

    public InMemoryLockEventPublisher(ILogger<InMemoryLockEventPublisher> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : LockEvent
    {
        var eventType = typeof(TEvent);

        List<Delegate> handlers;
        lock (_lockObject)
        {
            if (!_subscribers.TryGetValue(eventType, out var subs))
                return; // No subscribers for this event type

            handlers = new List<Delegate>(subs);
        }

        _logger.LogDebug("Publishing event: {EventType} [ID: {EventId}]", eventType.Name, @event.EventId);

        var tasks = new List<Task>();

        foreach (var handler in handlers)
        {
            try
            {
                if (handler is Func<TEvent, Task> asyncHandler)
                {
                    tasks.Add(asyncHandler(@event));
                }
                else
                {
                    _logger.LogWarning("Invalid handler type for event: {EventType}", eventType.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in event handler for {EventType}", eventType.Name);
            }
        }

        // Wait for all handlers to complete
        if (tasks.Any())
        {
            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error waiting for event handlers");
            }
        }

        _logger.LogDebug("Event published successfully: {EventType}", eventType.Name);
    }

    public void Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : LockEvent
    {
        if (handler is null)
            throw new ArgumentNullException(nameof(handler));

        var eventType = typeof(TEvent);

        lock (_lockObject)
        {
            if (!_subscribers.TryGetValue(eventType, out var handlers))
            {
                handlers = new List<Delegate>();
                _subscribers[eventType] = handlers;
            }

            handlers.Add(handler);
        }

        _logger.LogDebug("Handler subscribed for event type: {EventType}", eventType.Name);
    }

    public void Unsubscribe<TEvent>() where TEvent : LockEvent
    {
        var eventType = typeof(TEvent);

        lock (_lockObject)
        {
            if (_subscribers.TryGetValue(eventType, out var handlers))
            {
                handlers.Clear();
            }
        }

        _logger.LogDebug("All handlers unsubscribed for event type: {EventType}", eventType.Name);
    }

    /// <summary>
    /// Gets subscriber count for a specific event type.
    /// Useful for diagnostics and testing.
    /// </summary>
    public int GetSubscriberCount<TEvent>() where TEvent : LockEvent
    {
        var eventType = typeof(TEvent);

        lock (_lockObject)
        {
            return _subscribers.TryGetValue(eventType, out var handlers) ? handlers.Count : 0;
        }
    }
}

/// <summary>
/// No-op implementation for when events are disabled or in testing.
/// </summary>
public sealed class NoOpLockEventPublisher : ILockEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent @event) where TEvent : LockEvent
        => Task.CompletedTask;

    public void Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : LockEvent
    {
        // No-op
    }

    public void Unsubscribe<TEvent>() where TEvent : LockEvent
    {
        // No-op
    }
}

/// <summary>
/// Extension methods for event publishing.
/// </summary>
public static class LockEventPublisherExtensions
{
    /// <summary>
    /// Publishes an event synchronously (waits for completion).
    /// </summary>
    public static void Publish<TEvent>(
        this ILockEventPublisher publisher,
        TEvent @event) where TEvent : LockEvent
    {
        publisher.PublishAsync(@event).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Fire-and-forget event publishing (no waiting).
    /// </summary>
    public static void PublishFireAndForget<TEvent>(
        this ILockEventPublisher publisher,
        TEvent @event) where TEvent : LockEvent
    {
        _ = publisher.PublishAsync(@event);
    }

    /// <summary>
    /// Registers service collection extension for dependency injection.
    /// </summary>
    public static IServiceCollection AddLockEventPublisher(this IServiceCollection services)
    {
        services.AddSingleton<ILockEventPublisher, InMemoryLockEventPublisher>();
        return services;
    }
}
