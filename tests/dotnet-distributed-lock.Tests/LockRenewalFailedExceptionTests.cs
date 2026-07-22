using FluentAssertions;
using SarmKadan.DistributedLock.Core.Exceptions;
using SarmKadan.DistributedLock.Exceptions;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

public class LockRenewalFailedExceptionTests
{
    [Fact]
    public void Constructor_WithLockIdOnly_SetsLockIdAndDefaultMessage()
    {
        // Arrange
        var lockId = "test-lock-123";

        // Act
        var exception = new LockRenewalFailedException(lockId);

        // Assert
        exception.LockId.Should().Be(lockId);
        exception.Message.Should().Be($"Failed to renew lock with ID '{lockId}'.");
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithLockIdAndMessage_SetsLockIdMessageAndNoInnerException()
    {
        // Arrange
        var lockId = "test-lock-456";
        var message = "Custom error message for lock renewal failure";

        // Act
        var exception = new LockRenewalFailedException(lockId, message);

        // Assert
        exception.LockId.Should().Be(lockId);
        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithAllParameters_SetsAllPropertiesCorrectly()
    {
        // Arrange
        var lockId = "test-lock-789";
        var message = "Custom error with inner exception";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new LockRenewalFailedException(lockId, message, innerException);

        // Assert
        exception.LockId.Should().Be(lockId);
        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeSameAs(innerException);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Constructor_WithVariousLockIds_HandlesAllValidStrings(string? lockId)
    {
        // Arrange
        var actualLockId = lockId ?? string.Empty;

        // Act
        var exception = new LockRenewalFailedException(actualLockId);

        // Assert
        exception.LockId.Should().Be(actualLockId);
        exception.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void LockIdProperty_IsReadOnlyAndSetCorrectly()
    {
        // Arrange
        var lockId = "immutable-lock-id";
        var exception = new LockRenewalFailedException(lockId);

        // Act & Assert - LockId should be immutable
        exception.LockId.Should().Be(lockId);

        // Verify it's read-only by attempting to modify (won't compile if not read-only)
        // This is a compile-time check - the property should have no setter
        var propertyInfo = typeof(LockRenewalFailedException).GetProperty("LockId");
        propertyInfo?.GetSetMethod(true).Should().BeNull("LockId property should be read-only");
    }

    [Fact]
    public void Constructor_WithEmptyLockId_CreatesValidException()
    {
        // Arrange
        var emptyLockId = string.Empty;

        // Act
        var exception = new LockRenewalFailedException(emptyLockId);

        // Assert
        exception.LockId.Should().BeEmpty();
        exception.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_WithLongLockId_HandlesLongStrings()
    {
        // Arrange
        var longLockId = new string('x', 1000);

        // Act
        var exception = new LockRenewalFailedException(longLockId);

        // Assert
        exception.LockId.Should().Be(longLockId);
        exception.Message.Should().Contain(longLockId);
    }

    [Fact]
    public void Inheritance_HasCorrectBaseType()
    {
        // Arrange & Act
        var exception = new LockRenewalFailedException("test-id");

        // Assert
        exception.Should().BeAssignableTo<DistributedLockException>();
        exception.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void ToString_IncludesExceptionTypeAndMessage()
    {
        // Arrange
        var lockId = "to-string-test-lock";
        var message = "Test ToString method";
        var exception = new LockRenewalFailedException(lockId, message);

        // Act
        var toStringResult = exception.ToString();

        // Assert
        toStringResult.Should().Contain("LockRenewalFailedException");
        toStringResult.Should().Contain(message);
    }
}
