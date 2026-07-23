#nullable enable

using System;
using FluentAssertions;
using SarmKadan.DistributedLock.Exceptions;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

public class LockExpiredExceptionTests
{
    [Fact]
    public void Constructor_WithLockKeyAndExpirationTime_SetsPropertiesCorrectly()
    {
        var lockKey = "test-distributed-lock";
        var expirationTime = DateTime.UtcNow.AddMinutes(-5);
        var ex = new LockExpiredException(lockKey, expirationTime);

        ex.LockKey.Should().Be(lockKey);
        ex.ExpirationTime.Should().Be(expirationTime);
        ex.Message.Should().Be($"Lock '{lockKey}' has expired at {expirationTime:O}.");
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithAllParameters_IncludesInnerExceptionAndSetsAllProperties()
    {
        var lockKey = "another-lock-key";
        var expirationTime = DateTime.UtcNow.AddHours(-1);
        var innerException = new InvalidOperationException("Inner error occurred");
        var ex = new LockExpiredException(lockKey, expirationTime, innerException);

        ex.LockKey.Should().Be(lockKey);
        ex.ExpirationTime.Should().Be(expirationTime);
        ex.Message.Should().Be($"Lock '{lockKey}' has expired at {expirationTime:O}.");
        ex.InnerException.Should().BeSameAs(innerException);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithNullOrEmptyLockKey_HandlesAllValidInputs(string? lockKey)
    {
        var expirationTime = DateTime.UtcNow.AddMinutes(-5);
        var ex = new LockExpiredException(lockKey!, expirationTime);

        ex.LockKey.Should().Be(lockKey);
        ex.ExpirationTime.Should().Be(expirationTime);
        ex.Message.Should().NotBeNullOrEmpty();
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithFutureExpirationTime_CreatesValidException()
    {
        var lockKey = "future-expiration-lock";
        var expirationTime = DateTime.UtcNow.AddMinutes(5);
        var ex = new LockExpiredException(lockKey, expirationTime);

        ex.LockKey.Should().Be(lockKey);
        ex.ExpirationTime.Should().Be(expirationTime);
        ex.Message.Should().Contain(lockKey);
        ex.Message.Should().Contain(expirationTime.ToString("O"));
    }

    [Fact]
    public void Constructor_WithMinValueExpirationTime_HandlesBoundaryValue()
    {
        var lockKey = "min-expiration-lock";
        var expirationTime = DateTime.MinValue;
        var ex = new LockExpiredException(lockKey, expirationTime);

        ex.LockKey.Should().Be(lockKey);
        ex.ExpirationTime.Should().Be(DateTime.MinValue);
        ex.Message.Should().Contain(lockKey);
        ex.Message.Should().Contain(DateTime.MinValue.ToString("O"));
    }

    [Fact]
    public void Constructor_WithMaxValueExpirationTime_HandlesBoundaryValue()
    {
        var lockKey = "max-expiration-lock";
        var expirationTime = DateTime.MaxValue;
        var ex = new LockExpiredException(lockKey, expirationTime);

        ex.LockKey.Should().Be(lockKey);
        ex.ExpirationTime.Should().Be(DateTime.MaxValue);
        ex.Message.Should().Contain(lockKey);
        ex.Message.Should().Contain(DateTime.MaxValue.ToString("O"));
    }

    [Fact]
    public void Properties_AreReadOnlyAndSetCorrectly()
    {
        var lockKey = "immutable-lock-key";
        var expirationTime = DateTime.UtcNow.AddMinutes(-10);
        var ex = new LockExpiredException(lockKey, expirationTime);

        ex.LockKey.Should().Be(lockKey);
        ex.ExpirationTime.Should().Be(expirationTime);

        var lockKeyProperty = typeof(LockExpiredException).GetProperty("LockKey");
        var expirationTimeProperty = typeof(LockExpiredException).GetProperty("ExpirationTime");

        lockKeyProperty?.GetSetMethod(true).Should().BeNull("LockKey property should be read-only");
        expirationTimeProperty?.GetSetMethod(true).Should().BeNull("ExpirationTime property should be read-only");
    }

    [Fact]
    public void Constructor_WithLongStrings_HandlesLongInput()
    {
        var longLockKey = new string('x', 1000);
        var longExpirationTime = DateTime.UtcNow.AddMinutes(-100);
        var ex = new LockExpiredException(longLockKey, longExpirationTime);

        ex.LockKey.Should().Be(longLockKey);
        ex.ExpirationTime.Should().Be(longExpirationTime);
        ex.Message.Should().Contain(longLockKey);
        ex.Message.Should().Contain(longExpirationTime.ToString("O"));
    }

    [Fact]
    public void Inheritance_HasCorrectBaseType()
    {
        var ex = new LockExpiredException("test-lock", DateTime.UtcNow.AddMinutes(-5));

        ex.Should().BeAssignableTo<DistributedLockException>();
        ex.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void ToString_IncludesExceptionTypeAndMessage()
    {
        var lockKey = "to-string-test-lock";
        var expirationTime = DateTime.UtcNow.AddMinutes(-3);
        var ex = new LockExpiredException(lockKey, expirationTime);
        var toStringResult = ex.ToString();

        toStringResult.Should().Contain(nameof(LockExpiredException));
        toStringResult.Should().Contain(ex.Message);
    }

    [Fact]
    public void Message_FormatMatchesExpectedPattern()
    {
        var lockKey = "my-distributed-lock";
        var expirationTime = new DateTime(2024, 12, 25, 14, 30, 0, DateTimeKind.Utc);
        var ex = new LockExpiredException(lockKey, expirationTime);

        ex.Message.Should().Be($"Lock '{lockKey}' has expired at {expirationTime:O}.");
    }

    [Fact]
    public void Constructor_WithRecentPastExpirationTime_CreatesValidException()
    {
        var lockKey = "recent-past-lock";
        var expirationTime = DateTime.UtcNow.AddSeconds(-30);
        var ex = new LockExpiredException(lockKey, expirationTime);

        ex.LockKey.Should().Be(lockKey);
        ex.ExpirationTime.Should().Be(expirationTime);
        ex.Message.Should().Contain(lockKey);
        ex.Message.Should().Contain("has expired");
    }
}