#nullable enable
using System;
using FluentAssertions;
using SarmKadan.DistributedLock.Models;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

public class FencingTokenTests
{
    [Fact]
    public void Constructor_SetsPropertiesCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        const string tokenStr = "test-token";
        const long seqNum = 10;

        // Act
        var token = new FencingToken(tokenStr, seqNum, now);

        // Assert
        token.Token.Should().Be(tokenStr);
        token.SequenceNumber.Should().Be(seqNum);
        token.IssuedAt.Should().Be(now);
    }

    [Fact]
    public void Constructor_ThrowsOnInvalidInput()
    {
        // Act & Assert
        Action actNullToken = () => new FencingToken(null!, 1);
        actNullToken.Should().Throw<ArgumentException>().WithParameterName("token");

        Action actEmptyToken = () => new FencingToken("", 1);
        actEmptyToken.Should().Throw<ArgumentException>().WithParameterName("token");

        Action actNegativeSeq = () => new FencingToken("t", -1);
        actNegativeSeq.Should().Throw<ArgumentException>().WithParameterName("sequenceNumber");
    }

    [Fact]
    public void IncrementSequence_ReturnsNewTokenWithIncrementedSequence()
    {
        // Arrange
        var original = new FencingToken("original", 5);

        // Act
        var incremented = original.IncrementSequence();

        // Assert
        incremented.SequenceNumber.Should().Be(6);
        incremented.Token.Should().NotBe(original.Token);
        incremented.IssuedAt.Should().BeAfter(original.IssuedAt);
    }

    [Fact]
    public void IsGreaterThan_CompareSequenceNumbers()
    {
        // Arrange
        var low = new FencingToken("low", 1);
        var high = new FencingToken("high", 2);

        // Assert
        high.IsGreaterThan(low).Should().BeTrue();
        low.IsGreaterThan(high).Should().BeFalse();
    }

    [Fact]
    public void IsGreaterThan_ThrowsOnNull()
    {
        // Arrange
        var token = new FencingToken("t", 1);

        // Act
        Action act = () => token.IsGreaterThan(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsValid_ChecksExpiration()
    {
        // Arrange
        var validToken = new FencingToken("t", 1, DateTime.UtcNow);
        var expiredToken = new FencingToken("t", 1, DateTime.UtcNow.AddMinutes(-10));

        // Act & Assert
        validToken.IsValid(TimeSpan.FromMinutes(1)).Should().BeTrue();
        expiredToken.IsValid(TimeSpan.FromMinutes(1)).Should().BeFalse();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var token1 = new FencingToken("t", 1, now);
        var token2 = new FencingToken("t", 1, now);

        // Assert
        token1.Should().Be(token2);
        token1.GetHashCode().Should().Be(token2.GetHashCode());
    }
}
