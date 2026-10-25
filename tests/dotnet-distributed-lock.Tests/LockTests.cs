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

/// <summary>
/// Unit tests for the <see cref="Lock"/> model: construction, renewal, release, ownership checks and expiry flags.
/// </summary>
public class LockTests
{
    /// <summary>
    /// The parameterized constructor sets key, owner, duration and an Acquiring status, and leaves optional fields unset.
    /// </summary>
    [Fact]
    public void Constructor_WithValidArguments_InitializesAcquiringLock()
    {
        var duration = TimeSpan.FromSeconds(10);

        var @lock = new Lock("orders", "worker-1", duration);

        @lock.Key.Should().Be("orders");
        @lock.OwnerId.Should().Be("worker-1");
        @lock.Duration.Should().Be(duration);
        @lock.Status.Should().Be(LockStatus.Acquiring);
        @lock.RenewalCount.Should().Be(0);
        @lock.RenewedAt.Should().BeNull();
        @lock.FencingToken.Should().BeNull();
        @lock.Metadata.Should().BeNull();
        @lock.AcquiredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        @lock.ExpiresAt.Should().Be(@lock.AcquiredAt.Add(duration));
    }

    /// <summary>
    /// Null, blank and too-short arguments are rejected; a duration of exactly the minimum is accepted.
    /// </summary>
    [Fact]
    public void Constructor_WithInvalidArguments_ThrowsAndMinimumDurationIsAccepted()
    {
        Action nullKey = () => _ = new Lock(null!, "owner", TimeSpan.FromSeconds(5));
        Action blankOwner = () => _ = new Lock("key", "   ", TimeSpan.FromSeconds(5));
        Action tooShort = () => _ = new Lock("key", "owner", TimeSpan.FromMilliseconds(999));

        nullKey.Should().Throw<ArgumentNullException>().WithParameterName("key");
        blankOwner.Should().Throw<ArgumentException>().WithParameterName("ownerId");
        tooShort.Should().Throw<ArgumentException>().WithParameterName("duration");

        var atMinimum = new Lock("key", "owner", TimeSpan.FromSeconds(1));
        atMinimum.Duration.Should().Be(TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Setting Key or OwnerId to null through the property setters throws, and the previous value is kept.
    /// </summary>
    [Fact]
    public void KeyAndOwnerId_SetToNull_ThrowAndKeepPreviousValue()
    {
        var @lock = new Lock("key", "owner", TimeSpan.FromSeconds(5));

        Action setKey = () => @lock.Key = null!;
        Action setOwner = () => @lock.OwnerId = null!;

        setKey.Should().Throw<ArgumentNullException>();
        setOwner.Should().Throw<ArgumentNullException>();
        @lock.Key.Should().Be("key");
        @lock.OwnerId.Should().Be("owner");
    }

    /// <summary>
    /// Renew without an argument extends expiry by the original duration, sets RenewedAt, increments the count and marks the lock Held.
    /// </summary>
    [Fact]
    public void Renew_WithoutNewDuration_ExtendsExpiryAndMarksHeld()
    {
        var duration = TimeSpan.FromSeconds(30);
        var @lock = new Lock("key", "owner", duration);

        @lock.Renew();

        @lock.Status.Should().Be(LockStatus.Held);
        @lock.RenewalCount.Should().Be(1);
        @lock.RenewedAt.Should().NotBeNull();
        @lock.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.Add(duration), TimeSpan.FromSeconds(5));
        @lock.Duration.Should().Be(duration);
    }

    /// <summary>
    /// Renewing a lock whose expiry is in the past throws <see cref="LockExpiredException"/> and does not change state.
    /// </summary>
    [Fact]
    public void Renew_AfterExpiry_ThrowsLockExpiredExceptionWithoutChangingState()
    {
        var @lock = new Lock("key", "owner", TimeSpan.FromSeconds(5));
        @lock.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);

        Action act = () => @lock.Renew(TimeSpan.FromMinutes(1));

        act.Should().Throw<LockExpiredException>();
        @lock.RenewalCount.Should().Be(0);
        @lock.RenewedAt.Should().BeNull();
        @lock.Status.Should().Be(LockStatus.Acquiring);
    }

    /// <summary>
    /// Release marks the lock Released and moves expiry to now, so the lock is no longer valid.
    /// </summary>
    [Fact]
    public void Release_MarksReleasedExpiresNowAndInvalidatesLock()
    {
        var @lock = new Lock("key", "owner", TimeSpan.FromMinutes(1));
        @lock.Renew();

        @lock.Release();

        @lock.Status.Should().Be(LockStatus.Released);
        @lock.ExpiresAt.Should().BeOnOrBefore(DateTime.UtcNow);
        @lock.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// ValidateOwnership passes for the matching owner and throws <see cref="LockNotOwnedException"/> for any other value, including null.
    /// </summary>
    [Fact]
    public void ValidateOwnership_MatchingOwnerPassesOtherOwnersAndNullThrow()
    {
        var @lock = new Lock("key", "owner", TimeSpan.FromSeconds(5));

        Action matching = () => @lock.ValidateOwnership("owner");
        Action other = () => @lock.ValidateOwnership("intruder");
        Action nullOwner = () => @lock.ValidateOwnership(null!);

        matching.Should().NotThrow();
        other.Should().Throw<LockNotOwnedException>()
            .Which.ProvidedOwnerId.Should().Be("intruder");
        nullOwner.Should().Throw<LockNotOwnedException>();
    }

    /// <summary>
    /// IsValid requires Held status and a future expiry; IsCloseToExpiration flips at the 25% remaining threshold.
    /// </summary>
    [Fact]
    public void IsValidAndIsCloseToExpiration_RespectStatusAndQuarterLifetimeThreshold()
    {
        var @lock = new Lock("key", "owner", TimeSpan.FromSeconds(100));

        @lock.IsValid.Should().BeFalse("status is still Acquiring");

        @lock.Renew();
        @lock.IsValid.Should().BeTrue();
        @lock.IsCloseToExpiration.Should().BeFalse();

        @lock.ExpiresAt = DateTime.UtcNow.AddSeconds(20);
        @lock.IsCloseToExpiration.Should().BeTrue("20s remaining is below 25s (25% of 100s)");

        @lock.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        @lock.IsValid.Should().BeFalse();
        @lock.IsExpired.Should().BeTrue();
    }
}
