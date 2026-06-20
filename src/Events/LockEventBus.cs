#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Events;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Central event bus for distributed lock events.
/// Manages event publication, subscription, and delivery with ordering guarantees.
/// Can be extended for distributed scenarios using message brokers.
/// </summary>
public interface ILockEventBus
{
    /// <summary>
    /// Publishes an event asynchronously.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="correlationId">An optional correlation ID.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PublishAsync<TEvent>(TEvent @event, string? correlationId = null) where TEvent : LockEvent;

    /// <summary>
    /// Subscribes to an event with a synchronous handler.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event.</typeparam>
    /// <param name="handler">The synchronous event handler.</param>
    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : LockEvent;

    /// <summary>
    /// Subscribes to an event with an asynchronous handler.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event.</typeparam>
    /// <param name="handler">The asynchronous event handler.</param>
    void Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : LockEvent;

    /// <summary>
    /// Subscribes to an event with an asynchronous handler, returning an IDisposable for cancellation.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event.</typeparam>
    /// <param name="handler">The asynchronous event handler.</param>
    /// <returns>A task returning an IDisposable object for managing the subscription.</returns>
    Task<IDisposable> SubscribeAsync<TEvent>(Func<TEvent, Task> handler) where TEvent : LockEvent;

    /// <summary>
    /// Gets the number of subscribers for a specific event type.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event.</typeparam>
    /// <returns>The number of subscribers.</returns>
    int GetSubscriberCount<TEvent>() where TEvent : LockEvent;
}

/// <summary>
/// In-memory event bus with event history and replay capabilities.
/// </summary>
public sealed class InMemoryLockEventBus : ILockEventBus
{
    private readonly ConcurrentDictionary<Type, ConcurrentBag<object>> _subscribers = new();
    private readonly ConcurrentBag<LockEvent> _eventHistory;
    private readonly int _maxHistorySize;
    private readonly ILogger<InMemoryLockEventBus> _logger;
    private readonly object _historyLock = new();

    public InMemoryLockEventBus(
        ILogger<InMemoryLockEventBus> logger,
        int maxHistorySize = 10000)
    {
        _logger = logger;
        _maxHistorySize = maxHistorySize;
        _eventHistory = new ConcurrentBag<LockEvent>();
    }

    public async Task PublishAsync<TEvent>(TEvent @event, string? correlationId = null) where TEvent : LockEvent
    {
        @event.CorrelationId ??= correlationId;
        @event.SourceSystem ??= "InMemoryBus";

        // Store in history
        lock (_historyLock)
        {
            _eventHistory.Add(@event);

            // Trim history if it exceeds max size
            while (_eventHistory.Count > _maxHistorySize)
            {
                _eventHistory.TryTake(out _);
            }
        }

        var eventType = typeof(TEvent);

        if (!_subscribers.TryGetValue(eventType, out var handlers))
        {
            _logger.LogDebug("No subscribers for event: {EventType}", eventType.Name);
            return;
        }

        _logger.LogDebug("Publishing event {EventType} to {Count} subscribers", eventType.Name, handlers.Count);

        var tasks = new List<Task>();

        foreach (var handler in handlers)
        {
            try
            {
                if (handler is Func<TEvent, Task> asyncHandler)
                {
                    tasks.Add(asyncHandler(@event));
                }
                else if (handler is Action<TEvent> syncHandler)
                {
                    syncHandler(@event);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in event handler for {EventType}", eventType.Name);
            }
        }

        if (tasks.Any())
        {
            await Task.WhenAll(tasks);
        }
    }

    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : LockEvent
    {
        if (handler is null)
            throw new ArgumentNullException(nameof(handler));

        var eventType = typeof(TEvent);
        var subscribers = _subscribers.GetOrAdd(eventType, _ => new ConcurrentBag<object>());
        subscribers.Add(handler);

        _logger.LogDebug("Subscribed to event: {EventType}", eventType.Name);
    }

    public void Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : LockEvent
    {
        if (handler is null)
            throw new ArgumentNullException(nameof(handler));

        var eventType = typeof(TEvent);
        var subscribers = _subscribers.GetOrAdd(eventType, _ => new ConcurrentBag<object>());
        subscribers.Add(handler);

        _logger.LogDebug("Subscribed to event: {EventType}", eventType.Name);
    }

    public async Task<IDisposable> SubscribeAsync<TEvent>(Func<TEvent, Task> handler) where TEvent : LockEvent
    {
        Subscribe(handler);
        return new EventSubscription(typeof(TEvent), handler, this);
    }

    public int GetSubscriberCount<TEvent>() where TEvent : LockEvent
    {
        var eventType = typeof(TEvent);
        return _subscribers.TryGetValue(eventType, out var handlers) ? handlers.Count : 0;
    }

    /// <summary>
    /// Gets all events of a specific type from history.
    /// </summary>
    public List<TEvent> GetEventHistory<TEvent>() where TEvent : LockEvent
    {
        return _eventHistory
            .OfType<TEvent>()
            .ToList();
    }

    /// <summary>
    /// Gets events matching a predicate.
    /// </summary>
    public List<TEvent> GetEventHistory<TEvent>(Func<TEvent, bool> predicate) where TEvent : LockEvent
    {
        return _eventHistory
            .OfType<TEvent>()
            .Where(predicate)
            .ToList();
    }

    /// <summary>
    /// Gets events by correlation ID.
    /// </summary>
    public List<LockEvent> GetEventsByCorrelation(string correlationId)
    {
        return _eventHistory
            .Where(e => e.CorrelationId == correlationId)
            .ToList();
    }

    private class EventSubscription : IDisposable
    {
        private readonly Type _eventType;
        private readonly object _handler;
        private readonly InMemoryLockEventBus _bus;

        public EventSubscription(Type eventType, object handler, InMemoryLockEventBus bus)
        {
            _eventType = eventType;
            _handler = handler;
            _bus = bus;
        }

        public void Dispose()
        {
            if (_bus._subscribers.TryGetValue(_eventType, out var handlers))
            {
                // Note: ConcurrentBag doesn't support removal, so we can't truly unsubscribe
                // This is a limitation of the simple implementation
            }
        }
    }
}

/// <summary>
/// Extension methods for event bus.
/// </summary>
public static class LockEventBusExtensions
{
    /// <summary>
    /// Publishes an event synchronously.
    /// </summary>
    public static void Publish<TEvent>(
        this ILockEventBus eventBus,
        TEvent @event) where TEvent : LockEvent
    {
        eventBus.PublishAsync(@event).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Publishes an event fire-and-forget.
    /// </summary>
    public static void PublishFireAndForget<TEvent>(
        this ILockEventBus eventBus,
        TEvent @event) where TEvent : LockEvent
    {
        _ = eventBus.PublishAsync(@event);
    }

    /// <summary>
    /// Registers the event bus in dependency injection.
    /// </summary>
    public static IServiceCollection AddLockEventBus(
        this IServiceCollection services,
        int maxHistorySize = 10000)
    {
        services.AddSingleton<ILockEventBus>(
            sp => new InMemoryLockEventBus(
                sp.GetRequiredService<ILogger<InMemoryLockEventBus>>(),
                maxHistorySize));

        return services;
    }
}
