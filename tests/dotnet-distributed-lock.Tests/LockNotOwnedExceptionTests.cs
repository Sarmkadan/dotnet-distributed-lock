#nullable enable

using FluentAssertions;
using SarmKadan.DistributedLock.Exceptions;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Unit tests for <see cref="LockNotOwnedException"/>
/// </summary>
public class LockNotOwnedExceptionTests
{
    [Fact]
    public void Constructor_WithAllParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var lockKey = "test-lock-key";
        var ownerId = "owner-123";
        var providedOwnerId = "attempted-owner-456";

        // Act
        var ex = new LockNotOwnedException(lockKey, ownerId, providedOwnerId);

        // Assert
        ex.LockKey.Should().Be(lockKey);
        ex.OwnerId.Should().Be(ownerId);
        ex.ProvidedOwnerId.Should().Be(providedOwnerId);
        ex.Message.Should().Be($"Lock '{lockKey}' is owned by '{ownerId}', not '{providedOwnerId}'.");
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithAllParametersAndInnerException_SetsAllProperties()
    {
        // Arrange
        var lockKey = "another-lock";
        var ownerId = "real-owner";
        var providedOwnerId = "fake-owner";
        var innerException = new InvalidOperationException("Inner error occurred");

        // Act
        var ex = new LockNotOwnedException(lockKey, ownerId, providedOwnerId, innerException);

        // Assert
        ex.LockKey.Should().Be(lockKey);
        ex.OwnerId.Should().Be(ownerId);
        ex.ProvidedOwnerId.Should().Be(providedOwnerId);
        ex.Message.Should().Be($"Lock '{lockKey}' is owned by '{ownerId}', not '{providedOwnerId}'.");
        ex.InnerException.Should().BeSameAs(innerException);
    }

    [Theory]
    [InlineData(null, "owner", "attempted")]
    [InlineData("", "owner", "attempted")]
    [InlineData(" ", "owner", "attempted")]
    [InlineData("lock-key", null, "attempted")]
    [InlineData("lock-key", "", "attempted")]
    [InlineData("lock-key", "owner", null)]
    [InlineData("lock-key", "owner", "")]
    public void Constructor_WithNullOrEmptyParameters_HandlesAllValidInputs(string? lockKey, string? ownerId, string? providedOwnerId)
    {
        // Arrange
        var actualLockKey = lockKey ?? string.Empty;
        var actualOwnerId = ownerId ?? string.Empty;
        var actualProvidedOwnerId = providedOwnerId ?? string.Empty;

        // Act
        var ex = new LockNotOwnedException(actualLockKey, actualOwnerId, actualProvidedOwnerId);

        // Assert
        ex.LockKey.Should().Be(actualLockKey);
        ex.OwnerId.Should().Be(actualOwnerId);
        ex.ProvidedOwnerId.Should().Be(actualProvidedOwnerId);
        ex.Message.Should().NotBeNullOrEmpty();
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void Properties_AreReadOnlyAndSetCorrectly()
    {
        // Arrange
        var lockKey = "immutable-lock";
        var ownerId = "real-owner";
        var providedOwnerId = "fake-owner";
        var ex = new LockNotOwnedException(lockKey, ownerId, providedOwnerId);

        // Act & Assert - Properties should be immutable
        ex.LockKey.Should().Be(lockKey);
        ex.OwnerId.Should().Be(ownerId);
        ex.ProvidedOwnerId.Should().Be(providedOwnerId);

        // Verify properties are read-only by checking for setters
        var lockKeyProperty = typeof(LockNotOwnedException).GetProperty("LockKey");
        var ownerIdProperty = typeof(LockNotOwnedException).GetProperty("OwnerId");
        var providedOwnerIdProperty = typeof(LockNotOwnedException).GetProperty("ProvidedOwnerId");

        lockKeyProperty?.GetSetMethod(true).Should().BeNull("LockKey property should be read-only");
        ownerIdProperty?.GetSetMethod(true).Should().BeNull("OwnerId property should be read-only");
        providedOwnerIdProperty?.GetSetMethod(true).Should().BeNull("ProvidedOwnerId property should be read-only");
    }

    [Fact]
    public void Constructor_WithEmptyStrings_CreatesValidException()
    {
        // Arrange
        var emptyLockKey = string.Empty;
        var emptyOwnerId = string.Empty;
        var emptyProvidedOwnerId = string.Empty;

        // Act
        var ex = new LockNotOwnedException(emptyLockKey, emptyOwnerId, emptyProvidedOwnerId);

        // Assert
        ex.LockKey.Should().BeEmpty();
        ex.OwnerId.Should().BeEmpty();
        ex.ProvidedOwnerId.Should().BeEmpty();
        ex.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_WithLongStrings_HandlesLongInput()
    {
        // Arrange
        var longLockKey = new string('x', 1000);
        var longOwnerId = new string('y', 1000);
        var longProvidedOwnerId = new string('z', 1000);

        // Act
        var ex = new LockNotOwnedException(longLockKey, longOwnerId, longProvidedOwnerId);

        // Assert
        ex.LockKey.Should().Be(longLockKey);
        ex.OwnerId.Should().Be(longOwnerId);
        ex.ProvidedOwnerId.Should().Be(longProvidedOwnerId);
        ex.Message.Should().Contain(longLockKey);
        ex.Message.Should().Contain(longOwnerId);
        ex.Message.Should().Contain(longProvidedOwnerId);
    }

    [Fact]
    public void Inheritance_HasCorrectBaseType()
    {
        // Arrange & Act
        var ex = new LockNotOwnedException("lock", "owner", "attempted");

        // Assert
        ex.Should().BeAssignableTo<DistributedLockException>();
        ex.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void ToString_IncludesExceptionTypeAndMessage()
    {
        // Arrange
        var lockKey = "to-string-lock";
        var ownerId = "real-owner";
        var providedOwnerId = "fake-owner";
        var ex = new LockNotOwnedException(lockKey, ownerId, providedOwnerId);

        // Act
        var toStringResult = ex.ToString();

        // Assert
        toStringResult.Should().Contain(nameof(LockNotOwnedException));
        toStringResult.Should().Contain(ex.Message);
    }

    [Fact]
    public void Message_FormatMatchesExpectedPattern()
    {
        // Arrange
        var lockKey = "my-distributed-lock";
        var ownerId = "service-a";
        var providedOwnerId = "service-b";

        // Act
        var ex = new LockNotOwnedException(lockKey, ownerId, providedOwnerId);

        // Assert
        ex.Message.Should().Be($"Lock '{lockKey}' is owned by '{ownerId}', not '{providedOwnerId}'.");
    }
}