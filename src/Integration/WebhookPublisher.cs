#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Integration;

using System.Collections.Concurrent;
using System.Net;
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
/// Tracks the circuit breaker state for a single webhook endpoint.
/// </summary>
internal sealed class EndpointCircuitState
{
    private long _consecutiveFailures;
    private long _openedAtTicks;

    /// <summary>
    /// Records a successful delivery, closing the circuit and resetting the failure count.
    /// </summary>
    public void RecordSuccess()
    {
        Interlocked.Exchange(ref _consecutiveFailures, 0);
        Interlocked.Exchange(ref _openedAtTicks, 0);
    }

    /// <summary>
    /// Records a failed delivery, tripping (or re-tripping) the circuit once the given
    /// threshold is reached. Every qualifying failure - including one that occurs during
    /// a half-open trial call after the previous reset window has already elapsed - moves
    /// the open timestamp forward, so a still-unhealthy endpoint keeps the circuit open
    /// instead of silently falling back to always-closed after the first trip.
    /// </summary>
    /// <param name="failureThreshold">Number of consecutive failures required to open the circuit.</param>
    public void RecordFailure(int failureThreshold)
    {
        var failures = Interlocked.Increment(ref _consecutiveFailures);
        if (failures >= failureThreshold)
        {
            Interlocked.Exchange(ref _openedAtTicks, DateTime.UtcNow.Ticks);
        }
    }

    /// <summary>
    /// Determines whether calls should currently be blocked by the open circuit.
    /// Automatically transitions to half-open (allows one trial call) once the reset
    /// timeout has elapsed.
    /// </summary>
    /// <param name="resetTimeout">Duration the circuit stays open before allowing a trial call.</param>
    public bool IsOpen(TimeSpan resetTimeout)
    {
        var openedAt = Interlocked.Read(ref _openedAtTicks);
        if (openedAt == 0)
            return false;

        var elapsed = DateTime.UtcNow - new DateTime(openedAt, DateTimeKind.Utc);
        return elapsed < resetTimeout;
    }
}

