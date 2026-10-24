#nullable enable
using System.Net;
using System.Text.Json;
using FluentAssertions;
using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Integration;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Unit tests for <see cref="HttpWebhookPublisher"/> covering payload serialization, endpoint delivery,
/// and the failure contract: delivery problems are retried or counted, never thrown to the caller.
/// </summary>
public class HttpWebhookPublisherTests
{
    /// <summary>
    /// Serializes the lock as a <see cref="WebhookPayload"/> and POSTs it as JSON to the configured endpoint.
    /// </summary>
    [Fact]
    public async Task PublishLockAcquiredAsync_PostsSerializedWebhookPayloadToEndpoint()
    {
        var handler = FakeWebhookHandler.Responding(HttpStatusCode.OK);
        var publisher = WebhookTestHelpers.CreateHttpPublisher(
            handler,
            WebhookTestHelpers.CreateConfig(WebhookTestHelpers.Endpoint));
        var @lock = WebhookTestHelpers.CreateLock();

        await publisher.PublishLockAcquiredAsync(@lock);

        handler.Requests.Should().ContainSingle();
        var request = handler.Requests[0];
        request.Method.Should().Be(HttpMethod.Post);
        request.Uri.Should().Be(new Uri(WebhookTestHelpers.Endpoint));
        request.ContentType.Should().Be("application/json");

        var payload = JsonSerializer.Deserialize<WebhookPayload>(request.Body);
        payload.Should().NotBeNull();
        payload!.EventType.Should().Be("lock.acquired");
        payload.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        payload.Data.Key.Should().Be(@lock.Key);
        payload.Data.OwnerId.Should().Be(@lock.OwnerId);
        payload.Data.Status.Should().Be(LockStatus.Held);
    }

    /// <summary>
    /// Each publish method stamps its own event type onto the payload.
    /// </summary>
    /// <param name="eventType">The event type expected in the serialized payload.</param>
    [Theory]
    [MemberData(nameof(WebhookTestHelpers.EventTypes), MemberType = typeof(WebhookTestHelpers))]
    public async Task EachPublishMethod_SendsItsOwnEventType(string eventType)
    {
        var handler = FakeWebhookHandler.Responding(HttpStatusCode.OK);
        var publisher = WebhookTestHelpers.CreateHttpPublisher(
            handler,
            WebhookTestHelpers.CreateConfig(WebhookTestHelpers.Endpoint));

        await WebhookTestHelpers.Publish(publisher, eventType, WebhookTestHelpers.CreateLock());

        var payload = JsonSerializer.Deserialize<WebhookPayload>(handler.Requests.Single().Body);
        payload!.EventType.Should().Be(eventType);
    }

    /// <summary>
    /// A publish whose event flag is disabled in configuration sends nothing.
    /// </summary>
    [Fact]
    public async Task PublishLockAcquiredAsync_WhenEventDisabled_DoesNotSend()
    {
        var handler = FakeWebhookHandler.Responding(HttpStatusCode.OK);
        var config = WebhookTestHelpers.CreateConfig(WebhookTestHelpers.Endpoint);
        config.EnableAcquiredEvent = false;
        var publisher = WebhookTestHelpers.CreateHttpPublisher(handler, config);

        await publisher.PublishLockAcquiredAsync(WebhookTestHelpers.CreateLock());

        handler.Requests.Should().BeEmpty();
    }

    /// <summary>
    /// With no endpoints configured the publish completes without sending or throwing.
    /// </summary>
    [Fact]
    public async Task PublishLockAcquiredAsync_WithNoEndpoints_DoesNotSendOrThrow()
    {
        var handler = FakeWebhookHandler.Responding(HttpStatusCode.OK);
        var publisher = WebhookTestHelpers.CreateHttpPublisher(handler, WebhookTestHelpers.CreateConfig());

        var act = () => publisher.PublishLockAcquiredAsync(WebhookTestHelpers.CreateLock());

        await act.Should().NotThrowAsync();
        handler.Requests.Should().BeEmpty();
    }

