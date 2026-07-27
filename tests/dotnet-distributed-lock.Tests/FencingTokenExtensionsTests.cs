#nullable enable
using System;
using FluentAssertions;
using SarmKadan.DistributedLock.Models;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

public class FencingTokenExtensionsTests
{
    [Fact]
    public void Parse_HappyPath_ReturnsToken()
    {
        // Arrange
        var tokenString = "token:1";

        // Act
        var token = FencingTokenExtensions.Parse(tokenString);

        // Assert
        token.Token.Should().Be("token");
        token.SequenceNumber.Should().Be(1);
    }

    [Fact]
    public void Parse_NullInput_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => FencingTokenExtensions.Parse(null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TryParse_HappyPath_ReturnsTrue()
    {
        // Arrange
        var tokenString = "token:1";

        // Act
        var result = FencingTokenExtensions.TryParse(tokenString, out var token);

        // Assert
        result.Should().BeTrue();
        token.Token.Should().Be("token");
        token.SequenceNumber.Should().Be(1);
    }

    [Fact]
    public void TryParse_NullInput_ReturnsFalse()
    {
        // Act
        var result = FencingTokenExtensions.TryParse(null, out _);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TryParse_EmptyString_ReturnsFalse()
    {
        // Act
        var result = FencingTokenExtensions.TryParse("", out _);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ToTokenString_HappyPath_ReturnsTokenString()
    {
        // Arrange
        var token = new FencingToken("token", 1);

        // Act
        var tokenString = FencingTokenExtensions.ToTokenString(token);

        // Assert
        tokenString.Should().Be("token:1");
    }

    [Fact]
    public void GetAge_HappyPath_ReturnsTimeSpan()
    {
        // Arrange
        var token = new FencingToken("token", 1);
        var now = DateTime.UtcNow;

        // Act
        var age = FencingTokenExtensions.GetAge(token);

        // Assert
        age.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void IsLessThan_HappyPath_ReturnsTrue()
    {
        // Arrange
        var token1 = new FencingToken("token1", 1);
        var token2 = new FencingToken("token2", 2);

        // Act
        var result = FencingTokenExtensions.IsLessThan(token1, token2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsGreaterThanOrEqual_HappyPath_ReturnsTrue()
    {
        // Arrange
        var token1 = new FencingToken("token1", 1);
        var token2 = new FencingToken("token2", 2);

        // Act
        var result = FencingTokenExtensions.IsGreaterThanOrEqual(token1, token2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsLessThanOrEqual_HappyPath_ReturnsTrue()
    {
        // Arrange
        var token1 = new FencingToken("token1", 1);
        var token2 = new FencingToken("token2", 2);

        // Act
        var result = FencingTokenExtensions.IsLessThanOrEqual(token1, token2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void WithSequenceNumber_HappyPath_ReturnsToken()
    {
        // Arrange
        var token = new FencingToken("token", 1);

        // Act
        var newToken = FencingTokenExtensions.WithSequenceNumber(token, 2);

        // Assert
        newToken.Token.Should().Be("token");
        newToken.SequenceNumber.Should().Be(2);
    }

    [Fact]
    public void WithNewToken_HappyPath_ReturnsToken()
    {
        // Act
        var newToken = FencingTokenExtensions.WithNewToken(new FencingToken("token", 1));

        // Assert
        newToken.Token.Should().NotBe("token");
        newToken.SequenceNumber.Should().Be(1);
    }

    [Fact]
    public void IsAdjacentTo_HappyPath_ReturnsTrue()
    {
        // Arrange
        var token1 = new FencingToken("token1", 1);
        var token2 = new FencingToken("token2", 2);

        // Act
        var result = FencingTokenExtensions.IsAdjacentTo(token1, token2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAdjacentTo_HappyPath_ReturnsFalse()
    {
        // Arrange
        var token1 = new FencingToken("token1", 1);
        var token2 = new FencingToken("token2", 3);

        // Act
        var result = FencingTokenExtensions.IsAdjacentTo(token1, token2);

        // Assert
        result.Should().BeFalse();
    }
}
