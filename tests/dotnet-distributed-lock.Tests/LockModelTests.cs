#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using FluentAssertions;
using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Exceptions;
using SarmKadan.DistributedLock.Models;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

public class LockModelTests
{
    // -------------------------------------------------------------------------
    // Lock constructor
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithValidArguments_SetsPropertiesCorrectly()
    {
        // Arrange
        var key = "resource:orders";
        var ownerId = "worker-1";
        var duration = TimeSpan.FromSeconds(10);

        // Act
        var @lock = new Lock(key, ownerId, duration);

        // Assert
        @lock.Key.Should().Be(key);
        @lock.OwnerId.Should().Be(ownerId);
        @lock.Duration.Should().Be(duration);
        @lock.Status.Should().Be(LockStatus.Acquiring);
        @lock.RenewalCount.Should().Be(0);
        @lock.AcquiredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        @lock.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.Add(duration), TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithNullOrWhiteSpaceKey_ThrowsArgumentException(string key)
    {
        // Act
        var act = () => new Lock(key, "owner-1", TimeSpan.FromSeconds(5));

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("key");
    }

    [Fact]
    public void Constructor_WithEmptyOwnerId_ThrowsArgumentException()
    {
        // Act
        var act = () => new Lock("resource:db", "", TimeSpan.FromSeconds(5));

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("ownerId");
    }

    [Fact]
    public void Constructor_WithDurationBelowMinimum_ThrowsArgumentException()
    {
        // Arrange — minimum is 1 second per LockConstants
        var tooShort = TimeSpan.FromMilliseconds(500);

        // Act
        var act = () => new Lock("key", "owner", tooShort);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("duration");
    }

    // -------------------------------------------------------------------------
    // Lock.IsExpired / IsValid
    // -------------------------------------------------------------------------

    [Fact]
    public void IsExpired_WhenExpiresAtIsInThePast_ReturnsTrue()
    {
        // Arrange
        var @lock = new Lock("key", "owner", TimeSpan.FromSeconds(5))
        {
            ExpiresAt = DateTime.UtcNow.AddSeconds(-1)
        };

        // Act & Assert
        @lock.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void IsExpired_WhenExpiresAtIsInTheFuture_ReturnsFalse()
    {
        // Arrange
        var @lock = new Lock("key", "owner", TimeSpan.FromSeconds(30));

        // Act & Assert
        @lock.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenStatusIsHeldAndNotExpired_ReturnsTrue()
    {
        // Arrange
        var @lock = new Lock("key", "owner", TimeSpan.FromSeconds(30))
        {
            Status = LockStatus.Held
        };

        // Act & Assert
        @lock.IsValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenStatusIsAcquiredNotHeld_ReturnsFalse()
    {
        // Arrange — Status stays Acquiring after construction
        var @lock = new Lock("key", "owner", TimeSpan.FromSeconds(30));

        // Act & Assert
        @lock.IsValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenExpired_ReturnsFalse()
    {
        // Arrange
        var @lock = new Lock("key", "owner", TimeSpan.FromSeconds(30))
        {
            Status = LockStatus.Held,
            ExpiresAt = DateTime.UtcNow.AddSeconds(-1)
        };

        // Act & Assert
        @lock.IsValid.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // Lock.IsCloseToExpiration
    // -------------------------------------------------------------------------

    [Fact]
    public void IsCloseToExpiration_WhenRemainingTimeLessThan25Percent_ReturnsTrue()
    {
        // Arrange — 10 s duration; expire in 2 s (20 % < 25 %)
        var @lock = new Lock("key", "owner", TimeSpan.FromSeconds(10))
        {
            ExpiresAt = DateTime.UtcNow.AddSeconds(2)
        };

        // Act & Assert
        @lock.IsCloseToExpiration.Should().BeTrue();
    }

    [Fact]
    public void IsCloseToExpiration_WhenRemainingTimeMoreThan25Percent_ReturnsFalse()
    {
        // Arrange — 10 s duration; expire in 8 s (80 % > 25 %)
        var @lock = new Lock("key", "owner", TimeSpan.FromSeconds(10))
        {
            ExpiresAt = DateTime.UtcNow.AddSeconds(8)
        };

        // Act & Assert
        @lock.IsCloseToExpiration.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // Lock.Renew
    // -------------------------------------------------------------------------

    [Fact]
    public void Renew_WhenNotExpired_IncrementsRenewalCountAndSetsRenewedAt()
    {
        // Arrange
        var @lock = new Lock("key", "owner", TimeSpan.FromSeconds(30))
        {
            Status = LockStatus.Held
        };

        // Act
        @lock.Renew();

        // Assert
        @lock.RenewalCount.Should().Be(1);
        @lock.RenewedAt.Should().NotBeNull();
        @lock.RenewedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        @lock.Status.Should().Be(LockStatus.Held);
    }

    [Fact]
    public void Renew_WithNewDuration_ExtendsByNewDuration()
    {
        // Arrange
        var @lock = new Lock("key", "owner", TimeSpan.FromSeconds(30))
        {
            Status = LockStatus.Held
        };
        var extendBy = TimeSpan.FromMinutes(1);

        // Act
        @lock.Renew(extendBy);

        // Assert
        @lock.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.Add(extendBy), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Renew_WhenExpired_ThrowsLockExpiredException()
    {
        // Arrange
        var @lock = new Lock("key", "owner", TimeSpan.FromSeconds(5))
        {
            ExpiresAt = DateTime.UtcNow.AddSeconds(-1)
        };

        // Act
        var act = () => @lock.Renew();

        // Assert
        act.Should().Throw<LockExpiredException>()
            .Which.LockKey.Should().Be("key");
    }

    // -------------------------------------------------------------------------
    // Lock.Release
    // -------------------------------------------------------------------------

    [Fact]
    public void Release_SetsStatusToReleasedAndExpiresImmediately()
    {
        // Arrange
        var @lock = new Lock("key", "owner", TimeSpan.FromSeconds(30))
        {
            Status = LockStatus.Held
        };

        // Act
        @lock.Release();

        // Assert
        @lock.Status.Should().Be(LockStatus.Released);
        @lock.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    // -------------------------------------------------------------------------
    // Lock.ValidateOwnership
    // -------------------------------------------------------------------------

    [Fact]
    public void ValidateOwnership_WithCorrectOwner_DoesNotThrow()
    {
        // Arrange
        var @lock = new Lock("key", "owner-A", TimeSpan.FromSeconds(10));

        // Act & Assert
        @lock.Invoking(l => l.ValidateOwnership("owner-A")).Should().NotThrow();
    }

    [Fact]
    public void ValidateOwnership_WithWrongOwner_ThrowsLockNotOwnedException()
    {
        // Arrange
        var @lock = new Lock("key", "owner-A", TimeSpan.FromSeconds(10));

        // Act
        var act = () => @lock.ValidateOwnership("impostor");

        // Assert
        act.Should().Throw<LockNotOwnedException>()
            .Which.ProvidedOwnerId.Should().Be("impostor");
    }

    // -------------------------------------------------------------------------
    // FencingToken
    // -------------------------------------------------------------------------

    [Fact]
    public void FencingToken_Constructor_WithNegativeSequenceNumber_ThrowsArgumentException()
    {
        // Act
        var act = () => new FencingToken("abc123", -1);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("sequenceNumber");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FencingToken_Constructor_WithEmptyToken_ThrowsArgumentException(string token)
    {
        // Act
        var act = () => new FencingToken(token, 1);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("token");
    }

    [Fact]
    public void FencingToken_IncrementSequence_CreatesTokenWithSequencePlusOne()
    {
        // Arrange
        var original = new FencingToken("abc123token456", 5);

        // Act
        var incremented = original.IncrementSequence();

        // Assert
        incremented.SequenceNumber.Should().Be(6);
        incremented.Token.Should().NotBe(original.Token);
    }

    [Fact]
    public void FencingToken_IsGreaterThan_WhenSequenceIsHigher_ReturnsTrue()
    {
        // Arrange
        var older = new FencingToken("token-old", 3);
        var newer = new FencingToken("token-new", 5);

        // Act & Assert
        newer.IsGreaterThan(older).Should().BeTrue();
    }

    [Fact]
    public void FencingToken_IsGreaterThan_WhenSequenceIsLower_ReturnsFalse()
    {
        // Arrange
        var newer = new FencingToken("token-new", 5);
        var older = new FencingToken("token-old", 3);

        // Act & Assert
        older.IsGreaterThan(newer).Should().BeFalse();
    }

    [Fact]
    public void FencingToken_IsGreaterThan_WhenComparedToNull_ReturnsTrue()
    {
        // Arrange
        var token = new FencingToken("sometoken123456", 1);

        // Act & Assert
        token.IsGreaterThan(null!).Should().BeTrue();
    }

    [Fact]
    public void FencingToken_IsValid_WhenWithinLifetime_ReturnsTrue()
    {
        // Arrange — issued just now, lifetime of 1 hour
        var token = new FencingToken("token1234567890", 1, DateTime.UtcNow);

        // Act & Assert
        token.IsValid(TimeSpan.FromHours(1)).Should().BeTrue();
    }

    [Fact]
    public void FencingToken_IsValid_WhenLifetimeExceeded_ReturnsFalse()
    {
        // Arrange — issued 2 seconds ago, lifetime of 1 second
        var token = new FencingToken("token1234567890", 1, DateTime.UtcNow.AddSeconds(-2));

        // Act & Assert
        token.IsValid(TimeSpan.FromSeconds(1)).Should().BeFalse();
    }

    [Fact]
    public void FencingToken_Equals_WithSameTokenAndSequence_ReturnsTrue()
    {
        // Arrange
        var issuedAt = DateTime.UtcNow;
        var t1 = new FencingToken("abc123456789012", 7, issuedAt);
        var t2 = new FencingToken("abc123456789012", 7, issuedAt.AddSeconds(1));

        // Act & Assert — equality is based on Token string + SequenceNumber only
        t1.Should().Be(t2);
    }

    [Fact]
    public void FencingToken_CompareTo_WithHigherSequenceOther_ReturnsNegative()
    {
        // Arrange
        var lower = new FencingToken("token1234567890", 2);
        var higher = new FencingToken("token0987654321", 9);

        // Act & Assert
        lower.CompareTo(higher).Should().BeNegative();
    }

    [Fact]
    public void FencingToken_ToString_ContainsTokenAndSequence()
    {
        // Arrange
        var token = new FencingToken("abc123456789012", 42);

        // Act
        var str = token.ToString();

        // Assert
        str.Should().Be("abc123456789012:42");
    }
}
