#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using System;
using FluentAssertions;
using SarmKadan.DistributedLock.Exceptions;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Unit tests for <see cref="LockAcquisitionExceptionExtensions"/>.
/// </summary>
public class LockAcquisitionExceptionExtensionsTests
{
    [Fact]
    public void ToDetailedErrorMessage_WithValidExceptionAndMaxRetries_ReturnsFormattedMessage()
    {
        // Arrange
        var lockKey = "test-lock-key";
        var timeout = TimeSpan.FromSeconds(5);
        var retryCount = 3;
        var maxRetries = 5;
        var exception = new LockAcquisitionException(lockKey, timeout, retryCount);

        // Act
        var result = exception.ToDetailedErrorMessage(maxRetries);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain(lockKey);
        result.Should().Contain($"{timeout.TotalSeconds}s");
        result.Should().Contain($"{retryCount}");
        result.Should().Contain($"{maxRetries}");
        result.Should().Contain("RECOMMENDATION:");
        result.Should().Contain("Original message:");
    }

    [Fact]
    public void ToDetailedErrorMessage_WhenRetryCountExceedsMaxRetries_ContainsTimeoutRecommendation()
    {
        // Arrange
        var lockKey = "timeout-lock";
        var timeout = TimeSpan.FromSeconds(1);
        var retryCount = 10;
        var maxRetries = 5;
        var exception = new LockAcquisitionException(lockKey, timeout, retryCount);

        // Act
        var result = exception.ToDetailedErrorMessage(maxRetries);

        // Assert
        result.Should().Contain("RECOMMENDATION:");
        result.Should().Contain("Consider increasing the timeout");
        result.Should().Contain("reducing the lock contention");
    }

    [Fact]
    public void ToDetailedErrorMessage_WhenRetryCountBelowMaxRetries_ContainsVerificationRecommendation()
    {
        // Arrange
        var lockKey = "retry-lock";
        var timeout = TimeSpan.FromSeconds(10);
        var retryCount = 2;
        var maxRetries = 10;
        var exception = new LockAcquisitionException(lockKey, timeout, retryCount);

        // Act
        var result = exception.ToDetailedErrorMessage(maxRetries);

        // Assert
        result.Should().Contain("RECOMMENDATION:");
        result.Should().Contain("Verify that the lock is being released properly");
        result.Should().Contain("Check for deadlocks");
    }

    [Fact]
    public void ToDetailedErrorMessage_WithNullException_ThrowsArgumentNullException()
    {
        // Arrange
        LockAcquisitionException? exception = null;
        var maxRetries = 5;

        // Act
        Action act = () => exception!.ToDetailedErrorMessage(maxRetries);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("exception");
    }

    [Fact]
    public void ToDetailedErrorMessage_WithNegativeMaxRetries_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var lockKey = "test-lock";
        var timeout = TimeSpan.FromSeconds(5);
        var exception = new LockAcquisitionException(lockKey, timeout);
        var maxRetries = -1;

