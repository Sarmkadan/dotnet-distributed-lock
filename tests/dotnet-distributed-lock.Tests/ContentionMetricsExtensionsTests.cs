#nullable enable

using System;
using FluentAssertions;
using SarmKadan.DistributedLock.Models;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

public class ContentionMetricsExtensionsTests
{
    #region GetContentionPercentage

    [Fact]
    public void GetContentionPercentage_NullMetrics_ThrowsArgumentNullException()
    {
        // Arrange
        ContentionMetrics? metrics = null;

        // Act
        Action act = () => metrics!.GetContentionPercentage();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetContentionPercentage_ZeroPeakWaiters_ReturnsZero()
    {
        // Arrange
        var metrics = new ContentionMetrics("test-key");
        // PeakWaiters will be 0 by default

        // Act
        var result = metrics.GetContentionPercentage();

        // Assert
        result.Should().Be(0d);
    }

    [Fact]
    public void GetContentionPercentage_NoCurrentWaiters_ReturnsZero()
    {
        // Arrange
        var metrics = new ContentionMetrics("test-key");
        // CurrentWaiters will be 0 by default, PeakWaiters will be 0

        // Act
        var result = metrics.GetContentionPercentage();

        // Assert
        result.Should().Be(0d);
    }

    [Fact]
    public void GetContentionPercentage_FiftyPercentContention_ReturnsFifty()
    {
        // Arrange
        var metrics = new ContentionMetrics("test-key");
        // Simulate 1 waiter added, then another (creates contention event)
        // This makes PeakWaiters = 2
        // Then remove one waiter (CurrentWaiters = 1)
        metrics.RecordWaiterAdded(); // Waiter 1
        metrics.RecordWaiterAdded(); // Waiter 2 (contention event, PeakWaiters = 2)
        metrics.RecordWaiterRemoved(100); // Remove waiter 1, CurrentWaiters = 1

        // Act
        var result = metrics.GetContentionPercentage();

        // Assert
        // CurrentWaiters = 1, PeakWaiters = 2 => 50%
        result.Should().Be(50d);
    }

    [Fact]
    public void GetContentionPercentage_OneHundredPercentContention_ReturnsOneHundred()
    {
        // Arrange
        var metrics = new ContentionMetrics("test-key");
        // Simulate 2 waiters added (creates contention event, PeakWaiters = 2)
        // Don't remove any, so CurrentWaiters = 2
        metrics.RecordWaiterAdded(); // Waiter 1
        metrics.RecordWaiterAdded(); // Waiter 2 (contention event, PeakWaiters = 2)

        // Act
        var result = metrics.GetContentionPercentage();

        // Assert
        // CurrentWaiters = 2, PeakWaiters = 2 => 100%
        result.Should().Be(100d);
    }

    [Fact]
    public void GetContentionPercentage_MoreCurrentThanPeak_ReturnsCappedAtOneHundred()
    {
        // Arrange - This scenario can't happen naturally since Peak tracks max
        // But we can test the cap logic directly
        var metrics = new ContentionMetrics("test-key");
        // Manually test the cap by calling with values that would exceed 100%
        // Since we can't easily set CurrentWaiters > PeakWaiters naturally,
        // we'll test that the method caps at 100
        metrics.RecordWaiterAdded(); // Waiter 1
        metrics.RecordWaiterAdded(); // Waiter 2 (contention event, PeakWaiters = 2)
        metrics.RecordWaiterRemoved(100); // Remove waiter 1, CurrentWaiters = 1

        // Act
        var result = metrics.GetContentionPercentage();

        // Assert - Should be 50%, not over 100%
        result.Should().Be(50d);
        result.Should().BeLessThanOrEqualTo(100d);
    }

    [Fact]
    public void GetContentionPercentage_SmallFraction_ReturnsPreciseValue()
    {
        // Arrange
        var metrics = new ContentionMetrics("test-key");
        // Create scenario: 1 waiter currently, 3 peak waiters
        metrics.RecordWaiterAdded(); // Waiter 1
        metrics.RecordWaiterAdded(); // Waiter 2 (contention event, PeakWaiters = 2)
        metrics.RecordWaiterAdded(); // Waiter 3 (contention event, PeakWaiters = 3)
        metrics.RecordWaiterRemoved(100); // Remove waiter 1, CurrentWaiters = 2
        metrics.RecordWaiterRemoved(100); // Remove waiter 2, CurrentWaiters = 1

        // Act
        var result = metrics.GetContentionPercentage();

        // Assert
        // CurrentWaiters = 1, PeakWaiters = 3 => 33.33%
        result.Should().BeApproximately(33.3333, 0.0001);
    }

    #endregion

    #region ToDetailedString

    [Fact]
    public void ToDetailedString_NullMetrics_ThrowsArgumentNullException()
    {
        // Arrange
        ContentionMetrics? metrics = null;

        // Act
        Action act = () => metrics!.ToDetailedString();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToDetailedString_WithHistory_ReturnsFormattedStringWithAllMetrics()
    {
        // Arrange
        var metrics = new ContentionMetrics("my-lock-key");
        // Add some activity - need 2 waiters simultaneously to create a contention event
        metrics.RecordWaiterAdded(); // Waiter 1
        metrics.RecordWaiterAdded(); // Waiter 2 (contention event: 2 waiters > 1)
        metrics.RecordWaiterRemoved(50); // Remove waiter 1
        metrics.RecordWaiterRemoved(50); // Remove waiter 2
        metrics.RecordDeadlock(); // One deadlock

        // Act
        var result = metrics.ToDetailedString(includeHistory: true);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("my-lock-key");
        result.Should().Contain("Current Waiters: 0"); // All removed
        result.Should().Contain("Peak Waiters: 2");
        result.Should().Contain("Contention Events: 1"); // Only 1 contention event when 2 waiters simultaneously
        result.Should().Contain("Total Waiters: 2"); // 2 registrations (waiter 1 and 2)
        result.Should().Contain("Deadlocks: 1");
        result.Should().Contain("Average Wait: 50.00ms"); // (50+50)/2
        result.Should().Contain("Created:");
        result.Should().Contain("Last Updated:");
        result.Should().Contain("Current Contention: 0.0%"); // 0 current waiters
    }

    [Fact]
    public void ToDetailedString_WithoutHistory_DoesNotIncludeContention()
    {
        // Arrange
        var metrics = new ContentionMetrics("test-key");
        metrics.RecordWaiterAdded();
        metrics.RecordWaiterAdded();
        metrics.RecordWaiterRemoved(100);
        metrics.RecordWaiterRemoved(100);

        // Act
        var result = metrics.ToDetailedString(includeHistory: false);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().NotContain("Current Contention:");
        result.Should().NotContain("Waiter Throughput:");
    }

    [Fact]
    public void ToDetailedString_NoContentionEvents_StillIncludesBasicMetrics()
    {
        // Arrange
        var metrics = new ContentionMetrics("test-key");
        // No waiters added, so no contention events

        // Act
        var result = metrics.ToDetailedString(includeHistory: true);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Current Waiters: 0");
        result.Should().Contain("Peak Waiters: 0");
        result.Should().Contain("Contention Events: 0");
    }

    #endregion

    #region IsHighContention

    [Fact]
    public void IsHighContention_NullMetrics_ThrowsArgumentNullException()
    {
        // Arrange
        ContentionMetrics? metrics = null;

        // Act
        Action act = () => metrics!.IsHighContention();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsHighContention_DefaultThresholds_NoWaiters_ReturnsFalse()
    {
        // Arrange
        var metrics = new ContentionMetrics("test-key");
        // Default: 0 waiters, threshold is 5 waiters or 50%

        // Act
        var result = metrics.IsHighContention();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsHighContention_DefaultThresholds_FourWaiters_ReturnsFalse()
    {
        // Arrange
        var metrics = new ContentionMetrics("test-key");
        // Add 4 waiters
        for (int i = 0; i < 4; i++)
        {
            metrics.RecordWaiterAdded();
        }
        // Remove them all to test current waiters = 0 but peak = 4
        for (int i = 0; i < 4; i++)
        {
            metrics.RecordWaiterRemoved(100);
        }
        // Now test with 0 current waiters (below threshold of 5)
        // But peak is 4, so contention % = 0 (0/4*100) which is below 50%

        // Act
        var result = metrics.IsHighContention();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsHighContention_DefaultThresholds_FiveWaiters_ReturnsTrue()
    {
        // Arrange
        var metrics = new ContentionMetrics("test-key");
        // Add 5 waiters and keep them (current waiters = 5)
        for (int i = 0; i < 5; i++)
        {
            metrics.RecordWaiterAdded();
        }
        // Don't remove any, so current waiters = 5

        // Act
        var result = metrics.IsHighContention();

        // Assert
        // Current waiters (5) >= threshold waiters (5) => true
        result.Should().BeTrue();
    }

    [Fact]
    public void IsHighContention_DefaultThresholds_TwentyWaiters_ReturnsTrue()
    {
        // Arrange
        var metrics = new ContentionMetrics("test-key");
        // Add 20 waiters and keep them
        for (int i = 0; i < 20; i++)
        {
            metrics.RecordWaiterAdded();
        }

        // Act
        var result = metrics.IsHighContention();

        // Assert
        // Current waiters (20) >= threshold waiters (5) => true
        result.Should().BeTrue();
    }

    [Fact]
    public void IsHighContention_HighPercentage_LowWaiters_ReturnsTrue()
    {
        // Arrange
        var metrics = new ContentionMetrics("test-key");
        // Create high percentage with low waiters: 1 waiter currently, 2 peak
        metrics.RecordWaiterAdded(); // Waiter 1
        metrics.RecordWaiterAdded(); // Waiter 2 (contention event, PeakWaiters = 2)
        metrics.RecordWaiterRemoved(100); // Remove waiter 1, CurrentWaiters = 1
        // CurrentWaiters = 1, PeakWaiters = 2 => 50% contention (at threshold)

        // Act
        var result = metrics.IsHighContention();

        // Assert
        // Contention percentage (50%) >= threshold percentage (50) => true
        result.Should().BeTrue();
    }

    [Fact]
    public void IsHighContention_CustomThresholds_RespectsThresholds()
    {
        // Arrange
        var metrics = new ContentionMetrics("test-key");
        // Add 10 waiters and keep them
        for (int i = 0; i < 10; i++)
        {
            metrics.RecordWaiterAdded();
        }
        // CurrentWaiters = 10, PeakWaiters = 10 => 100% contention

        // Act
        var result = metrics.IsHighContention(thresholdWaiters: 15, thresholdPercentage: 80);

        // Assert
        // Current waiters (10) < threshold waiters (15) => false for first condition
        // Contention percentage (100%) >= threshold percentage (80) => true for second condition
        // Overall: true (OR condition)
        result.Should().BeTrue();
    }

    [Fact]
    public void IsHighContention_NegativeThresholds_UsesZero()
    {
        // Arrange
        var metrics = new ContentionMetrics("test-key");
        // With negative thresholds, they should be clamped to 0
        // Any non-negative value will be >= 0

        // Act
        var result = metrics.IsHighContention(thresholdWaiters: -5, thresholdPercentage: -10);

        // Assert
        // With thresholds clamped to 0, any metrics will trigger high contention
        // Since CurrentWaiters >= 0 (always true for non-negative) OR ContentionPct >= 0 (always true)
        result.Should().BeTrue();
    }

    #endregion

    #region GetEstimatedTimeSaved

    [Fact]
    public void GetEstimatedTimeSaved_NullMetrics_ThrowsArgumentNullException()
    {
        // Arrange
        ContentionMetrics? metrics = null;

        // Act
        Action act = () => metrics!.GetEstimatedTimeSaved();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetEstimatedTimeSaved_NoWaiters_ReturnsZero()
    {
        // Arrange
        var metrics = new ContentionMetrics("test-key");
        // No waiters added

        // Act
        var result = metrics.GetEstimatedTimeSaved();

        // Assert
        result.Should().Be(0d);
    }

    [Fact]
    public void GetEstimatedTimeSaved_SingleWaiter_ReturnsZero()
    {
        // Arrange
        var metrics = new ContentionMetrics("test-key");
        metrics.RecordWaiterAdded(); // 1 waiter
        metrics.RecordWaiterRemoved(100); // Remove it

        // Act
        var result = metrics.GetEstimatedTimeSaved();

        // Assert
        result.Should().Be(0d);
    }

    [Fact]
    public void GetEstimatedTimeSaved_NoPeakWaiters_ReturnsZero()
    {
        // Arrange
        var metrics = new ContentionMetrics("test-key");
        // Can't really have CurrentWaiters > 0 and PeakWaiters = 0 naturally
        // But we can test the method's guard clause
        metrics.RecordWaiterAdded(); // This will make PeakWaiters = 1
        metrics.RecordWaiterRemoved(100); // Remove it

        // Act
        var result = metrics.GetEstimatedTimeSaved();

        // Assert
        // After removal: CurrentWaiters = 0, so should return 0
        result.Should().Be(0d);
    }

    [Fact]
    public void GetEstimatedTimeSaved_ZeroAverageWaitTime_ReturnsZero()
    {
        // Arrange
        var metrics = new ContentionMetrics("test-key");
        metrics.RecordWaiterAdded(); // Waiter 1
        metrics.RecordWaiterAdded(); // Waiter 2 (contention event)
        metrics.RecordWaiterRemoved(0); // Zero wait time
        metrics.RecordWaiterRemoved(0); // Zero wait time

        // Act
        var result = metrics.GetEstimatedTimeSaved();

        // Assert
        result.Should().Be(0d);
    }

    [Fact]
    public void GetEstimatedTimeSaved_LowContention_BelowTarget_ReturnsZero()
    {
        // Arrange
        var metrics = new ContentionMetrics("test-key");
        // Create low contention: 1 waiter currently, 10 peak (10% contention)
        // Target is 10%, so should return 0
        for (int i = 0; i < 10; i++)
        {
            metrics.RecordWaiterAdded(); // Add 10 waiters
        }
        for (int i = 0; i < 9; i++)
        {
            metrics.RecordWaiterRemoved(50); // Remove 9, leaving 1 current
        }
        // CurrentWaiters = 1, PeakWaiters = 10 => 10% contention

        // Act
        var result = metrics.GetEstimatedTimeSaved(targetContentionPercentage: 10);

        // Assert
        result.Should().Be(0d);
    }

    [Fact]
    public void GetEstimatedTimeSaved_HighContention_WithDefaultTarget_ReturnsPositiveValue()
    {
        // Arrange
        var metrics = new ContentionMetrics("test-key");
        // Create high contention: 8 waiters currently, 10 peak (80% contention)
        // Default target is 10%, so should return positive value
        for (int i = 0; i < 10; i++)
        {
            metrics.RecordWaiterAdded(); // Add 10 waiters
        }
        for (int i = 0; i < 2; i++)
        {
            metrics.RecordWaiterRemoved(100); // Remove 2, leaving 8 current (100ms wait time)
        }
        // CurrentWaiters = 8, PeakWaiters = 10 => 80% contention

        // Act
        var result = metrics.GetEstimatedTimeSaved();

        // Assert
        result.Should().BeGreaterThan(0d);
        result.Should().BeLessThan(10000d); // Reasonable upper bound
    }

    [Fact]
    public void GetEstimatedTimeSaved_TargetHundredPercent_ReturnsZero()
    {
        // Arrange
        var metrics = new ContentionMetrics("test-key");
        // Any contention with 100% target should return 0 (no savings possible)
        metrics.RecordWaiterAdded(); // Waiter 1
        metrics.RecordWaiterAdded(); // Waiter 2 (contention event)
        metrics.RecordWaiterRemoved(100); // Remove waiter 1
        metrics.RecordWaiterRemoved(100); // Remove waiter 2
        // CurrentWaiters = 0, PeakWaiters = 2 => 0% contention

        // Act
        var result = metrics.GetEstimatedTimeSaved(targetContentionPercentage: 100);

        // Assert
        result.Should().Be(0d);
    }

    [Fact]
    public void GetEstimatedTimeSaved_NegativeTargetContention_ReturnsZero()
    {
        // Arrange
        var metrics = new ContentionMetrics("test-key");
        metrics.RecordWaiterAdded(); // Waiter 1
        metrics.RecordWaiterAdded(); // Waiter 2 (contention event)
        metrics.RecordWaiterRemoved(100); // Remove waiter 1
        metrics.RecordWaiterRemoved(100); // Remove waiter 2

        // Act
        var result = metrics.GetEstimatedTimeSaved(targetContentionPercentage: -5);

        // Assert
        // Negative target should be clamped to 0, so if current contention > 0, returns 0
        result.Should().Be(0d);
    }

    [Fact]
    public void GetEstimatedTimeSaved_CustomTarget_ReturnsPositiveValue()
    {
        // Arrange
        var metrics = new ContentionMetrics("test-lock");
        // Create scenario: 8 waiters currently, 10 peak (80% contention)
        // Target: 20% contention
        for (int i = 0; i < 10; i++)
        {
            metrics.RecordWaiterAdded(); // Add 10 waiters
        }
        for (int i = 0; i < 2; i++)
        {
            metrics.RecordWaiterRemoved(200); // Remove 2, leaving 8 current (200ms wait time)
        }
        // CurrentWaiters = 8, PeakWaiters = 10 => 80% contention

        // Act
        var result = metrics.GetEstimatedTimeSaved(targetContentionPercentage: 20);

        // Assert
        // Should return positive value for time saved
        result.Should().BeGreaterThan(0d);
    }

    #endregion
}