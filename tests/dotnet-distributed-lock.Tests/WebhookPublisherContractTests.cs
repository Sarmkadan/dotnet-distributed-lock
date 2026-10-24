#nullable enable
using FluentAssertions;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Contract tests that run against every <see cref="SarmKadan.DistributedLock.Integration.IWebhookPublisher"/>
/// implementation and assert they behave identically at the API boundary.
/// </summary>
public class WebhookPublisherContractTests
{
    /// <summary>
    /// A null lock is rejected by every implementation with <see cref="ArgumentNullException"/> on the
    /// returned task (never a synchronous throw), so callers can handle both implementations the same way.
    /// </summary>
    /// <param name="kind">The implementation under test.</param>
    /// <param name="eventType">The publish method under test.</param>
    [Theory]
    [MemberData(nameof(WebhookTestHelpers.KindsAndEventTypes), MemberType = typeof(WebhookTestHelpers))]
    public async Task NullLock_FaultsReturnedTaskWithArgumentNullException(WebhookPublisherKind kind, string eventType)
    {
        var publisher = WebhookTestHelpers.Create(kind);

        var task = WebhookTestHelpers.Publish(publisher, eventType, null!);
        var act = async () => await task;

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// A cancelled caller token never surfaces as an exception from any implementation.
    /// </summary>
    /// <param name="kind">The implementation under test.</param>
    /// <param name="eventType">The publish method under test.</param>
    [Theory]
    [MemberData(nameof(WebhookTestHelpers.KindsAndEventTypes), MemberType = typeof(WebhookTestHelpers))]
    public async Task CancelledToken_CompletesWithoutThrowing(WebhookPublisherKind kind, string eventType)
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var publisher = WebhookTestHelpers.Create(kind);

        var act = () => WebhookTestHelpers.Publish(publisher, eventType, WebhookTestHelpers.CreateLock(), cts.Token);

        await act.Should().NotThrowAsync();
    }
}