/// <summary>
/// Default implementation that sends webhooks via HTTP POST with retry, exponential
/// backoff with jitter, and a per-endpoint circuit breaker. Delivery failures are
/// logged and counted, but are never allowed to propagate into the lock acquire or
/// release path.
/// </summary>
public class HttpWebhookPublisher : IWebhookPublisher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpWebhookPublisher> _logger;
    private readonly WebhookConfig _config;
    private readonly ConcurrentDictionary<string, EndpointCircuitState> _circuitStates = new();
    private long _failureCount;

    /// <summary>
    /// Total number of webhook delivery failures observed since this instance was created,
    /// including retries and circuit-breaker short-circuits.
    /// </summary>
    public long FailureCount => Interlocked.Read(ref _failureCount);

    /// <summary>
    /// Creates a new <see cref="HttpWebhookPublisher"/>.
    /// </summary>
    /// <param name="httpClient">HTTP client used to deliver webhook requests.</param>
    /// <param name="logger">Logger used for delivery diagnostics.</param>
    /// <param name="config">Optional webhook configuration; defaults are used when omitted.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClient"/> or <paramref name="logger"/> is null.</exception>
    public HttpWebhookPublisher(
        HttpClient httpClient,
        ILogger<HttpWebhookPublisher> logger,
        WebhookConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _logger = logger;
        _config = config ?? new WebhookConfig();
    }

    /// <summary>
    /// Publishes a lock-acquired event, if enabled by configuration.
    /// </summary>
    /// <param name="lock">The lock the event pertains to.</param>
    /// <param name="cancellationToken">Token used to cancel the publish operation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lock"/> is null.</exception>
    public async Task PublishLockAcquiredAsync(Lock @lock, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@lock);

        if (!_config.EnableAcquiredEvent)
            return;

        await PublishWebhookAsync("lock.acquired", @lock, cancellationToken);
    }

    /// <summary>
    /// Publishes a lock-released event, if enabled by configuration.
    /// </summary>
    /// <param name="lock">The lock the event pertains to.</param>
    /// <param name="cancellationToken">Token used to cancel the publish operation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lock"/> is null.</exception>
    public async Task PublishLockReleasedAsync(Lock @lock, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@lock);

        if (!_config.EnableReleasedEvent)
            return;

        await PublishWebhookAsync("lock.released", @lock, cancellationToken);
    }

    /// <summary>
    /// Publishes a lock-expired event, if enabled by configuration.
    /// </summary>
    /// <param name="lock">The lock the event pertains to.</param>
    /// <param name="cancellationToken">Token used to cancel the publish operation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lock"/> is null.</exception>
    public async Task PublishLockExpiredAsync(Lock @lock, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@lock);

        if (!_config.EnableExpiredEvent)
            return;

        await PublishWebhookAsync("lock.expired", @lock, cancellationToken);
    }

    /// <summary>
    /// Publishes a lock-renewed event, if enabled by configuration.
    /// </summary>
    /// <param name="lock">The lock the event pertains to.</param>
    /// <param name="cancellationToken">Token used to cancel the publish operation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lock"/> is null.</exception>
    public async Task PublishLockRenewedAsync(Lock @lock, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@lock);

        if (!_config.EnableRenewedEvent)
            return;

        await PublishWebhookAsync("lock.renewed", @lock, cancellationToken);
    }

    /// <summary>
    /// Sends a webhook to all configured endpoints, applying retry with exponential
    /// backoff and jitter plus a per-endpoint circuit breaker. All failures are caught,
    /// logged, and counted here so they never surface to the lock acquire/release path.
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

        foreach (var endpoint in _config.Endpoints)
        {
            try
            {
                await SendWithResilienceAsync(endpoint, eventType, json, cancellationToken);
            }
            catch (Exception ex)
            {
                // Defense in depth: SendWithResilienceAsync already swallows delivery
                // failures, but the lock pipeline must never observe an exception here
                // under any circumstance (e.g. an unexpected bug in the resilience path).
                Interlocked.Increment(ref _failureCount);
                _logger.LogError(ex, "Unexpected error publishing webhook to {Endpoint}", endpoint);
            }
        }
    }

    /// <summary>
    /// Sends a single webhook request to <paramref name="endpoint"/>, honoring the
    /// circuit breaker and retrying transient failures with exponential backoff and jitter.
    /// </summary>
    private async Task SendWithResilienceAsync(
        string endpoint,
        string eventType,
        string json,
        CancellationToken cancellationToken)
    {
        var circuit = _circuitStates.GetOrAdd(endpoint, static _ => new EndpointCircuitState());
        var resetTimeout = TimeSpan.FromMilliseconds(_config.CircuitBreakerResetMs);

        if (circuit.IsOpen(resetTimeout))
        {
            // Circuit is open: behave like a no-op publisher for this endpoint until
            // the reset timeout elapses, so a flaky endpoint cannot stall the lock pipeline.
            Interlocked.Increment(ref _failureCount);
            _logger.LogWarning(
                "Webhook circuit breaker open for {Endpoint}; skipping delivery of {EventType}",
                endpoint,
                eventType);
            return;
        }

        var maxAttempts = Math.Max(1, _config.MaxRetries + 1);
        var random = Random.Shared;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_config.TimeoutMs);

                _logger.LogInformation(
                    "Publishing webhook to {Endpoint}: {EventType} (attempt {Attempt}/{MaxAttempts})",
                    endpoint,
                    eventType,
                    attempt,
                    maxAttempts);

                var response = await _httpClient.PostAsync(endpoint, content, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Webhook published successfully to {Endpoint}", endpoint);
                    circuit.RecordSuccess();
                    return;
                }

                if (!IsTransientStatusCode(response.StatusCode) || attempt == maxAttempts)
                {
                    _logger.LogError(
                        "Webhook publish failed: {Endpoint} returned {StatusCode} (attempt {Attempt}/{MaxAttempts})",
                        endpoint,
                        response.StatusCode,
                        attempt,
                        maxAttempts);
                    Interlocked.Increment(ref _failureCount);
                    circuit.RecordFailure(_config.CircuitBreakerFailureThreshold);
                    return;
                }

                _logger.LogWarning(
                    "Webhook publish attempt {Attempt}/{MaxAttempts} to {Endpoint} returned {StatusCode}; retrying",
                    attempt,
                    maxAttempts,
                    endpoint,
                    response.StatusCode);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Per-request timeout, not caller-initiated cancellation: treat as transient.
                if (attempt == maxAttempts)
                {
                    _logger.LogError(
                        "Webhook publish timeout for {Endpoint} after {Attempt} attempts",
                        endpoint,
                        attempt);
                    Interlocked.Increment(ref _failureCount);
                    circuit.RecordFailure(_config.CircuitBreakerFailureThreshold);
                    return;
                }

                _logger.LogWarning(
                    "Webhook publish attempt {Attempt}/{MaxAttempts} to {Endpoint} timed out; retrying",
                    attempt,
                    maxAttempts,
                    endpoint);
            }
            catch (OperationCanceledException)
            {
                // Caller cancelled the overall operation: stop retrying, do not count
                // as a delivery failure, and do not propagate into the lock pipeline.
                _logger.LogInformation("Webhook publish to {Endpoint} cancelled by caller", endpoint);
                return;
            }
            catch (HttpRequestException ex)
            {
                if (attempt == maxAttempts)
                {
                    _logger.LogError(ex, "Failed to publish webhook to {Endpoint} after {Attempt} attempts", endpoint, attempt);
                    Interlocked.Increment(ref _failureCount);
                    circuit.RecordFailure(_config.CircuitBreakerFailureThreshold);
                    return;
                }

                _logger.LogWarning(
                    ex,
                    "Webhook publish attempt {Attempt}/{MaxAttempts} to {Endpoint} failed; retrying",
                    attempt,
                    maxAttempts,
                    endpoint);
            }
            catch (Exception ex)
            {
                // Unexpected, non-transient failure: no point retrying.
                _logger.LogError(ex, "Failed to publish webhook to {Endpoint}", endpoint);
                Interlocked.Increment(ref _failureCount);
                circuit.RecordFailure(_config.CircuitBreakerFailureThreshold);
                return;
            }

            var delay = ComputeBackoffDelay(attempt, _config.BaseRetryDelayMs, _config.MaxRetryDelayMs, random);
            try
            {
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Computes an exponential backoff delay with full jitter for the given attempt number.
    /// </summary>
    /// <param name="attempt">1-based attempt number that just failed.</param>
    /// <param name="baseDelayMs">Base delay in milliseconds.</param>
    /// <param name="maxDelayMs">Upper bound on the computed delay.</param>
    /// <param name="random">Random source used for jitter.</param>
    private static TimeSpan ComputeBackoffDelay(int attempt, int baseDelayMs, int maxDelayMs, Random random)
    {
        var exponential = baseDelayMs * Math.Pow(2, attempt - 1);
        var capped = Math.Min(exponential, maxDelayMs);
        var jittered = random.NextDouble() * capped;
        return TimeSpan.FromMilliseconds(jittered);
    }

    /// <summary>
    /// Determines whether an HTTP status code represents a transient failure worth retrying
    /// (server errors, request timeout, and too-many-requests).
    /// </summary>
    /// <param name="statusCode">Status code returned by the webhook endpoint.</param>
    private static bool IsTransientStatusCode(HttpStatusCode statusCode) =>
        (int)statusCode >= 500 || statusCode == HttpStatusCode.RequestTimeout || statusCode == HttpStatusCode.TooManyRequests;
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
    /// Timeout for a single webhook delivery attempt, in milliseconds.
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Number of retries on transient failures (5xx, request timeout, 429), in addition
    /// to the initial attempt.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Base delay, in milliseconds, used as the starting point for exponential backoff
    /// between retry attempts. Actual delay includes random jitter up to this exponential value.
    /// </summary>
    public int BaseRetryDelayMs { get; set; } = 200;

    /// <summary>
    /// Upper bound, in milliseconds, on the computed backoff delay between retry attempts.
    /// </summary>
    public int MaxRetryDelayMs { get; set; } = 5000;

    /// <summary>
    /// Number of consecutive delivery failures to a given endpoint required to trip
    /// the circuit breaker for that endpoint.
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    /// <summary>
    /// Duration, in milliseconds, that the circuit breaker stays open for an endpoint
    /// before allowing another delivery attempt.
    /// </summary>
    public int CircuitBreakerResetMs { get; set; } = 30000;
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
