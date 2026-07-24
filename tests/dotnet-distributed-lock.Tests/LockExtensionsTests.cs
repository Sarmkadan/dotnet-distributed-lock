#nullable enable

using System;
using FluentAssertions;
using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Tests;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

public class LockExtensionsTests
{
    // Helper to create a Lock with desired status and expiration.
    private static Lock CreateLock(LockStatus status, DateTime expiresAt)
    {
        // The Lock class in this repository has a parameterless constructor and public setters
        // for the properties we need in the tests. If the actual implementation differs,
        // adjust the construction accordingly.
        var @lock = new Lock
        {
            Status = status,
            ExpiresAt = expiresAt
        };
        return @lock;
    }

    #region IsActive

    [Fact]
    public void IsActive_NullLock_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => LockExtensions.IsActive(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(LockStatus.Acquiring)]
    [InlineData(LockStatus.Renewing)]
    public void IsActive_WhenStatusIsAcquiringOrRenewing_ReturnsTrue(LockStatus status)
    {
        // Arrange
        var @lock = CreateLock(status, DateTime.UtcNow.AddMinutes(1));

        // Act
        var result = @lock.IsActive();

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(LockStatus.Held)]
    [InlineData(LockStatus.Released)]
    [InlineData(LockStatus.Unknown)]
    public void IsActive_WhenStatusIsNotActive_ReturnsFalse(LockStatus status)
    {
        // Arrange
        var @lock = CreateLock(status, DateTime.UtcNow.AddMinutes(1));

        // Act
        var result = @lock.IsActive();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsAvailable

    [Fact]
    public void IsAvailable_NullLock_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => LockExtensions.IsAvailable(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsAvailable_WhenNotHeldAndNotExpired_ReturnsTrue()
    {
        // Arrange
        var @lock = CreateLock(LockStatus.Acquiring, DateTime.UtcNow.AddMinutes(5));

        // Act
        var result = @lock.IsAvailable();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAvailable_WhenHeld_ReturnsFalse()
    {
        // Arrange
        var @lock = CreateLock(LockStatus.Held, DateTime.UtcNow.AddMinutes(5));

        // Act
        var result = @lock.IsAvailable();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsAvailable_WhenExpired_ReturnsFalse()
    {
        // Arrange
        var @lock = CreateLock(LockStatus.Acquiring, DateTime.UtcNow.AddSeconds(-10));

        // Act
        var result = @lock.IsAvailable();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetRemainingTime

    [Fact]
    public void GetRemainingTime_NullLock_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => LockExtensions.GetRemainingTime(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetRemainingTime_WhenFutureExpiration_ReturnsPositiveTimeSpan()
    {
        // Arrange
        var future = DateTime.UtcNow.AddSeconds(30);
        var @lock = CreateLock(LockStatus.Acquiring, future);

        // Act
        var remaining = @lock.GetRemainingTime();

        // Assert
        remaining.Should().BeGreaterThan(TimeSpan.Zero);
        remaining.TotalSeconds.Should().BeApproximately(30, 1);
    }

    [Fact]
    public void GetRemainingTime_WhenPastExpiration_ReturnsZero()
    {
        // Arrange
        var past = DateTime.UtcNow.AddSeconds(-5);
        var @lock = CreateLock(LockStatus.Acquiring, past);

        // Act
        var remaining = @lock.GetRemainingTime();

        // Assert
        remaining.Should().Be(TimeSpan.Zero);
    }

    #endregion

    #region SafeRenew

    [Fact]
    public void SafeRenew_NullLock_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => LockExtensions.SafeRenew(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SafeRenew_WhenExpired_ReleasesAndReturnsFalse()
    {
        // Arrange
        var past = DateTime.UtcNow.AddSeconds(-1);
        var @lock = CreateLock(LockStatus.Acquiring, past);

        // Act
        var result = @lock.SafeRenew();

        // Assert
        result.Should().BeFalse();
        // After expiration the lock should be released; the exact state depends on implementation.
        // We verify that the status is no longer Acquiring/Renewing.
        @lock.Status.Should().NotBe(LockStatus.Acquiring);
        @lock.Status.Should().NotBe(LockStatus.Renewing);
    }

    [Fact]
    public void SafeRenew_WhenNotExpired_RenewsAndReturnsTrue()
    {
        // Arrange
        var future = DateTime.UtcNow.AddMinutes(1);
        var @lock = CreateLock(LockStatus.Acquiring, future);
        var originalExpires = @lock.ExpiresAt;

        // Act
        var result = @lock.SafeRenew(TimeSpan.FromSeconds(30));

        // Assert
        result.Should().BeTrue();
        @lock.ExpiresAt.Should().BeAfter(originalExpires);
    }

    #endregion
}
