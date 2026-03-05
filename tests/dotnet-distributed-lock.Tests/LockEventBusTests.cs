#nullable enable
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Events;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

public class InMemoryLockEventBusTests
{
    private readonly InMemoryLockEventBus _bus;

    public InMemoryLockEventBusTests()
    {
        _bus = new InMemoryLockEventBus(NullLogger<InMemoryLockEventBus>.Instance);
    }

    // -------------------------------------------------------------------------
    // Subscription and Publishing
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new InMemoryLockEventBus(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public async Task PublishAsync_WithNoSubscribers_DoesNotThrow()
    {
        // Arrange
        var @event = new LockAcquiredEvent("lock:1", "owner-1", LockStatus.Held);

        // Act & Assert
        await _bus.Invoking(b => b.PublishAsync(@event)).Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_WithSyncSubscriber_InvokesHandler()
    {
        // Arrange
        var handled = false;
        var @event = new LockAcquiredEvent("lock:1", "owner-1", LockStatus.Held);

        _bus.Subscribe<LockAcquiredEvent>(_ => handled = true);

        // Act
        await _bus.PublishAsync(@event);

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public async Task PublishAsync_WithAsyncSubscriber_InvokesHandler()
    {
        // Arrange
        var handled = false;
        var @event = new LockAcquiredEvent("lock:1", "owner-1", LockStatus.Held);

        _bus.Subscribe<LockAcquiredEvent>(async e =>
        {
            await Task.Delay(10);
            handled = true;
        });

        // Act
        await _bus.PublishAsync(@event);

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public async Task PublishAsync_WithMultipleSubscribers_InvokesAll()
    {
        // Arrange
        var count = 0;
        var @event = new LockAcquiredEvent("lock:1", "owner-1", LockStatus.Held);

        _bus.Subscribe<LockAcquiredEvent>(_ => count++);
        _bus.Subscribe<LockAcquiredEvent>(_ => count++);
        _bus.Subscribe<LockAcquiredEvent>(_ => count++);

        // Act
        await _bus.PublishAsync(@event);

        // Assert
        count.Should().Be(3);
    }

    // -------------------------------------------------------------------------
    // Subscriber Count
    // -------------------------------------------------------------------------

    [Fact]
    public void GetSubscriberCount_WithNoSubscribers_ReturnsZero()
    {
        // Act
        var count = _bus.GetSubscriberCount<LockAcquiredEvent>();

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void GetSubscriberCount_AfterSubscription_ReturnsCorrectCount()
    {
        // Arrange
        _bus.Subscribe<LockAcquiredEvent>(_ => { });
        _bus.Subscribe<LockAcquiredEvent>(_ => { });

        // Act
        var count = _bus.GetSubscriberCount<LockAcquiredEvent>();

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public void GetSubscriberCount_DifferentEventTypes_AreIndependent()
    {
        // Arrange
        _bus.Subscribe<LockAcquiredEvent>(_ => { });
        _bus.Subscribe<LockReleasedEvent>(_ => { });
        _bus.Subscribe<LockReleasedEvent>(_ => { });

        // Act
        var acquiredCount = _bus.GetSubscriberCount<LockAcquiredEvent>();
        var releasedCount = _bus.GetSubscriberCount<LockReleasedEvent>();

        // Assert
        acquiredCount.Should().Be(1);
        releasedCount.Should().Be(2);
    }

    // -------------------------------------------------------------------------
    // Correlation ID and Source System
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_WithCorrelationId_PropagatesIt()
    {
        // Arrange
        var capturedEvent = default(LockAcquiredEvent);
        var correlationId = Guid.NewGuid().ToString();
        var @event = new LockAcquiredEvent("lock:1", "owner-1", LockStatus.Held);

        _bus.Subscribe<LockAcquiredEvent>(e => capturedEvent = e);

        // Act
        await _bus.PublishAsync(@event, correlationId);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task PublishAsync_SetsSourceSystemIfNotSet()
    {
        // Arrange
        var capturedEvent = default(LockAcquiredEvent);
        var @event = new LockAcquiredEvent("lock:1", "owner-1", LockStatus.Held);

        _bus.Subscribe<LockAcquiredEvent>(e => capturedEvent = e);

        // Act
        await _bus.PublishAsync(@event);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.SourceSystem.Should().Be("InMemoryBus");
    }

    // -------------------------------------------------------------------------
    // Exception Handling
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_WhenSubscriberThrows_ContinuesToPublish()
    {
        // Arrange
        var executed = false;
        var @event = new LockAcquiredEvent("lock:1", "owner-1", LockStatus.Held);

        _bus.Subscribe<LockAcquiredEvent>(_ => throw new InvalidOperationException("Subscriber error"));
        _bus.Subscribe<LockAcquiredEvent>(_ => executed = true);

        // Act & Assert — should not throw even if first subscriber fails
        await _bus.Invoking(b => b.PublishAsync(@event)).Should().NotThrowAsync();

        // Second handler should still execute
        executed.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Event Types
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_WithLockAcquiredEvent_Works()
    {
        // Arrange
        var handled = false;
        var @event = new LockAcquiredEvent("lock:1", "owner-1", LockStatus.Held);

        _bus.Subscribe<LockAcquiredEvent>(_ => handled = true);

        // Act
        await _bus.PublishAsync(@event);

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public async Task PublishAsync_WithLockReleasedEvent_Works()
    {
        // Arrange
        var handled = false;
        var @event = new LockReleasedEvent("lock:1", "owner-1");

        _bus.Subscribe<LockReleasedEvent>(_ => handled = true);

        // Act
        await _bus.PublishAsync(@event);

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public async Task PublishAsync_WithLockRenewedEvent_Works()
    {
        // Arrange
        var handled = false;
        var @event = new LockRenewedEvent("lock:1", "owner-1");

        _bus.Subscribe<LockRenewedEvent>(_ => handled = true);

        // Act
        await _bus.PublishAsync(@event);

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public async Task PublishAsync_WithLockFailedEvent_Works()
    {
        // Arrange
        var handled = false;
        var @event = new LockFailedEvent("lock:1", "owner-1", "Lock contention");

        _bus.Subscribe<LockFailedEvent>(_ => handled = true);

        // Act
        await _bus.PublishAsync(@event);

        // Assert
        handled.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Concurrency
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_ConcurrentPublishers_AllSucceed()
    {
        // Arrange
        var count = 0;
        var lockObj = new object();

        _bus.Subscribe<LockAcquiredEvent>(_ =>
        {
            lock (lockObj) count++;
        });

        var events = Enumerable.Range(0, 100)
            .Select(i => new LockAcquiredEvent($"lock:{i}", "owner-1", LockStatus.Held))
            .ToList();

        // Act
        await Task.WhenAll(events.Select(e => _bus.PublishAsync(e)));

        // Assert
        count.Should().Be(100);
    }

    [Fact]
    public async Task PublishAsync_ConcurrentSubscribersAndPublishers_MaintainsConsistency()
    {
        // Arrange
        var handled = new System.Collections.Concurrent.ConcurrentBag<string>();

        // Multiple subscribers
        for (int i = 0; i < 5; i++)
        {
            var index = i;
            _bus.Subscribe<LockAcquiredEvent>(e => handled.Add($"sub-{index}"));
        }

        var events = Enumerable.Range(0, 10)
            .Select(i => new LockAcquiredEvent($"lock:{i}", "owner-1", LockStatus.Held))
            .ToList();

        // Act
        await Task.WhenAll(events.Select(e => _bus.PublishAsync(e)));

        // Assert — each of 10 events should reach all 5 subscribers
        handled.Should().HaveCount(50);
    }
}
