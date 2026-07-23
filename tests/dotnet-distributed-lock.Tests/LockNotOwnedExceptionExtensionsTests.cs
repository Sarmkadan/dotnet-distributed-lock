#nullable enable

using System;
using FluentAssertions;
using SarmKadan.DistributedLock.Exceptions;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Unit tests for <see cref="LockNotOwnedExceptionExtensions"/>
/// </summary>
public class LockNotOwnedExceptionExtensionsTests
{
    private const string LockKey = "test-lock-key";
    private const string OwnerId = "service-a";
    private const string ProvidedOwnerId = "service-b";

    [Fact]
    public void ToDetailedErrorMessage_WithValidException_ReturnsFormattedMessage()
    {
        // Arrange
        var exception = new LockNotOwnedException(LockKey, OwnerId, ProvidedOwnerId);

        // Act
        var result = exception.ToDetailedErrorMessage();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Lock ownership conflict detected");
        result.Should().Contain("Lock Key:");
        result.Should().Contain("Correct Owner:");
        result.Should().Contain("Provided Owner:");
        result.Should().Contain("Suggested Actions:");
    }

    [Fact]
    public void IsOwnerMismatch_WithMatchingOwner_ReturnsTrue()
    {
        // Arrange
        var exception = new LockNotOwnedException(LockKey, OwnerId, ProvidedOwnerId);

        // Act
        var result = exception.IsOwnerMismatch(OwnerId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsOwnerMismatch_WithDifferentOwner_ReturnsFalse()
    {
        // Arrange
        var exception = new LockNotOwnedException(LockKey, OwnerId, ProvidedOwnerId);

        // Act
        var result = exception.IsOwnerMismatch("different-owner");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsOwnerMismatch_WithCaseSensitiveComparison_ReturnsFalseForDifferentCase()
    {
        // Arrange
        var exception = new LockNotOwnedException(LockKey, OwnerId, ProvidedOwnerId);

        // Act
        var result = exception.IsOwnerMismatch("SERVICE-A");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetSuggestedOwnerIds_WithValidException_ReturnsMultipleSuggestions()
    {
        // Arrange
        var exception = new LockNotOwnedException(LockKey, OwnerId, ProvidedOwnerId);

        // Act
        var result = exception.GetSuggestedOwnerIds();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(4);
        result.Should().Contain(OwnerId);
        result.Should().Contain(ProvidedOwnerId);
        result.Should().ContainSingle(x => x.Length == 32 && x.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')));
        result.Should().ContainSingle(x => x.StartsWith("lock-"));
    }


    [Fact]
    public void GetSuggestedOwnerIds_WithEmptyLockKey_StillReturnsSuggestions()
    {
        // Arrange
        var exception = new LockNotOwnedException(string.Empty, OwnerId, ProvidedOwnerId);

        // Act
        var result = exception.GetSuggestedOwnerIds();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(4);
        result.Should().Contain(OwnerId);
        result.Should().Contain(ProvidedOwnerId);
    }

    [Fact]
    public void WithCorrectedOwner_WithValidParameters_ReturnsNewExceptionWithUpdatedOwner()
    {
        // Arrange
        var exception = new LockNotOwnedException(LockKey, OwnerId, ProvidedOwnerId);
        var newOwnerId = "service-c";

        // Act
        var result = exception.WithCorrectedOwner(newOwnerId);

        // Assert
        result.Should().NotBeSameAs(exception);
        result.LockKey.Should().Be(LockKey);
        result.OwnerId.Should().Be(OwnerId);
        result.ProvidedOwnerId.Should().Be(newOwnerId);
        result.Should().BeOfType<LockNotOwnedException>();
    }

    [Fact]
    public void WithCorrectedOwner_WithEmptyNewOwnerId_CreatesValidException()
    {
        // Arrange
        var exception = new LockNotOwnedException(LockKey, OwnerId, ProvidedOwnerId);
        var newOwnerId = string.Empty;

        // Act
        var result = exception.WithCorrectedOwner(newOwnerId);

        // Assert
        result.Should().NotBeNull();
        result.ProvidedOwnerId.Should().BeEmpty();
    }

    [Fact]
    public void ExtensionMethods_WorkWithExceptionInheritanceChain()
    {
        // Arrange
        var exception = new LockNotOwnedException(LockKey, OwnerId, ProvidedOwnerId);

        // Act & Assert - All extension methods should work on base DistributedLockException
        exception.Should().BeAssignableTo<DistributedLockException>();

        var action = () => exception.ToDetailedErrorMessage();
        action.Should().NotThrow();

        var isMatch = exception.IsOwnerMismatch(OwnerId);
        isMatch.Should().BeTrue();

        var suggestions = exception.GetSuggestedOwnerIds();
        suggestions.Should().NotBeEmpty();

        var corrected = exception.WithCorrectedOwner("new-owner");
        corrected.Should().NotBeNull();
    }
}