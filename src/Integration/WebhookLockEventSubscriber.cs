#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Integration;

using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Events;
using SarmKadan.DistributedLock.Models;

/// <summary>
/// Subscriber that adapts lock lifecycle events into <see cref="WebhookPayload"/> instances
/// and delegates delivery to an <see cref="IWebhookPublisher"/>. This makes the event
/// subscriber pipeline the single fan-out point for lock activity: any new event type only
/// needs a handler here rather than a second, independent dispatch path.
/// </summary>
/// <remarks>
/// Lives alongside the rest of <see cref="SarmKadan.DistributedLock.Integration"/> rather than
/// in <c>src/Events</c> because it depends on <see cref="IWebhookPublisher"/>, and the
/// Integration folder is intentionally excluded from the packaged library build (it needs
/// hosting/HTTP client abstractions the core package does not reference). Consumers that add
/// the Integration sources to their own build can register this subscriber alongside an
/// <see cref="IWebhookPublisher"/> implementation.
/// </remarks>
public sealed class WebhookLockEventSubscriber : LockEventSubscriber
{
    private readonly IWebhookPublisher _webhookPublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookLockEventSubscriber"/> class.
    /// </summary>
    /// <param name="logger">The logger used for diagnostic output.</param>
    /// <param name="webhookPublisher">The publisher used to deliver adapted webhook payloads.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> or <paramref name="webhookPublisher"/> is null.</exception>
    public WebhookLockEventSubscriber(ILogger<WebhookLockEventSubscriber> logger, IWebhookPublisher webhookPublisher) : base(logger)
    {
        ArgumentNullException.ThrowIfNull(webhookPublisher);

        _webhookPublisher = webhookPublisher;
    }

    /// <summary>
    /// Registers the lock lifecycle event handlers that forward to the webhook publisher.
    /// </summary>
    /// <param name="publisher">The event publisher to subscribe against.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="publisher"/> is null.</exception>
    public override async Task RegisterAsync(ILockEventPublisher publisher)
    {
        ArgumentNullException.ThrowIfNull(publisher);

        publisher.Subscribe<LockAcquiredEvent>(OnLockAcquired);
        publisher.Subscribe<LockReleasedEvent>(OnLockReleased);
        publisher.Subscribe<LockExpiredEvent>(OnLockExpired);
        publisher.Subscribe<LockRenewedEvent>(OnLockRenewed);

        await Task.CompletedTask;
    }

    private Task OnLockAcquired(LockAcquiredEvent @event) => HandleEventAsync(@event, e =>
        _webhookPublisher.PublishLockAcquiredAsync(ToLock(e.LockId, e.LockName, e.OwnerId, e.ExpiresAt, e.Duration, e.Status)));

    private Task OnLockReleased(LockReleasedEvent @event) => HandleEventAsync(@event, e =>
        _webhookPublisher.PublishLockReleasedAsync(ToLock(e.LockId, e.LockName, e.OwnerId, e.ReleasedAt, e.HeldDuration, LockStatus.Released, e.AcquiredAt)));

    private Task OnLockExpired(LockExpiredEvent @event) => HandleEventAsync(@event, e =>
        _webhookPublisher.PublishLockExpiredAsync(ToLock(e.LockId, e.LockName, e.OwnerId, e.ExpiredAt, e.TotalDuration, LockStatus.Expired)));

    private Task OnLockRenewed(LockRenewedEvent @event) => HandleEventAsync(@event, e =>
        _webhookPublisher.PublishLockRenewedAsync(ToLock(e.LockId, e.LockName, e.OwnerId, e.NewExpiresAt, e.RenewedDuration, LockStatus.Held)));

    /// <summary>
    /// Builds the <see cref="Lock"/> snapshot expected by <see cref="IWebhookPublisher"/> from
    /// the fields carried on a lock lifecycle event.
    /// </summary>
    private static Lock ToLock(
        string key,
        string lockName,
        string ownerId,
        DateTime expiresAt,
        TimeSpan duration,
        LockStatus status,
        DateTime? acquiredAt = null) =>
        new()
        {
            Key = string.IsNullOrEmpty(key) ? lockName : key,
            OwnerId = ownerId,
            Status = status,
            AcquiredAt = acquiredAt ?? DateTime.UtcNow,
            ExpiresAt = expiresAt,
            Duration = duration
        };
}