    /// <summary>
    /// Every configured endpoint receives its own copy of the event.
    /// </summary>
    [Fact]
    public async Task PublishLockAcquiredAsync_FansOutToEveryEndpoint()
    {
        const string first = "https://a.example.test/hook";
        const string second = "https://b.example.test/hook";
        var handler = FakeWebhookHandler.Responding(HttpStatusCode.NoContent);
        var publisher = WebhookTestHelpers.CreateHttpPublisher(
            handler,
            WebhookTestHelpers.CreateConfig(first, second));

        await publisher.PublishLockAcquiredAsync(WebhookTestHelpers.CreateLock());

        handler.Requests.Select(r => r.Uri).Should().Equal(new Uri(first), new Uri(second));
        publisher.FailureCount.Should().Be(0);
    }

    /// <summary>
    /// A 4xx response is not transient: it is sent once, counted as a failure, and not thrown.
    /// </summary>
    [Fact]
    public async Task PublishLockAcquiredAsync_ClientError_IsNotRetriedAndDoesNotThrow()
    {
        var handler = FakeWebhookHandler.Responding(HttpStatusCode.BadRequest);
        var publisher = WebhookTestHelpers.CreateHttpPublisher(
            handler,
            WebhookTestHelpers.CreateConfig(WebhookTestHelpers.Endpoint));

        var act = () => publisher.PublishLockAcquiredAsync(WebhookTestHelpers.CreateLock());

        await act.Should().NotThrowAsync();
        handler.Requests.Should().ContainSingle();
        publisher.FailureCount.Should().Be(1);
    }

    /// <summary>
    /// A 5xx response is retried up to <see cref="WebhookConfig.MaxRetries"/> times, then counted and not thrown.
    /// </summary>
    [Fact]
    public async Task PublishLockAcquiredAsync_ServerError_RetriesUpToMaxRetriesThenCountsFailure()
    {
        var handler = FakeWebhookHandler.Responding(HttpStatusCode.ServiceUnavailable);
        var config = WebhookTestHelpers.CreateConfig(WebhookTestHelpers.Endpoint);
        config.MaxRetries = 2;
        var publisher = WebhookTestHelpers.CreateHttpPublisher(handler, config);

        var act = () => publisher.PublishLockAcquiredAsync(WebhookTestHelpers.CreateLock());

        await act.Should().NotThrowAsync();
        handler.Requests.Should().HaveCount(3, "one initial attempt plus two retries");
        publisher.FailureCount.Should().Be(1);
    }

    /// <summary>
    /// A transient 5xx followed by a 2xx is delivered, stops retrying, and records no failure.
    /// </summary>
    [Fact]
    public async Task PublishLockAcquiredAsync_TransientServerErrorThenSuccess_StopsRetrying()
    {
        var responses = new Queue<HttpStatusCode>(new[] { HttpStatusCode.InternalServerError, HttpStatusCode.OK });
        var handler = new FakeWebhookHandler((_, _) => Task.FromResult(new HttpResponseMessage(responses.Dequeue())));
        var publisher = WebhookTestHelpers.CreateHttpPublisher(
            handler,
            WebhookTestHelpers.CreateConfig(WebhookTestHelpers.Endpoint));

        await publisher.PublishLockAcquiredAsync(WebhookTestHelpers.CreateLock());

        handler.Requests.Should().HaveCount(2);
        publisher.FailureCount.Should().Be(0);
    }

    /// <summary>
    /// A per-attempt timeout is treated as transient: retried, then counted and not thrown.
    /// </summary>
    [Fact]
    public async Task PublishLockAcquiredAsync_RequestTimeout_RetriesThenCountsFailureWithoutThrowing()
    {
        var handler = new FakeWebhookHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var config = WebhookTestHelpers.CreateConfig(WebhookTestHelpers.Endpoint);
        config.TimeoutMs = 20;
        config.MaxRetries = 1;
        var publisher = WebhookTestHelpers.CreateHttpPublisher(handler, config);

        var act = () => publisher.PublishLockAcquiredAsync(WebhookTestHelpers.CreateLock());

        await act.Should().NotThrowAsync();
        handler.Requests.Should().HaveCount(2, "one initial attempt plus one retry");
        publisher.FailureCount.Should().Be(1);
    }

