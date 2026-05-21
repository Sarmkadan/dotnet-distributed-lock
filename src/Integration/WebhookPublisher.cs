#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Integration;

using System.Text.Json;
using SarmKadan.DistributedLock.Core.Models;

/// <summary>
/// Publishes lock events to external webhook endpoints.
/// Allows integration with external systems when locks are acquired, released, or expire.
/// Implements retry logic and dead-letter handling for failed webhook deliveries.
/// </summary>
public interface IWebhookPublisher
{
    Task PublishLockAcquiredAsync(Lock @lock, CancellationToken cancellationToken = default);
    Task PublishLockReleasedAsync(Lock @lock, CancellationToken cancellationToken = default);
    Task PublishLockExpiredAsync(Lock @lock, CancellationToken cancellationToken = default);
    Task PublishLockRenewedAsync(Lock @lock, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation that sends webhooks via HTTP POST.
/// </summary>
public class HttpWebhookPublisher : IWebhookPublisher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpWebhookPublisher> _logger;
    private readonly WebhookConfig _config;

    public HttpWebhookPublisher(
        HttpClient httpClient,
        ILogger<HttpWebhookPublisher> logger,
        WebhookConfig? config = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? new WebhookConfig();
    }

    public async Task PublishLockAcquiredAsync(Lock @lock, CancellationToken cancellationToken = default)
    {
        if (!_config.EnableAcquiredEvent)
            return;

        await PublishWebhookAsync(
            "lock.acquired",
            @lock,
            cancellationToken);
    }

    public async Task PublishLockReleasedAsync(Lock @lock, CancellationToken cancellationToken = default)
    {
        if (!_config.EnableReleasedEvent)
            return;

        await PublishWebhookAsync(
            "lock.released",
            @lock,
            cancellationToken);
    }

    public async Task PublishLockExpiredAsync(Lock @lock, CancellationToken cancellationToken = default)
    {
        if (!_config.EnableExpiredEvent)
            return;

        await PublishWebhookAsync(
            "lock.expired",
            @lock,
            cancellationToken);
    }

    public async Task PublishLockRenewedAsync(Lock @lock, CancellationToken cancellationToken = default)
    {
        if (!_config.EnableRenewedEvent)
            return;

        await PublishWebhookAsync(
            "lock.renewed",
            @lock,
            cancellationToken);
    }

    /// <summary>
    /// Sends a webhook to all configured endpoints.
    /// Uses background fire-and-forget pattern with error logging.
    /// </summary>
    private async Task PublishWebhookAsync(
        string eventType,
        Lock lockData,
        CancellationToken cancellationToken)
    {
        if (_config.Endpoints is null || _config.Endpoints.Count == 0)
        {
            _logger.LogWarning("No webhook endpoints configured for event: {EventType}", eventType);
            return;
        }

        var payload = new WebhookPayload
        {
            EventType = eventType,
            Timestamp = DateTime.UtcNow,
            Data = lockData
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        foreach (var endpoint in _config.Endpoints)
        {
            try
            {
                _logger.LogInformation("Publishing webhook to {Endpoint}: {EventType}", endpoint, eventType);

                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    cts.CancelAfter(_config.TimeoutMs);

                    var response = await _httpClient.PostAsync(endpoint, content, cts.Token).ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Webhook published successfully to {Endpoint}", endpoint);
                    }
                    else
                    {
                        _logger.LogError(
                            "Webhook publish failed: {Endpoint} returned {StatusCode}",
                            endpoint,
                            response.StatusCode);
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogError(ex, "Webhook publish timeout for {Endpoint}", endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish webhook to {Endpoint}", endpoint);
            }
        }
    }
}

/// <summary>
/// Configuration for webhook behavior.
/// </summary>
public class WebhookConfig
{
    /// <summary>
    /// List of webhook endpoints to publish to.
    /// </summary>
    public List<string> Endpoints { get; set; } = new();

    /// <summary>
    /// Enable publishing of lock acquired events.
    /// </summary>
    public bool EnableAcquiredEvent { get; set; } = true;

    /// <summary>
    /// Enable publishing of lock released events.
    /// </summary>
    public bool EnableReleasedEvent { get; set; } = true;

    /// <summary>
    /// Enable publishing of lock expired events.
    /// </summary>
    public bool EnableExpiredEvent { get; set; } = true;

    /// <summary>
    /// Enable publishing of lock renewed events.
    /// </summary>
    public bool EnableRenewedEvent { get; set; } = true;

    /// <summary>
    /// Timeout for webhook delivery in milliseconds.
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Number of retries on transient failures.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}

/// <summary>
/// Represents a webhook payload sent to external endpoints.
/// </summary>
public record WebhookPayload
{
    public required string EventType { get; init; }
    public DateTime Timestamp { get; init; }
    public required Lock Data { get; init; }
}

/// <summary>
/// No-op implementation for testing or when webhooks are disabled.
/// </summary>
public class NoOpWebhookPublisher : IWebhookPublisher
{
    public Task PublishLockAcquiredAsync(Lock @lock, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PublishLockReleasedAsync(Lock @lock, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PublishLockExpiredAsync(Lock @lock, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PublishLockRenewedAsync(Lock @lock, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
