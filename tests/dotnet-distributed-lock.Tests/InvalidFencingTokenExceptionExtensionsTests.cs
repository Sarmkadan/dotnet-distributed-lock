#nullable enable

using FluentAssertions;
using SarmKadan.DistributedLock.Exceptions;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Unit tests for <see cref="InvalidFencingTokenExceptionExtensions"/>
/// </summary>
public class InvalidFencingTokenExceptionExtensionsTests
{
    [Fact]
    public void IsTokenMismatch_WithMatchingTokens_ReturnsFalse()
    {
        // Arrange
        var exception = new InvalidFencingTokenException("token123", "token123");

        // Act
        var result = exception.IsTokenMismatch();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsTokenMismatch_WithDifferentTokens_ReturnsTrue()
    {
        // Arrange
        var exception = new InvalidFencingTokenException("token123", "token456");

        // Act
        var result = exception.IsTokenMismatch();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsTokenMismatch_WithNullException_ThrowsArgumentNullException()
    {
        // Arrange
        InvalidFencingTokenException? exception = null;

        // Act
        var act = () => exception!.IsTokenMismatch();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsTokenSuperseded_WithOlderToken_ReturnsTrue()
    {
        // Arrange
        var exception = new InvalidFencingTokenException("00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000002");

        // Act
        var result = exception.IsTokenSuperseded();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsTokenSuperseded_WithNewerToken_ReturnsFalse()
    {
        // Arrange
        var exception = new InvalidFencingTokenException("00000000-0000-0000-0000-000000000002", "00000000-0000-0000-0000-000000000001");

        // Act
        var result = exception.IsTokenSuperseded();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsTokenFromFuture_WithNewerToken_ReturnsTrue()
    {
        // Arrange
        var exception = new InvalidFencingTokenException("00000000-0000-0000-0000-000000000002", "00000000-0000-0000-0000-000000000001");

        // Act
        var result = exception.IsTokenFromFuture();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsTokenFromFuture_WithOlderToken_ReturnsFalse()
    {
        // Arrange
        var exception = new InvalidFencingTokenException("00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000002");

        // Act
        var result = exception.IsTokenFromFuture();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void WithTokens_WithValidTokens_ReturnsNewException()
    {
        // Arrange
        var originalException = new InvalidFencingTokenException("old-provided", "old-current");
        var newProvidedToken = "new-provided";
        var newCurrentToken = "new-current";

        // Act
        var result = originalException.WithTokens(newProvidedToken, newCurrentToken);

        // Assert
        result.Should().NotBeSameAs(originalException);
        result.ProvidedToken.Should().Be(newProvidedToken);
        result.CurrentToken.Should().Be(newCurrentToken);
        result.Message.Should().Contain(newProvidedToken);
        result.Message.Should().Contain(newCurrentToken);
    }

    [Fact]
    public void WithTokens_WithNullOriginalException_ThrowsArgumentNullException()
    {
        // Arrange
        InvalidFencingTokenException? originalException = null;
        var newProvidedToken = "new-provided";
        var newCurrentToken = "new-current";

        // Act
        var act = () => originalException!.WithTokens(newProvidedToken, newCurrentToken);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null, "current")]
    [InlineData("", "current")]
    public void WithTokens_WithNullOrEmptyNewProvidedToken_ThrowsArgumentException(string? newProvidedToken, string currentToken)
    {
        // Arrange
        var originalException = new InvalidFencingTokenException("old-provided", "old-current");

        // Act
        var act = () => originalException.WithTokens(newProvidedToken!, currentToken);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("newProvidedToken");
    }

    [Theory]
    [InlineData("provided", null)]
    [InlineData("provided", "")]
    public void WithTokens_WithNullOrEmptyNewCurrentToken_ThrowsArgumentException(string providedToken, string? newCurrentToken)
    {
        // Arrange
        var originalException = new InvalidFencingTokenException("old-provided", "old-current");

        // Act
        var act = () => originalException.WithTokens(providedToken, newCurrentToken!);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("newCurrentToken");
    }

    [Fact]
    public void GetTokenDetails_ReturnsFormattedString()
    {
        // Arrange
        var providedToken = "provided-token-123";
        var currentToken = "current-token-456";
        var exception = new InvalidFencingTokenException(providedToken, currentToken);

        // Act
        var result = exception.GetTokenDetails();

        // Assert
        result.Should().Be($"Provided: '{providedToken}', Current: '{currentToken}'");
    }
}