#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using FluentAssertions;
using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Models;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

public class LockRequestContextExtensionsTests
{
    [Fact]
    public void HasExpired_ExpiredContext_ReturnsTrue()
    {
        // Arrange
        var context = new LockRequestContext("key", "requester")
        {
            RequestedAt = DateTime.UtcNow.AddMinutes(-10),
            RequestedDuration = TimeSpan.FromMinutes(5)
        };

        // Act
        var expired = context.HasExpired();

        // Assert
        expired.Should().BeTrue();
    }

    [Fact]
    public void HasExpired_NotExpiredContext_ReturnsFalse()
    {
        // Arrange
        var context = new LockRequestContext("key", "requester")
        {
            RequestedAt = DateTime.UtcNow.AddMinutes(-2),
            RequestedDuration = TimeSpan.FromMinutes(5)
        };

        // Act
        var expired = context.HasExpired();

        // Assert
        expired.Should().BeFalse();
    }

    [Fact]
    public void RemainingTime_ExpiredContext_ReturnsTimeSpanZero()
    {
        // Arrange
        var context = new LockRequestContext("key", "requester")
        {
            RequestedAt = DateTime.UtcNow.AddMinutes(-10),
            RequestedDuration = TimeSpan.FromMinutes(5)
        };

        // Act
        var remaining = context.RemainingTime();

        // Assert
        remaining.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void RemainingTime_ValidContext_ReturnsCorrectRemainingTime()
    {
        // Arrange
        var duration = TimeSpan.FromMinutes(5);
        var context = new LockRequestContext("key", "requester")
        {
            RequestedAt = DateTime.UtcNow.AddMinutes(-2),
            RequestedDuration = duration
        };

        // Act
        var remaining = context.RemainingTime();

        // Assert
        remaining.TotalMinutes.Should().BeApproximately(3, 0.1);
    }

    [Fact]
    public void ToDiagnosticString_IncludesAllExpectedFields()
    {
        // Arrange
        var context = new LockRequestContext("key", "requester")
        {
            RequestorName = "TestRequester",
            Successful = true,
            RetryCount = 2
        };
        context.MarkCompleted(true);
        context.AddProperty("CustomKey", "CustomValue");

        // Act
        var diagnosticString = context.ToDiagnosticString();

        // Assert
        diagnosticString.Should().Contain("RequestId: " + context.RequestId);
        diagnosticString.Should().Contain("LockKey: key");
        diagnosticString.Should().Contain("RequesterId: requester");
        diagnosticString.Should().Contain("RequestorName: TestRequester");
        diagnosticString.Should().Contain("RetryCount: 2");
        diagnosticString.Should().Contain("CustomProperties:");
        diagnosticString.Should().Contain("CustomKey: CustomValue");
    }

    [Fact]
    public void IsSuccessfulWithinDuration_SuccessfulAndWithinTime_ReturnsTrue()
    {
        // Arrange
        var context = new LockRequestContext("key", "requester")
        {
            RequestedAt = DateTime.UtcNow.AddMinutes(-2),
            RequestedDuration = TimeSpan.FromMinutes(5),
            Successful = true
        };
        context.CompletedAt = DateTime.UtcNow.AddMinutes(-1); // Finished within 1 minute of 5 minute window

        // Act
        var success = context.IsSuccessfulWithinDuration();

        // Assert
        success.Should().BeTrue();
    }

    [Fact]
    public void IsSuccessfulWithinDuration_SuccessfulButLate_ReturnsFalse()
    {
        // Arrange
        var context = new LockRequestContext("key", "requester")
        {
            RequestedAt = DateTime.UtcNow.AddMinutes(-10),
            RequestedDuration = TimeSpan.FromMinutes(5),
            Successful = true
        };
        context.CompletedAt = DateTime.UtcNow.AddMinutes(-2); // Finished 8 minutes after start, but duration was only 5

        // Act
        var success = context.IsSuccessfulWithinDuration();

        // Assert
        success.Should().BeFalse();
    }

    [Fact]
    public void GetStandardMetrics_ReturnsCorrectMetricsDictionary()
    {
        // Arrange
        var context = new LockRequestContext("key", "requester")
        {
            RequestedAt = DateTime.UtcNow,
            RequestedDuration = TimeSpan.FromSeconds(10),
            Successful = true
        };

        // Act
        var metrics = context.GetStandardMetrics();

        // Assert
        metrics["request_id"].Should().Be(context.RequestId);
        metrics["lock_key"].Should().Be("key");
        metrics["requester_id"].Should().Be("requester");
        metrics["successful"].Should().Be(true);
        metrics.Should().ContainKey("remaining_time_seconds");
    }

    [Fact]
    public void AllMethods_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        LockRequestContext? context = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => context!.HasExpired());
        Assert.Throws<ArgumentNullException>(() => context!.RemainingTime());
        Assert.Throws<ArgumentNullException>(() => context!.ToDiagnosticString());
        Assert.Throws<ArgumentNullException>(() => context!.IsSuccessfulWithinDuration());
        Assert.Throws<ArgumentNullException>(() => context!.GetStandardMetrics());
    }
}
