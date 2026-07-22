#nullable enable
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SarmKadan.DistributedLock.Events;
using SarmKadan.DistributedLock.Enums;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Unit tests for <see cref="LockEventSubscriber"/> implementations.
/// Tests both <see cref="LoggingLockEventSubscriber"/> and <see cref="MetricsTrackingEventSubscriber"/> classes.
/// </summary>
public class LockEventSubscriberTests
{
    private readonly InMemoryLockEventPublisher _publisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="LockEventSubscriberTests"/> class.
    /// </summary>
    public LockEventSubscriberTests()
    {
        _publisher = new InMemoryLockEventPublisher(NullLogger<InMemoryLockEventPublisher>.Instance);
    }

    // -------------------------------------------------------------------------
    // LoggingLockEventSubscriber Tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests that LoggingLockEventSubscriber constructor throws <see cref="ArgumentNullException"/> when null logger is provided.
    /// </summary>
    [Fact]
    public void LoggingLockEventSubscriber_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new LoggingLockEventSubscriber(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    /// <summary>
    /// Tests that LoggingLockEventSubscriber can be instantiated with valid logger.
    /// </summary>
    [Fact]
    public void LoggingLockEventSubscriber_WithValidLogger_CreatesInstance()
    {
        // Act
        var subscriber = new LoggingLockEventSubscriber(NullLogger<LoggingLockEventSubscriber>.Instance);

        // Assert
        subscriber.Should().NotBeNull();
    }

    /// <summary>
    /// Tests that LoggingLockEventSubscriber.RegisterAsync successfully registers all event handlers.
    /// </summary>
    [Fact]
    public async Task LoggingLockEventSubscriber_RegisterAsync_RegistersAllHandlers()
    {
        // Arrange
        var subscriber = new LoggingLockEventSubscriber(NullLogger<LoggingLockEventSubscriber>.Instance);

        // Act
        await subscriber.RegisterAsync(_publisher);

        // Assert
        var count = _publisher.GetSubscriberCount<LockAcquiredEvent>();
        count.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Tests that LoggingLockEventSubscriber handles LockAcquiredEvent without throwing.
    /// </summary>
    [Fact]
    public async Task LoggingLockEventSubscriber_HandlesLockAcquiredEvent()
    {
        // Arrange
        var subscriber = new LoggingLockEventSubscriber(NullLogger<LoggingLockEventSubscriber>.Instance);
        await subscriber.RegisterAsync(_publisher);
        var @event = new LockAcquiredEvent("test-lock", "owner-1", LockStatus.Held);

        // Act & Assert - should not throw
        await _publisher.Invoking(p => p.PublishAsync(@event)).Should().NotThrowAsync();
    }

    /// <summary>
    /// Tests that LoggingLockEventSubscriber handles LockReleasedEvent without throwing.
    /// </summary>
    [Fact]
    public async Task LoggingLockEventSubscriber_HandlesLockReleasedEvent()
    {
        // Arrange
        var subscriber = new LoggingLockEventSubscriber(NullLogger<LoggingLockEventSubscriber>.Instance);
        await subscriber.RegisterAsync(_publisher);
        var @event = new LockReleasedEvent("test-lock", "owner-1");

        // Act & Assert - should not throw
        await _publisher.Invoking(p => p.PublishAsync(@event)).Should().NotThrowAsync();
    }

    /// <summary>
    /// Tests that LoggingLockEventSubscriber handles LockExpiredEvent without throwing.
    /// </summary>
    [Fact]
    public async Task LoggingLockEventSubscriber_HandlesLockExpiredEvent()
    {
        // Arrange
        var subscriber = new LoggingLockEventSubscriber(NullLogger<LoggingLockEventSubscriber>.Instance);
        await subscriber.RegisterAsync(_publisher);
        var @event = new LockExpiredEvent
        {
            LockId = "test-lock",
            LockName = "test-lock",
            OwnerId = "owner-1",
            ExpiredAt = DateTime.UtcNow,
            TotalDuration = TimeSpan.FromSeconds(30)
        };

        // Act & Assert - should not throw
        await _publisher.Invoking(p => p.PublishAsync(@event)).Should().NotThrowAsync();
    }

    /// <summary>
    /// Tests that LoggingLockEventSubscriber handles LockRenewedEvent without throwing.
    /// </summary>
    [Fact]
    public async Task LoggingLockEventSubscriber_HandlesLockRenewedEvent()
    {
        // Arrange
        var subscriber = new LoggingLockEventSubscriber(NullLogger<LoggingLockEventSubscriber>.Instance);
        await subscriber.RegisterAsync(_publisher);
        var @event = new LockRenewedEvent("test-lock", "owner-1");

        // Act & Assert - should not throw
        await _publisher.Invoking(p => p.PublishAsync(@event)).Should().NotThrowAsync();
    }

    /// <summary>
    /// Tests that LoggingLockEventSubscriber handles LockAcquisitionFailedEvent without throwing.
    /// </summary>
    [Fact]
    public async Task LoggingLockEventSubscriber_HandlesLockAcquisitionFailedEvent()
    {
        // Arrange
        var subscriber = new LoggingLockEventSubscriber(NullLogger<LoggingLockEventSubscriber>.Instance);
        await subscriber.RegisterAsync(_publisher);
        var @event = new LockAcquisitionFailedEvent
        {
            LockName = "test-lock",
            RequesterId = "requester-1",
            Reason = "Contention detected"
        };

        // Act & Assert - should not throw
        await _publisher.Invoking(p => p.PublishAsync(@event)).Should().NotThrowAsync();
    }

    /// <summary>
    /// Tests that LoggingLockEventSubscriber handles LockContentionEvent without throwing.
    /// </summary>
    [Fact]
    public async Task LoggingLockEventSubscriber_HandlesLockContentionEvent()
    {
        // Arrange
        var subscriber = new LoggingLockEventSubscriber(NullLogger<LoggingLockEventSubscriber>.Instance);
        await subscriber.RegisterAsync(_publisher);
        var @event = new LockContentionEvent
        {
            LockName = "test-lock",
            ContentionLevel = 5,
            ContentionDetectedAt = DateTime.UtcNow,
            CompetingParties = new List<string> { "party-1", "party-2" }
        };

        // Act & Assert - should not throw
        await _publisher.Invoking(p => p.PublishAsync(@event)).Should().NotThrowAsync();
    }

    /// <summary>
    /// Tests that LoggingLockEventSubscriber handles LockErrorEvent without throwing.
    /// </summary>
    [Fact]
    public async Task LoggingLockEventSubscriber_HandlesLockErrorEvent()
    {
        // Arrange
        var subscriber = new LoggingLockEventSubscriber(NullLogger<LoggingLockEventSubscriber>.Instance);
        await subscriber.RegisterAsync(_publisher);
        var @event = new LockErrorEvent
        {
            LockId = "test-lock",
            OperationType = "Acquire",
            ErrorMessage = "Failed to acquire lock"
        };

        // Act & Assert - should not throw
        await _publisher.Invoking(p => p.PublishAsync(@event)).Should().NotThrowAsync();
    }

    // -------------------------------------------------------------------------
    // MetricsTrackingEventSubscriber Tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests that MetricsTrackingEventSubscriber constructor throws <see cref="ArgumentNullException"/> when null logger is provided.
    /// </summary>
    [Fact]
    public void MetricsTrackingEventSubscriber_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new MetricsTrackingEventSubscriber(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    /// <summary>
    /// Tests that MetricsTrackingEventSubscriber can be instantiated with valid logger.
    /// </summary>
    [Fact]
    public void MetricsTrackingEventSubscriber_WithValidLogger_CreatesInstance()
    {
        // Act
        var subscriber = new MetricsTrackingEventSubscriber(NullLogger<MetricsTrackingEventSubscriber>.Instance);

        // Assert
        subscriber.Should().NotBeNull();
    }

    /// <summary>
    /// Tests that MetricsTrackingEventSubscriber.RegisterAsync successfully registers event handlers.
    /// </summary>
    [Fact]
    public async Task MetricsTrackingEventSubscriber_RegisterAsync_RegistersHandlers()
    {
        // Arrange
        var subscriber = new MetricsTrackingEventSubscriber(NullLogger<MetricsTrackingEventSubscriber>.Instance);

        // Act
        await subscriber.RegisterAsync(_publisher);

        // Assert
        var acquiredCount = _publisher.GetSubscriberCount<LockAcquiredEvent>();
        var releasedCount = _publisher.GetSubscriberCount<LockReleasedEvent>();
        var failedCount = _publisher.GetSubscriberCount<LockAcquisitionFailedEvent>();
        var contentionCount = _publisher.GetSubscriberCount<LockContentionEvent>();

        acquiredCount.Should().BeGreaterThan(0);
        releasedCount.Should().BeGreaterThan(0);
        failedCount.Should().BeGreaterThan(0);
        contentionCount.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Tests that MetricsTrackingEventSubscriber tracks lock acquisitions correctly.
    /// </summary>
    [Fact]
    public async Task MetricsTrackingEventSubscriber_TracksAcquisitions()
    {
        // Arrange
        var subscriber = new MetricsTrackingEventSubscriber(NullLogger<MetricsTrackingEventSubscriber>.Instance);
        await subscriber.RegisterAsync(_publisher);

        // Act - publish multiple acquisition events
        for (int i = 0; i < 5; i++)
        {
            var @event = new LockAcquiredEvent($"lock-{i}", $"owner-{i}", LockStatus.Held);
            await _publisher.PublishAsync(@event);
        }

        // Assert
        var metrics = subscriber.GetMetrics();
        metrics.Acquisitions.Should().Be(5);
        metrics.Releases.Should().Be(0);
        metrics.Failures.Should().Be(0);
        metrics.ContentionEvents.Should().Be(0);
        metrics.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Tests that MetricsTrackingEventSubscriber tracks lock releases correctly.
    /// </summary>
    [Fact]
    public async Task MetricsTrackingEventSubscriber_TracksReleases()
    {
        // Arrange
        var subscriber = new MetricsTrackingEventSubscriber(NullLogger<MetricsTrackingEventSubscriber>.Instance);
        await subscriber.RegisterAsync(_publisher);

        // Act - publish multiple release events
        for (int i = 0; i < 3; i++)
        {
            var @event = new LockReleasedEvent($"lock-{i}", $"owner-{i}");
            await _publisher.PublishAsync(@event);
        }

        // Assert
        var metrics = subscriber.GetMetrics();
        metrics.Releases.Should().Be(3);
        metrics.Acquisitions.Should().Be(0);
    }

    /// <summary>
    /// Tests that MetricsTrackingEventSubscriber tracks lock acquisition failures correctly.
    /// </summary>
    [Fact]
    public async Task MetricsTrackingEventSubscriber_TracksFailures()
    {
        // Arrange
        var subscriber = new MetricsTrackingEventSubscriber(NullLogger<MetricsTrackingEventSubscriber>.Instance);
        await subscriber.RegisterAsync(_publisher);

        // Act - publish multiple failure events
        for (int i = 0; i < 7; i++)
        {
            var @event = new LockAcquisitionFailedEvent
            {
                LockName = $"lock-{i}",
                RequesterId = $"requester-{i}",
                Reason = "Contention"
            };
            await _publisher.PublishAsync(@event);
        }

        // Assert
        var metrics = subscriber.GetMetrics();
        metrics.Failures.Should().Be(7);
        metrics.Acquisitions.Should().Be(0);
    }

    /// <summary>
    /// Tests that MetricsTrackingEventSubscriber tracks lock contention events correctly.
    /// </summary>
    [Fact]
    public async Task MetricsTrackingEventSubscriber_TracksContentionEvents()
    {
        // Arrange
        var subscriber = new MetricsTrackingEventSubscriber(NullLogger<MetricsTrackingEventSubscriber>.Instance);
        await subscriber.RegisterAsync(_publisher);

        // Act - publish multiple contention events
        for (int i = 0; i < 4; i++)
        {
            var @event = new LockContentionEvent
            {
                LockName = $"lock-{i}",
                ContentionLevel = i + 1,
                ContentionDetectedAt = DateTime.UtcNow,
                CompetingParties = new List<string> { $"party-{i}" }
            };
            await _publisher.PublishAsync(@event);
        }

        // Assert
        var metrics = subscriber.GetMetrics();
        metrics.ContentionEvents.Should().Be(4);
        metrics.Acquisitions.Should().Be(0);
    }

    /// <summary>
    /// Tests that MetricsTrackingEventSubscriber metrics are thread-safe (concurrent increments).
    /// </summary>
    [Fact]
    public async Task MetricsTrackingEventSubscriber_MetricsAreThreadSafe()
    {
        // Arrange
        var subscriber = new MetricsTrackingEventSubscriber(NullLogger<MetricsTrackingEventSubscriber>.Instance);
        await subscriber.RegisterAsync(_publisher);

        // Act - publish events concurrently
        var tasks = new List<Task>();
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var @event = new LockAcquiredEvent($"lock-{Guid.NewGuid()}", $"owner-{Guid.NewGuid()}", LockStatus.Held);
                await _publisher.PublishAsync(@event);
            }));
        }
        await Task.WhenAll(tasks);

        // Assert
        var metrics = subscriber.GetMetrics();
        metrics.Acquisitions.Should().Be(20);
    }

    /// <summary>
    /// Tests that MetricsTrackingEventSubscriber metrics include timestamp.
    /// </summary>
    [Fact]
    public async Task MetricsTrackingEventSubscriber_MetricsIncludeTimestamp()
    {
        // Arrange
        var subscriber = new MetricsTrackingEventSubscriber(NullLogger<MetricsTrackingEventSubscriber>.Instance);
        await subscriber.RegisterAsync(_publisher);

        // Act
        var metrics = subscriber.GetMetrics();

        // Assert
        metrics.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        metrics.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
    }

    /// <summary>
    /// Tests that MetricsTrackingEventSubscriber returns zero metrics when no events are processed.
    /// </summary>
    [Fact]
    public void MetricsTrackingEventSubscriber_ZeroMetrics_WhenNoEventsProcessed()
    {
        // Arrange
        var subscriber = new MetricsTrackingEventSubscriber(NullLogger<MetricsTrackingEventSubscriber>.Instance);

        // Act
        var metrics = subscriber.GetMetrics();

        // Assert
        metrics.Acquisitions.Should().Be(0);
        metrics.Releases.Should().Be(0);
        metrics.Failures.Should().Be(0);
        metrics.ContentionEvents.Should().Be(0);
        metrics.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Tests that MetricsTrackingEventSubscriber.GetMetrics returns new instance each time.
    /// </summary>
    [Fact]
    public async Task MetricsTrackingEventSubscriber_GetMetrics_ReturnsNewInstance()
    {
        // Arrange
        var subscriber = new MetricsTrackingEventSubscriber(NullLogger<MetricsTrackingEventSubscriber>.Instance);
        await subscriber.RegisterAsync(_publisher);

        // Act - get metrics multiple times
        var metrics1 = subscriber.GetMetrics();
        var metrics2 = subscriber.GetMetrics();

        // Assert - should be different instances
        metrics1.Should().NotBeSameAs(metrics2);
        metrics1.Timestamp.Should().NotBe(metrics2.Timestamp);
    }

    /// <summary>
    /// Tests that MetricsTrackingEventSubscriber handles mixed event types correctly.
    /// </summary>
    [Fact]
    public async Task MetricsTrackingEventSubscriber_HandlesMixedEventTypes()
    {
        // Arrange
        var subscriber = new MetricsTrackingEventSubscriber(NullLogger<MetricsTrackingEventSubscriber>.Instance);
        await subscriber.RegisterAsync(_publisher);

        // Act - publish mixed event types
        await _publisher.PublishAsync(new LockAcquiredEvent("lock-1", "owner-1", LockStatus.Held));
        await _publisher.PublishAsync(new LockReleasedEvent("lock-1", "owner-1"));
        await _publisher.PublishAsync(new LockAcquiredEvent("lock-2", "owner-2", LockStatus.Held));
        await _publisher.PublishAsync(new LockAcquisitionFailedEvent
        {
            LockName = "lock-3",
            RequesterId = "requester-1",
            Reason = "Timeout"
        });
        await _publisher.PublishAsync(new LockContentionEvent
        {
            LockName = "lock-4",
            ContentionLevel = 3,
            ContentionDetectedAt = DateTime.UtcNow,
            CompetingParties = new List<string> { "party-1", "party-2" }
        });

        // Assert
        var metrics = subscriber.GetMetrics();
        metrics.Acquisitions.Should().Be(2);
        metrics.Releases.Should().Be(1);
        metrics.Failures.Should().Be(1);
        metrics.ContentionEvents.Should().Be(1);
    }
}
