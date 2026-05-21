#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Events;

/// <summary>
/// Base class for subscribing to lock events.
/// Implements common subscription patterns and error handling.
/// </summary>
public abstract class LockEventSubscriber
{
    protected readonly ILogger _logger;

    protected LockEventSubscriber(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Registers all event handlers with the publisher.
    /// </summary>
    public abstract Task RegisterAsync(ILockEventPublisher publisher);

    protected async Task HandleEventAsync<TEvent>(
        TEvent @event,
        Func<TEvent, Task> handler) where TEvent : LockEvent
    {
        try
        {
            _logger.LogDebug("Handling event: {EventType}", typeof(TEvent).Name);
            await handler(@event).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling event: {EventType}", typeof(TEvent).Name);
        }
    }
}

/// <summary>
/// Example subscriber that logs all lock events.
/// </summary>
public sealed class LoggingLockEventSubscriber : LockEventSubscriber
{
    public LoggingLockEventSubscriber(ILogger<LoggingLockEventSubscriber> logger) : base(logger)
    {
    }

    public override async Task RegisterAsync(ILockEventPublisher publisher)
    {
        publisher.Subscribe<LockAcquiredEvent>(OnLockAcquired);
        publisher.Subscribe<LockReleasedEvent>(OnLockReleased);
        publisher.Subscribe<LockExpiredEvent>(OnLockExpired);
        publisher.Subscribe<LockRenewedEvent>(OnLockRenewed);
        publisher.Subscribe<LockAcquisitionFailedEvent>(OnAcquisitionFailed);
        publisher.Subscribe<LockContentionEvent>(OnContention);
        publisher.Subscribe<LockErrorEvent>(OnError);

        await Task.CompletedTask;
    }

    private async Task OnLockAcquired(LockAcquiredEvent @event)
    {
        _logger.LogInformation(
            "Lock acquired: {LockName} by {OwnerId}, expires {ExpiresAt:O}",
            @event.LockName,
            @event.OwnerId,
            @event.ExpiresAt);

        await Task.CompletedTask;
    }

    private async Task OnLockReleased(LockReleasedEvent @event)
    {
        _logger.LogInformation(
            "Lock released: {LockName}, held for {Duration}",
            @event.LockName,
            @event.HeldDuration);

        await Task.CompletedTask;
    }

    private async Task OnLockExpired(LockExpiredEvent @event)
    {
        _logger.LogInformation(
            "Lock expired: {LockName} (held for {Duration})",
            @event.LockName,
            @event.TotalDuration);

        await Task.CompletedTask;
    }

    private async Task OnLockRenewed(LockRenewedEvent @event)
    {
        _logger.LogInformation(
            "Lock renewed: {LockName}, new expiration {NewExpiresAt:O}",
            @event.LockName,
            @event.NewExpiresAt);

        await Task.CompletedTask;
    }

    private async Task OnAcquisitionFailed(LockAcquisitionFailedEvent @event)
    {
        _logger.LogWarning(
            "Failed to acquire lock: {LockName} - {Reason}",
            @event.LockName,
            @event.Reason);

        await Task.CompletedTask;
    }

    private async Task OnContention(LockContentionEvent @event)
    {
        _logger.LogWarning(
            "Lock contention detected: {LockName}, level: {Level}, parties: {Count}",
            @event.LockName,
            @event.ContentionLevel,
            @event.CompetingParties.Count);

        await Task.CompletedTask;
    }

    private async Task OnError(LockErrorEvent @event)
    {
        _logger.LogError(
            "Lock operation error: {LockId} ({Operation}) - {Message}",
            @event.LockId,
            @event.OperationType,
            @event.ErrorMessage);

        await Task.CompletedTask;
    }
}

/// <summary>
/// Subscriber that tracks metrics based on lock events.
/// </summary>
public sealed class MetricsTrackingEventSubscriber : LockEventSubscriber
{
    private long _acquisitions;
    private long _releases;
    private long _failures;
    private long _contentionEvents;

    public MetricsTrackingEventSubscriber(ILogger<MetricsTrackingEventSubscriber> logger) : base(logger)
    {
    }

    public override async Task RegisterAsync(ILockEventPublisher publisher)
    {
        publisher.Subscribe<LockAcquiredEvent>(OnLockAcquired);
        publisher.Subscribe<LockReleasedEvent>(OnLockReleased);
        publisher.Subscribe<LockAcquisitionFailedEvent>(OnAcquisitionFailed);
        publisher.Subscribe<LockContentionEvent>(OnContention);

        await Task.CompletedTask;
    }

    private async Task OnLockAcquired(LockAcquiredEvent @event)
    {
        Interlocked.Increment(ref _acquisitions);
        await Task.CompletedTask;
    }

    private async Task OnLockReleased(LockReleasedEvent @event)
    {
        Interlocked.Increment(ref _releases);
        await Task.CompletedTask;
    }

    private async Task OnAcquisitionFailed(LockAcquisitionFailedEvent @event)
    {
        Interlocked.Increment(ref _failures);
        await Task.CompletedTask;
    }

    private async Task OnContention(LockContentionEvent @event)
    {
        Interlocked.Increment(ref _contentionEvents);
        await Task.CompletedTask;
    }

    public EventMetrics GetMetrics()
    {
        return new EventMetrics
        {
            Acquisitions = Interlocked.Read(ref _acquisitions),
            Releases = Interlocked.Read(ref _releases),
            Failures = Interlocked.Read(ref _failures),
            ContentionEvents = Interlocked.Read(ref _contentionEvents),
            Timestamp = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Metrics collected from lock events.
/// </summary>
public record EventMetrics
{
    public long Acquisitions { get; init; }
    public long Releases { get; init; }
    public long Failures { get; init; }
    public long ContentionEvents { get; init; }
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Extension methods for event subscription.
/// </summary>
public static class LockEventSubscriberExtensions
{
    /// <summary>
    /// Registers all lock event subscribers in the dependency injection container.
    /// </summary>
    public static IServiceCollection AddLockEventSubscribers(this IServiceCollection services)
    {
        services.AddSingleton<LoggingLockEventSubscriber>();
        services.AddSingleton<MetricsTrackingEventSubscriber>();

        return services;
    }

    /// <summary>
    /// Initializes all registered event subscribers.
    /// Should be called during application startup.
    /// </summary>
    public static async Task InitializeLockEventSubscribersAsync(this IServiceProvider serviceProvider)
    {
        var publisher = serviceProvider.GetRequiredService<ILockEventPublisher>();

        var loggingSubscriber = serviceProvider.GetService<LoggingLockEventSubscriber>();
        if (loggingSubscriber is not null)
        {
            await loggingSubscriber.RegisterAsync(publisher).ConfigureAwait(false);
        }

        var metricsSubscriber = serviceProvider.GetService<MetricsTrackingEventSubscriber>();
        if (metricsSubscriber is not null)
        {
            await metricsSubscriber.RegisterAsync(publisher).ConfigureAwait(false);
        }
    }
}
