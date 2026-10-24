#nullable enable
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Integration;
using SarmKadan.DistributedLock.Models;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Selects which <see cref="IWebhookPublisher"/> implementation a shared test should construct.
/// </summary>
public enum WebhookPublisherKind
{
    /// <summary>The HTTP-backed publisher.</summary>
    Http,

    /// <summary>The disabled-state publisher.</summary>
    NoOp
}

/// <summary>
/// Shared fixtures for the webhook publisher test classes.
/// </summary>
public static class WebhookTestHelpers
{
    /// <summary>
    /// Endpoint used by default in tests. The request never leaves the process because
    /// every test injects a <see cref="FakeWebhookHandler"/>.
    /// </summary>
    public const string Endpoint = "https://hooks.example.test/lock-events";

    /// <summary>
    /// Event type strings expected for the four <see cref="IWebhookPublisher"/> methods.
    /// </summary>
    public static readonly string[] EventTypeNames = ["lock.acquired", "lock.released", "lock.expired", "lock.renewed"];

    /// <summary>
    /// Theory data holding each name in <see cref="EventTypeNames"/> as one row.
    /// </summary>
    /// <returns>Theory data with one event type per row.</returns>
    public static TheoryData<string> EventTypes() => new(EventTypeNames);

    /// <summary>
    /// Cross product of every <see cref="WebhookPublisherKind"/> and every event type.
    /// </summary>
    /// <returns>Theory data with one (kind, eventType) pair per row.</returns>
    public static TheoryData<WebhookPublisherKind, string> KindsAndEventTypes()
    {
        var data = new TheoryData<WebhookPublisherKind, string>();
        foreach (var kind in Enum.GetValues<WebhookPublisherKind>())
        {
            foreach (var eventType in EventTypeNames)
            {
                data.Add(kind, eventType);
            }
        }

        return data;
    }

    /// <summary>
    /// Creates a lock snapshot with every field the webhook payload carries populated.
    /// </summary>
    /// <returns>A held lock with a known key and owner.</returns>
    public static Lock CreateLock() => new()
    {
        Key = "orders:42",
        OwnerId = "worker-1",
        Status = LockStatus.Held,
        AcquiredAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddMinutes(1),
        Duration = TimeSpan.FromMinutes(1)
    };

    /// <summary>
    /// Creates a webhook configuration with no retry delay so retry-path tests run instantly.
    /// </summary>
    /// <param name="endpoints">Endpoints to publish to; none when omitted.</param>
    /// <returns>A configuration targeting the supplied endpoints.</returns>
    public static WebhookConfig CreateConfig(params string[] endpoints) => new()
    {
        Endpoints = endpoints.ToList(),
        TimeoutMs = 1000,
        MaxRetries = 2,
        BaseRetryDelayMs = 0,
        MaxRetryDelayMs = 0
    };

    /// <summary>
    /// Creates an <see cref="HttpWebhookPublisher"/> whose requests are served by <paramref name="handler"/>.
    /// </summary>
    /// <param name="handler">The fake handler that records and answers each request.</param>
    /// <param name="config">The webhook configuration to apply.</param>
    /// <returns>A publisher ready for use in a test.</returns>
    public static HttpWebhookPublisher CreateHttpPublisher(HttpMessageHandler handler, WebhookConfig config) =>
        new(new HttpClient(handler), NullLogger<HttpWebhookPublisher>.Instance, config);

    /// <summary>
    /// Creates a publisher of the requested kind with default test wiring.
    /// </summary>
    /// <param name="kind">The implementation to construct.</param>
    /// <returns>A publisher typed as <see cref="IWebhookPublisher"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="kind"/> is not a known implementation.</exception>
    public static IWebhookPublisher Create(WebhookPublisherKind kind) => kind switch
    {
        WebhookPublisherKind.Http => CreateHttpPublisher(
            FakeWebhookHandler.Responding(HttpStatusCode.OK),
            CreateConfig(Endpoint)),
        WebhookPublisherKind.NoOp => new NoOpWebhookPublisher(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown webhook publisher kind.")
    };

    /// <summary>
    /// Invokes the publish method on <paramref name="publisher"/> that matches <paramref name="eventType"/>.
    /// </summary>
    /// <param name="publisher">The publisher under test.</param>
    /// <param name="eventType">One of the strings returned by <see cref="EventTypes"/>.</param>
    /// <param name="lock">The lock to publish; may be null to exercise argument validation.</param>
    /// <param name="cancellationToken">Token passed to the publish call.</param>
    /// <returns>The task returned by the publish method.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="eventType"/> is not recognized.</exception>
    public static Task Publish(
        IWebhookPublisher publisher,
        string eventType,
        Lock @lock,
        CancellationToken cancellationToken = default) => eventType switch
    {
        "lock.acquired" => publisher.PublishLockAcquiredAsync(@lock, cancellationToken),
        "lock.released" => publisher.PublishLockReleasedAsync(@lock, cancellationToken),
        "lock.expired" => publisher.PublishLockExpiredAsync(@lock, cancellationToken),
        "lock.renewed" => publisher.PublishLockRenewedAsync(@lock, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, "Unknown lock event type.")
    };
}

/// <summary>
/// An <see cref="HttpMessageHandler"/> that records every outgoing request and answers it with a
/// caller-supplied function, so webhook delivery can be tested without a network.
/// </summary>
public sealed class FakeWebhookHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;
    private readonly List<CapturedWebhookRequest> _requests = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeWebhookHandler"/> class.
    /// </summary>
    /// <param name="respond">Produces the response for each request; may throw or honor the token to simulate failures.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="respond"/> is null.</exception>
    public FakeWebhookHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        ArgumentNullException.ThrowIfNull(respond);

        _respond = respond;
    }

    /// <summary>
    /// Every request received so far, in arrival order, with its body already read.
    /// </summary>
    public IReadOnlyList<CapturedWebhookRequest> Requests => _requests;

    /// <summary>
    /// Creates a handler that answers every request with the given status code and an empty body.
    /// </summary>
    /// <param name="statusCode">The status code to return.</param>
    /// <returns>A handler that always responds with <paramref name="statusCode"/>.</returns>
    public static FakeWebhookHandler Responding(HttpStatusCode statusCode) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)));

    /// <summary>
    /// Records the request (reading its body before the response is produced) and delegates to the responder.
    /// </summary>
    /// <param name="request">The outgoing request.</param>
    /// <param name="cancellationToken">Token supplied by the caller's <see cref="HttpClient"/>.</param>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);

        _requests.Add(new CapturedWebhookRequest(
            request.Method,
            request.RequestUri!,
            request.Content?.Headers.ContentType?.MediaType,
            body));

        return await _respond(request, cancellationToken);
    }
}

/// <summary>
/// A snapshot of one request received by <see cref="FakeWebhookHandler"/>.
/// </summary>
/// <param name="Method">HTTP method used.</param>
/// <param name="Uri">Absolute URI the request was sent to.</param>
/// <param name="ContentType">Media type of the request body, or null when there was no body.</param>
/// <param name="Body">Request body as UTF-8 text.</param>
public sealed record CapturedWebhookRequest(HttpMethod Method, Uri Uri, string? ContentType, string Body);
