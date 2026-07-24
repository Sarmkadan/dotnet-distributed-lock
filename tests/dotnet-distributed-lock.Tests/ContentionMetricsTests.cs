#nullable enable
using System;
using FluentAssertions;
using SarmKadan.DistributedLock.Models;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

public class ContentionMetricsTests
{
    private const string LockKey = "test-lock";

    [Fact]
    public void Constructor_Throws_WhenLockKeyIsNull()
    {
        // Arrange
        string? nullKey = null;

        // Act
        Action act = () => new ContentionMetrics(nullKey!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("lockKey");
    }

    [Fact]
    public void RecordWaiterAdded_UpdatesCounters()
    {
        // Arrange
        var metrics = new ContentionMetrics(LockKey);

        // Act
        metrics.RecordWaiterAdded(); // 1st waiter
        metrics.RecordWaiterAdded(); // 2nd waiter

        // Assert
        metrics.CurrentWaiters.Should().Be(2);
        metrics.PeakWaiters.Should().Be(2);
        metrics.TotalContentionEvents.Should().Be(1);
        metrics.TotalWaiterRegistrations.Should().Be(2);
        metrics.TotalWaiters.Should().Be(2);
    }

    [Fact]
    public void RecordWaiterRemoved_UpdatesCountersAndWaitTime()
    {
        // Arrange
        var metrics = new ContentionMetrics(LockKey);
        metrics.RecordWaiterAdded();
        metrics.RecordWaiterAdded();

        // Act
        metrics.RecordWaiterRemoved(150.0); // first waiter leaves
        metrics.RecordWaiterRemoved(50.0);  // second waiter leaves

        // Assert
        metrics.CurrentWaiters.Should().Be(0);
        metrics.TotalWaiterRegistrations.Should().Be(2);
        metrics.TotalWaiters.Should().Be(2);
        metrics.DeadlocksDetected.Should().Be(0);
        metrics.AverageWaitTimeMs.Should().BeApproximately(100.0, 0.01);
    }

    [Fact]
    public void RecordDeadlock_IncrementsCounter()
    {
        // Arrange
        var metrics = new ContentionMetrics(LockKey);

        // Act
        metrics.RecordDeadlock();
        metrics.RecordDeadlock();

        // Assert
        metrics.DeadlocksDetected.Should().Be(2);
    }

    [Fact]
    public void ToString_IncludesKeyAndStats()
    {
        // Arrange
        var metrics = new ContentionMetrics(LockKey);
        metrics.RecordWaiterAdded();
        metrics.RecordWaiterRemoved(200.0);

        // Act
        var str = metrics.ToString();

        // Assert
        str.Should().Contain($"Key={LockKey}");
        str.Should().Contain("Waiters=0");
        str.Should().Contain("Peak=1");
        str.Should().Contain("Deadlocks=0");
        str.Should().Contain("AvgWait=200.00ms");
    }

    [Fact]
    public void RecordWaiterRemoved_NegativeWaitTime_ClampedToZero()
    {
        // Arrange
        var metrics = new ContentionMetrics(LockKey);
        metrics.RecordWaiterAdded();

        // Act
        metrics.RecordWaiterRemoved(-10.0);

        // Assert
        metrics.AverageWaitTimeMs.Should().Be(0.0);
    }

    [Fact]
    public void RecordWaiterRemoved_NoWaiters_DoesNotThrow()
    {
        // Arrange
        var metrics = new ContentionMetrics(LockKey);

        // Act
        Action act = () => metrics.RecordWaiterRemoved(100.0);

        // Assert
        act.Should().NotThrow();
        metrics.CurrentWaiters.Should().Be(0);
    }

    [Fact]
    public void LastUpdatedAt_UpdatedAfterMutation()
    {
        // Arrange
        var metrics = new ContentionMetrics(LockKey);
        var created = metrics.CreatedAt;
        var initialLast = metrics.LastUpdatedAt;

        // Act
        metrics.RecordWaiterAdded();

        // Assert
        metrics.LastUpdatedAt.Should().BeAfter(initialLast);
        metrics.LastUpdatedAt.Should().BeAfter(created);
    }
}
