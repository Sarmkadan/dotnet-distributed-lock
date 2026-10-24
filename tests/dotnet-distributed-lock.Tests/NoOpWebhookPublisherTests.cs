#nullable enable
using FluentAssertions;
using SarmKadan.DistributedLock.Integration;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Unit tests for <see cref="NoOpWebhookPublisher"/>, verifying it is safe to use as a default or disabled-state substitute.
/// </summary>
public class NoOpWebhookPublisherTests
{
    /// <summary>
    /// Every publish method returns a task that is already completed, so it never blocks.
    /// </summary>
    /// <param name="eventType">The event method under test.</param>
    [Theory]
    [MemberData(nameof(WebhookTestHelpers.EventTypes), MemberType = typeof(WebhookTestHelpers))]
    public void Publish_ReturnsAlreadyCompletedTask(string eventType)
    {
        var publisher = new NoOpWebhookPublisher();

        var task = WebhookTestHelpers.Publish(publisher, eventType, WebhookTestHelpers.CreateLock());

        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    /// <summary>
    /// Publishing never throws, even when the caller's token is already cancelled, because the token is ignored.
    /// </summary>
    /// <param name="eventType">The event method under test.</param>
    [Theory]
    [MemberData(nameof(WebhookTestHelpers.EventTypes), MemberType = typeof(WebhookTestHelpers))]
    public async Task Publish_WithCancelledToken_DoesNotThrow(string eventType)
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var publisher = new NoOpWebhookPublisher();

        var act = () => WebhookTestHelpers.Publish(publisher, eventType, WebhookTestHelpers.CreateLock(), cts.Token);

        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// Repeated publishing of every event type keeps completing synchronously, so a disabled publisher adds no latency.
    /// </summary>
    [Fact]
    public void Publish_RepeatedCalls_AllCompleteSynchronously()
    {
        IWebhookPublisher publisher = new NoOpWebhookPublisher();
        var @lock = WebhookTestHelpers.CreateLock();

        for (var i = 0; i < 1_000; i++)
        {
            foreach (var eventType in WebhookTestHelpers.EventTypeNames)
            {
                WebhookTestHelpers.Publish(publisher, eventType, @lock).IsCompletedSuccessfully.Should().BeTrue();
            }
        }
    }
}
