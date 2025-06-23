#nullable enable
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Events;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Unit tests for <see cref="InMemoryLockEventBus"/> implementation.
/// Tests subscription management, event publishing, subscriber counting, correlation ID propagation,
/// exception handling, and concurrency scenarios for the in-memory event bus.
/// </summary>
public class InMemoryLockEventBusTests
{
	private readonly InMemoryLockEventBus _bus;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryLockEventBusTests"/> class.
	/// </summary>
	public InMemoryLockEventBusTests()
	{
		_bus = new InMemoryLockEventBus(NullLogger<InMemoryLockEventBus>.Instance);
	}

	// -------------------------------------------------------------------------
	// Subscription and Publishing
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that constructor throws <see cref="ArgumentNullException"/> when null logger is provided.
	/// </summary>
	[Fact]
	public void Constructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Act & Assert
		var act = () => new InMemoryLockEventBus(null!);
		act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
	}

	/// <summary>
	/// Tests that publishing an event with no subscribers does not throw any exceptions.
	/// </summary>
	[Fact]
	public async Task PublishAsync_WithNoSubscribers_DoesNotThrow()
	{
		// Arrange
		var @event = new LockAcquiredEvent("lock:1", "owner-1", LockStatus.Held);

		// Act & Assert
		await _bus.Invoking(b => b.PublishAsync(@event)).Should().NotThrowAsync();
	}

	/// <summary>
	/// Tests that publishing an event invokes synchronous subscriber handlers.
	/// </summary>
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

	/// <summary>
	/// Tests that publishing an event invokes asynchronous subscriber handlers.
	/// </summary>
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

	/// <summary>
	/// Tests that publishing an event invokes all registered subscriber handlers.
	/// </summary>
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

	/// <summary>
	/// Tests that subscriber count returns zero when no subscribers are registered.
	/// </summary>
	[Fact]
	public void GetSubscriberCount_WithNoSubscribers_ReturnsZero()
	{
		// Act
		var count = _bus.GetSubscriberCount<LockAcquiredEvent>();

		// Assert
		count.Should().Be(0);
	}

	/// <summary>
	/// Tests that subscriber count returns correct count after subscriptions are made.
	/// </summary>
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

	/// <summary>
	/// Tests that subscriber counts for different event types are independent.
	/// </summary>
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

	/// <summary>
	/// Tests that correlation ID is propagated to subscribers when provided during publishing.
	/// </summary>
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

	/// <summary>
	/// Tests that source system is set to "InMemoryBus" when not already set on the event.
	/// </summary>
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

	/// <summary>
	/// Tests that publishing continues to other subscribers when one subscriber throws an exception.
	/// </summary>
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

	/// <summary>
	/// Tests that publishing a <see cref="LockAcquiredEvent"/> works correctly.
	/// </summary>
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

	/// <summary>
	/// Tests that publishing a <see cref="LockReleasedEvent"/> works correctly.
	/// </summary>
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

	/// <summary>
	/// Tests that publishing a <see cref="LockRenewedEvent"/> works correctly.
	/// </summary>
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

	/// <summary>
	/// Tests that publishing a <see cref="LockFailedEvent"/> works correctly.
	/// </summary>
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

	/// <summary>
	/// Tests that concurrent publishing operations all succeed without race conditions.
	/// </summary>
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

	/// <summary>
	/// Tests that concurrent subscribers and publishers maintain consistency and all handlers are invoked.
	/// </summary>
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