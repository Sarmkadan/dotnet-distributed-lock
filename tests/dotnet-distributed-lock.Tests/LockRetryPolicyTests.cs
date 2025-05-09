#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using FluentAssertions;
using SarmKadan.DistributedLock.Services;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

public class DefaultLockRetryPolicyTests
{
    [Fact]
    public void Constructor_WithDefaults_SetsReasonableValues()
    {
        // Act
        var policy = new DefaultLockRetryPolicy();

        // Assert
        policy.MaxRetries.Should().Be(Constants.LockConstants.DefaultMaxRetries);
        policy.InitialDelay.TotalMilliseconds.Should().BeGreaterThan(0);
        policy.MaxDelay.TotalMilliseconds.Should().BeGreaterThan(policy.InitialDelay.TotalMilliseconds);
        policy.JitterFactor.Should().BeInRange(0, 1);
    }

    [Fact]
    public void Constructor_WithCustomValues_SetsCustomValues()
    {
        // Arrange
        var initialDelay = TimeSpan.FromMilliseconds(100);
        var maxDelay = TimeSpan.FromSeconds(10);

        // Act
        var policy = new DefaultLockRetryPolicy(
            maxRetries: 5,
            initialDelay: initialDelay,
            maxDelay: maxDelay,
            jitterFactor: 0.2);

        // Assert
        policy.MaxRetries.Should().Be(5);
        policy.InitialDelay.Should().Be(initialDelay);
        policy.MaxDelay.Should().Be(maxDelay);
        policy.JitterFactor.Should().Be(0.2);
    }

    [Fact]
    public void Constructor_WithNegativeMaxRetries_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => new DefaultLockRetryPolicy(maxRetries: -1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("maxRetries");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.5)]
    public void Constructor_WithInvalidJitterFactor_ThrowsArgumentOutOfRangeException(double jitter)
    {
        // Act
        var act = () => new DefaultLockRetryPolicy(jitterFactor: jitter);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("jitterFactor");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Constructor_WithValidJitterFactor_DoesNotThrow(double jitter)
    {
        // Act & Assert
        var policy = new DefaultLockRetryPolicy(jitterFactor: jitter);
        policy.JitterFactor.Should().Be(jitter);
    }

    [Fact]
    public void GetDelay_ForFirstAttempt_ReturnsInitialDelayWithJitter()
    {
        // Arrange
        var policy = new DefaultLockRetryPolicy(
            initialDelay: TimeSpan.FromMilliseconds(100),
            maxDelay: TimeSpan.FromSeconds(10),
            jitterFactor: 0.1);

        // Act
        var delay = policy.GetDelay(0);

        // Assert — should be between initialDelay and initialDelay * (1 + jitterFactor)
        delay.TotalMilliseconds.Should().BeGreaterThanOrEqualTo(100);
        delay.TotalMilliseconds.Should().BeLessThanOrEqualTo(110); // 100 * (1 + 0.1)
    }

    [Fact]
    public void GetDelay_ExponentialBackoff_IncreasesWithEachAttempt()
    {
        // Arrange
        var policy = new DefaultLockRetryPolicy(
            initialDelay: TimeSpan.FromMilliseconds(100),
            maxDelay: TimeSpan.FromSeconds(60),
            jitterFactor: 0);

        // Act
        var delay0 = policy.GetDelay(0);
        var delay1 = policy.GetDelay(1);
        var delay2 = policy.GetDelay(2);

        // Assert — exponential backoff: 100ms, 200ms, 400ms
        delay0.TotalMilliseconds.Should().BeApproximately(100, 1);
        delay1.TotalMilliseconds.Should().BeApproximately(200, 1);
        delay2.TotalMilliseconds.Should().BeApproximately(400, 1);
    }

    [Fact]
    public void GetDelay_CapsByMaxDelay()
    {
        // Arrange
        var policy = new DefaultLockRetryPolicy(
            initialDelay: TimeSpan.FromMilliseconds(100),
            maxDelay: TimeSpan.FromSeconds(5),
            jitterFactor: 0);

        // Act — request delay for attempt 10 (would be 100 * 2^10 = 102,400 ms without cap)
        var delay = policy.GetDelay(10);

        // Assert — should be capped at maxDelay
        delay.TotalMilliseconds.Should().BeLessThanOrEqualTo(5000);
    }

    [Fact]
    public void GetDelay_WithNoJitter_ProducesConsistentValues()
    {
        // Arrange
        var policy = new DefaultLockRetryPolicy(
            initialDelay: TimeSpan.FromMilliseconds(100),
            maxDelay: TimeSpan.FromSeconds(60),
            jitterFactor: 0);

        // Act
        var delay1 = policy.GetDelay(2);
        var delay2 = policy.GetDelay(2);

        // Assert — without jitter, same attempt should return same delay
        delay1.Should().Be(delay2);
    }

    [Fact]
    public void GetDelay_WithJitter_ProducesDifferentValues()
    {
        // Arrange
        var policy = new DefaultLockRetryPolicy(
            initialDelay: TimeSpan.FromMilliseconds(100),
            maxDelay: TimeSpan.FromSeconds(60),
            jitterFactor: 0.5);

        // Act — call multiple times and collect delays
        var delays = Enumerable.Range(0, 10).Select(i => policy.GetDelay(2)).ToList();

        // Assert — with jitter, most values should be different
        var uniqueDelays = delays.Distinct().Count();
        uniqueDelays.Should().BeGreaterThan(1, "jitter should produce some variation");
    }

    [Fact]
    public void GetDelay_RespectsBounds()
    {
        // Arrange
        var policy = new DefaultLockRetryPolicy(
            initialDelay: TimeSpan.FromMilliseconds(50),
            maxDelay: TimeSpan.FromSeconds(10),
            jitterFactor: 0.5);

        // Act — collect delays for several attempts
        var delays = Enumerable.Range(0, 20).Select(i => policy.GetDelay(i)).ToList();

        // Assert — all delays should be >= initialDelay and <= maxDelay * (1 + jitterFactor)
        foreach (var delay in delays)
        {
            delay.TotalMilliseconds.Should().BeGreaterThanOrEqualTo(50);
            delay.TotalMilliseconds.Should().BeLessThanOrEqualTo(10000 * 1.5); // maxDelay * (1 + jitterFactor)
        }
    }
}