    /// <summary>
    /// A transport-level <see cref="HttpRequestException"/> is treated as transient: retried, then counted.
    /// </summary>
    [Fact]
    public async Task PublishLockAcquiredAsync_TransportFailure_RetriesThenCountsFailureWithoutThrowing()
    {
        var handler = new FakeWebhookHandler((_, _) => throw new HttpRequestException("connection refused"));
        var config = WebhookTestHelpers.CreateConfig(WebhookTestHelpers.Endpoint);
        config.MaxRetries = 1;
        var publisher = WebhookTestHelpers.CreateHttpPublisher(handler, config);

        var act = () => publisher.PublishLockAcquiredAsync(WebhookTestHelpers.CreateLock());

        await act.Should().NotThrowAsync();
        handler.Requests.Should().HaveCount(2);
        publisher.FailureCount.Should().Be(1);
    }

    /// <summary>
    /// An unexpected exception is not retried, is counted, and is not thrown into the lock pipeline.
    /// </summary>
    [Fact]
    public async Task PublishLockAcquiredAsync_UnexpectedException_IsNotRetriedAndDoesNotThrow()
    {
        var handler = new FakeWebhookHandler((_, _) => throw new InvalidOperationException("boom"));
        var publisher = WebhookTestHelpers.CreateHttpPublisher(
            handler,
            WebhookTestHelpers.CreateConfig(WebhookTestHelpers.Endpoint));

        var act = () => publisher.PublishLockAcquiredAsync(WebhookTestHelpers.CreateLock());

        await act.Should().NotThrowAsync();
        handler.Requests.Should().ContainSingle();
        publisher.FailureCount.Should().Be(1);
    }

    /// <summary>
    /// When the caller cancels mid-delivery, the publish returns quietly: no retry and no failure counted.
    /// </summary>
    [Fact]
    public async Task PublishLockAcquiredAsync_CallerCancelled_ReturnsWithoutThrowingOrCountingFailure()
    {
        using var cts = new CancellationTokenSource();
        var handler = new FakeWebhookHandler(async (_, ct) =>
        {
            await cts.CancelAsync();
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var publisher = WebhookTestHelpers.CreateHttpPublisher(
            handler,
            WebhookTestHelpers.CreateConfig(WebhookTestHelpers.Endpoint));

        var act = () => publisher.PublishLockAcquiredAsync(WebhookTestHelpers.CreateLock(), cts.Token);

        await act.Should().NotThrowAsync();
        handler.Requests.Should().ContainSingle("caller cancellation must not trigger a retry");
        publisher.FailureCount.Should().Be(0);
    }

    /// <summary>
    /// Once the failure threshold is reached the circuit opens, and later publishes skip the endpoint.
    /// Each skipped delivery is counted as a failure.
    /// </summary>
    [Fact]
    public async Task PublishLockAcquiredAsync_AfterThresholdReached_SkipsEndpointUntilReset()
    {
        var handler = FakeWebhookHandler.Responding(HttpStatusCode.InternalServerError);
        var config = WebhookTestHelpers.CreateConfig(WebhookTestHelpers.Endpoint);
        config.MaxRetries = 0;
        config.CircuitBreakerFailureThreshold = 1;
        config.CircuitBreakerResetMs = 60_000;
        var publisher = WebhookTestHelpers.CreateHttpPublisher(handler, config);
        var @lock = WebhookTestHelpers.CreateLock();

        await publisher.PublishLockAcquiredAsync(@lock);
        await publisher.PublishLockAcquiredAsync(@lock);

        handler.Requests.Should().ContainSingle("the second publish must be short-circuited");
        publisher.FailureCount.Should().Be(2);
    }

    /// <summary>
    /// The null check runs before the enabled-event check, so a null lock is rejected even when the event is disabled.
    /// </summary>
    [Fact]
    public async Task PublishLockAcquiredAsync_NullLock_ThrowsEvenWhenEventDisabled()
    {
        var config = WebhookTestHelpers.CreateConfig(WebhookTestHelpers.Endpoint);
        config.EnableAcquiredEvent = false;
        var publisher = WebhookTestHelpers.CreateHttpPublisher(
            FakeWebhookHandler.Responding(HttpStatusCode.OK),
            config);

        var task = publisher.PublishLockAcquiredAsync(null!);
        var act = async () => await task;

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
