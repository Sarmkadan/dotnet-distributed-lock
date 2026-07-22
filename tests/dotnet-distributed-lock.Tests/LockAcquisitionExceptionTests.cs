#nullable enable
using System;
using FluentAssertions;
using SarmKadan.DistributedLock.Exceptions;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Unit tests for <see cref="LockAcquisitionException"/>.
/// </summary>
public class LockAcquisitionExceptionTests
{
    [Fact]
    public void Constructor_WithLockKeyTimeoutAndDefaultRetryCount_SetsPropertiesCorrectly()
    {
        // Arrange
        var lockKey = "my-lock";
        var timeout = TimeSpan.FromSeconds(12.5);

        // Act
        var ex = new LockAcquisitionException(lockKey, timeout);

        // Assert
        ex.LockKey.Should().Be(lockKey);
        ex.Timeout.Should().Be(timeout);
        ex.RetryCount.Should().Be(0);
        ex.Message.Should()
            .Contain($"Failed to acquire lock '{lockKey}'")
            .And.Contain($"{timeout.TotalSeconds}s")
            .And.Contain("after 0 retries");
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithAllParameters_IncludesInnerExceptionAndSetsAllProperties()
    {
        // Arrange
        var lockKey = "another-lock";
        var timeout = TimeSpan.FromMinutes(1);
        var retryCount = 3;
        var inner = new InvalidOperationException("inner reason");

        // Act
        var ex = new LockAcquisitionException(lockKey, timeout, retryCount, inner);

        // Assert
        ex.LockKey.Should().Be(lockKey);
        ex.Timeout.Should().Be(timeout);
        ex.RetryCount.Should().Be(retryCount);
        ex.InnerException.Should().BeSameAs(inner);
        ex.Message.Should()
            .Contain($"Failed to acquire lock '{lockKey}'")
            .And.Contain($"{timeout.TotalSeconds}s")
            .And.Contain($"after {retryCount} retries");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithVariousLockKey_HandlesNullOrEmptyValues(string? lockKey)
    {
        // Arrange
        var timeout = TimeSpan.FromSeconds(5);
        var expectedKey = lockKey; // may be null or empty

        // Act
        var ex = new LockAcquisitionException(lockKey!, timeout);

        // Assert
        ex.LockKey.Should().Be(expectedKey);
        // Message should still be well‑formed – the interpolated lock key will appear as empty string if null/whitespace
        ex.Message.Should().Contain("Failed to acquire lock");
        ex.Message.Should().Contain($"{timeout.TotalSeconds}s");
    }

    [Fact]
    public void Constructor_WithZeroTimeout_ProducesCorrectMessage()
    {
        // Arrange
        var lockKey = "zero-timeout";
        var timeout = TimeSpan.Zero;

        // Act
        var ex = new LockAcquisitionException(lockKey, timeout);

        // Assert
        ex.Timeout.Should().Be(TimeSpan.Zero);
        ex.Message.Should().Contain($"{timeout.TotalSeconds}s"); // should be "0"
    }

    [Fact]
    public void ToString_IncludesExceptionTypeAndMessage()
    {
        // Arrange
        var lockKey = "to-string-lock";
        var timeout = TimeSpan.FromSeconds(2);
        var ex = new LockAcquisitionException(lockKey, timeout, 1);

        // Act
        var result = ex.ToString();

        // Assert
        result.Should().Contain(nameof(LockAcquisitionException));
        result.Should().Contain(ex.Message);
    }
}