        // Act
        Action act = () => exception.ToDetailedErrorMessage(maxRetries);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("maxRetries");
    }

    [Fact]
    public void IsTimeoutRelated_WithShortTimeout_ReturnsTrue()
    {
        // Arrange
        var lockKey = "short-timeout-lock";
        var timeout = TimeSpan.FromMilliseconds(100);
        var exception = new LockAcquisitionException(lockKey, timeout);

        // Act
        var result = exception.IsTimeoutRelated();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsTimeoutRelated_WithLongTimeout_ReturnsFalse()
    {
        // Arrange
        var lockKey = "long-timeout-lock";
        var timeout = TimeSpan.FromSeconds(30);
        var exception = new LockAcquisitionException(lockKey, timeout);

        // Act
        var result = exception.IsTimeoutRelated();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsTimeoutRelated_WithBoundaryTimeout_ReturnsFalse()
    {
        // Arrange
        var lockKey = "boundary-lock";
        var timeout = TimeSpan.FromMilliseconds(5000);
        var exception = new LockAcquisitionException(lockKey, timeout);

        // Act
        var result = exception.IsTimeoutRelated();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsTimeoutRelated_WithNullException_ThrowsArgumentNullException()
    {
        // Arrange
        LockAcquisitionException? exception = null;

        // Act
        Action act = () => exception!.IsTimeoutRelated();

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("exception");
    }

    [Fact]
    public void ToLoggableMessage_WithValidException_ReturnsSanitizedMessage()
    {
        // Arrange
        var lockKey = "loggable-lock";
        var timeout = TimeSpan.FromSeconds(7.5);
        var retryCount = 4;
        var exception = new LockAcquisitionException(lockKey, timeout, retryCount);

        // Act
        var result = exception.ToLoggableMessage();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain(lockKey);
        result.Should().Contain($"{retryCount}");
        result.Should().Contain($"{timeout.TotalSeconds}s");
        result.Should().NotContain("Failed to acquire lock");
        result.Should().NotContain("Exception");
    }

    [Fact]
    public void ToLoggableMessage_WithNullException_ThrowsArgumentNullException()
    {
        // Arrange
        LockAcquisitionException? exception = null;

        // Act
        Action act = () => exception!.ToLoggableMessage();

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("exception");
    }

    [Fact]
    public void CalculateSuggestedRetryDelay_WithDefaultBaseDelay_ReturnsExponentialBackoff()
    {
        // Arrange
        var lockKey = "delay-lock";
        var timeout = TimeSpan.FromSeconds(10);
        var retryCount = 3;
        var exception = new LockAcquisitionException(lockKey, timeout, retryCount);
        var baseDelayMs = 100;

        // Act
        var result = exception.CalculateSuggestedRetryDelay(baseDelayMs);

        // Assert
        result.Should().BeGreaterThan(0);
        result.Should().BeInRange(640, 960);
    }

    [Fact]
    public void CalculateSuggestedRetryDelay_WithZeroBaseDelay_ReturnsZero()
    {
        // Arrange
        var lockKey = "zero-delay-lock";
        var timeout = TimeSpan.FromSeconds(5);
        var exception = new LockAcquisitionException(lockKey, timeout);

        // Act
        var result = exception.CalculateSuggestedRetryDelay(0);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void CalculateSuggestedRetryDelay_WithNegativeBaseDelay_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var lockKey = "negative-delay-lock";
        var timeout = TimeSpan.FromSeconds(5);
        var exception = new LockAcquisitionException(lockKey, timeout);
        var baseDelayMs = -10;

        // Act
        Action act = () => exception.CalculateSuggestedRetryDelay(baseDelayMs);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("baseDelayMs");
    }

    [Fact]
    public void CalculateSuggestedRetryDelay_WithHighRetryCount_CapsAt30Seconds()
    {
        // Arrange
        var lockKey = "cap-delay-lock";
        var timeout = TimeSpan.FromSeconds(10);
        var exception = new LockAcquisitionException(lockKey, timeout);

        // Act
        var result = exception.CalculateSuggestedRetryDelay(100);

        // Assert
        result.Should().BeLessThanOrEqualTo(30000);
    }

    [Fact]
    public void CalculateSuggestedRetryDelay_WithBaseDelay1000_ReturnsExpectedRange()
    {
        // Arrange
        var lockKey = "large-delay-lock";
        var timeout = TimeSpan.FromSeconds(15);
        var exception = new LockAcquisitionException(lockKey, timeout);

        // Act
        var result = exception.CalculateSuggestedRetryDelay(1000);

        // Assert - Expected: 1000 * 2^0 = 1000ms for retryCount=0, with randomness (80%-120%)
        result.Should().BeInRange(800, 1200);
    }

    [Fact]
    public void CalculateSuggestedRetryDelay_WithNullException_ThrowsArgumentNullException()
    {
        // Arrange
        LockAcquisitionException? exception = null;
        var baseDelayMs = 100;

        // Act
        Action act = () => exception!.CalculateSuggestedRetryDelay(baseDelayMs);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("exception");
    }

    [Fact]
    public void AllMethods_ProduceConsistentOutput_WithSameException()
    {
        // Arrange
        var lockKey = "consistency-lock";
        var timeout = TimeSpan.FromSeconds(8);
        var retryCount = 2;
        var maxRetries = 10;
        var exception = new LockAcquisitionException(lockKey, timeout, retryCount);

        // Act
        var detailedMessage = exception.ToDetailedErrorMessage(maxRetries);
        var loggableMessage = exception.ToLoggableMessage();
        var isTimeout = exception.IsTimeoutRelated();
        var suggestedDelay = exception.CalculateSuggestedRetryDelay();

        // Assert
        detailedMessage.Should().Contain(lockKey);
        detailedMessage.Should().Contain($"{timeout.TotalSeconds}s");
        detailedMessage.Should().Contain($"{retryCount}");
        detailedMessage.Should().Contain($"{maxRetries}");

        loggableMessage.Should().Contain(lockKey);
        loggableMessage.Should().Contain($"{retryCount}");
        loggableMessage.Should().Contain($"{timeout.TotalSeconds}s");

        suggestedDelay.Should().BeGreaterThan(0);
        suggestedDelay.Should().BeLessThanOrEqualTo(30000);

        isTimeout.Should().BeFalse();
    }
}