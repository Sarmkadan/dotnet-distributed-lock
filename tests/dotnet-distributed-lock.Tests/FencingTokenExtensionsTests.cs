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
    public void IsNewerThan_HappyPath_ReturnsTrue()
    {
        // Arrange
        var token1 = new FencingToken("token1", 1, DateTime.UtcNow.AddMinutes(-1));
        var token2 = new FencingToken("token2", 2, DateTime.UtcNow);

        // Act
        var result = FencingTokenExtensions.IsNewerThan(token2, token1);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsExpired_HappyPath_ReturnsTrue()
    {
        // Arrange
        var token = new FencingToken("token", 1, DateTime.UtcNow.AddMinutes(-1));
        var now = DateTimeOffset.UtcNow;

        // Act
        var result = FencingTokenExtensions.IsExpired(token, now);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ToDisplayString_HappyPath_ReturnsFormattedString()
    {
        // Arrange
        var issuedAt = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
        var token = new FencingToken("token", 1, issuedAt);

        // Act
        var displayString = FencingTokenExtensions.ToDisplayString(token);

        // Assert
        displayString.Should().Be("Token: token, Sequence: 1, Issued: 2026-07-31T12:00:00.0000000Z");
    }
}
