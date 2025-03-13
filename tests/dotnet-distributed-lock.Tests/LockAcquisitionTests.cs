#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using FluentAssertions;
using SarmKadan.DistributedLock.Configuration;
using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Models;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

public class LockAcquisitionTests
{
    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithValidArguments_SetsPropertiesCorrectly()
    {
        // Arrange
        var timeout = TimeSpan.FromSeconds(10);

        // Act
        var acquisition = new LockAcquisition("resource:orders", "worker-1", AcquisitionMode.Blocking, timeout, 5);

        // Assert
        acquisition.LockKey.Should().Be("resource:orders");
        acquisition.RequesterId.Should().Be("worker-1");
        acquisition.Mode.Should().Be(AcquisitionMode.Blocking);
        acquisition.Timeout.Should().Be(timeout);
        acquisition.MaxRetries.Should().Be(5);
        acquisition.AttemptCount.Should().Be(0);
        acquisition.IsSuccessful.Should().BeFalse();
        acquisition.Attempts.Should().BeEmpty();
        acquisition.Id.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithNullOrWhiteSpaceLockKey_ThrowsArgumentException(string key)
    {
        // Act
        var act = () => new LockAcquisition(key, "worker-1", AcquisitionMode.Blocking, TimeSpan.FromSeconds(5));

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("lockKey");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithNullOrWhiteSpaceRequesterId_ThrowsArgumentException(string requesterId)
    {
        // Act
        var act = () => new LockAcquisition("resource:db", requesterId, AcquisitionMode.Blocking, TimeSpan.FromSeconds(5));

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("requesterId");
    }

    // -------------------------------------------------------------------------
    // RecordAttempt
    // -------------------------------------------------------------------------

    [Fact]
    public void RecordAttempt_WhenSucceeded_SetsIsSuccessfulAndRecordsAcquiredAt()
    {
        // Arrange
        var acquisition = new LockAcquisition("resource:db", "worker-1", AcquisitionMode.Blocking, TimeSpan.FromSeconds(5));

        // Act
        acquisition.RecordAttempt(true);

        // Assert
        acquisition.IsSuccessful.Should().BeTrue();
        acquisition.AcquiredAt.Should().NotBeNull();
        acquisition.AcquiredAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        acquisition.AttemptCount.Should().Be(1);
        acquisition.Attempts.Should().HaveCount(1);
        acquisition.Attempts[0].Succeeded.Should().BeTrue();
    }

    [Fact]
    public void RecordAttempt_WhenFailed_IncrementsCountAndStoresErrorMessage()
    {
        // Arrange
        var acquisition = new LockAcquisition("resource:db", "worker-1", AcquisitionMode.Blocking, TimeSpan.FromSeconds(5));

        // Act
        acquisition.RecordAttempt(false, "Lock contention");

        // Assert
        acquisition.IsSuccessful.Should().BeFalse();
        acquisition.AcquiredAt.Should().BeNull();
        acquisition.AttemptCount.Should().Be(1);
        acquisition.Attempts[0].Succeeded.Should().BeFalse();
        acquisition.Attempts[0].ErrorMessage.Should().Be("Lock contention");
    }

    // -------------------------------------------------------------------------
    // CanRetry
    // -------------------------------------------------------------------------

    [Fact]
    public void CanRetry_WhenAttemptsRemaining_ReturnsTrue()
    {
        // Arrange — 1 attempt, 3 max retries
        var acquisition = new LockAcquisition("resource:db", "worker-1", AcquisitionMode.Blocking, TimeSpan.FromSeconds(5), 3);
        acquisition.RecordAttempt(false, "busy");

        // Act & Assert
        acquisition.CanRetry.Should().BeTrue();
    }

    [Fact]
    public void CanRetry_WhenMaxRetriesExhausted_ReturnsFalse()
    {
        // Arrange — 2 attempts equal to max retries
        var acquisition = new LockAcquisition("resource:db", "worker-1", AcquisitionMode.Blocking, TimeSpan.FromSeconds(5), 2);
        acquisition.RecordAttempt(false, "busy");
        acquisition.RecordAttempt(false, "busy");

        // Act & Assert
        acquisition.CanRetry.Should().BeFalse();
    }

    [Fact]
    public void CanRetry_AfterSuccessfulAcquisition_ReturnsFalse()
    {
        // Arrange — succeeded with retries still available
        var acquisition = new LockAcquisition("resource:db", "worker-1", AcquisitionMode.Blocking, TimeSpan.FromSeconds(5), 5);
        acquisition.RecordAttempt(true);

        // Act & Assert — success short-circuits retry eligibility
        acquisition.CanRetry.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // AverageAttemptTime
    // -------------------------------------------------------------------------

    [Fact]
    public void AverageAttemptTime_WithMultipleTimedAttempts_ReturnsCorrectAverage()
    {
        // Arrange
        var acquisition = new LockAcquisition("resource:db", "worker-1", AcquisitionMode.Blocking, TimeSpan.FromSeconds(5));
        acquisition.RecordAttempt(false, null, TimeSpan.FromMilliseconds(100));
        acquisition.RecordAttempt(false, null, TimeSpan.FromMilliseconds(300));

        // Act & Assert — (100 + 300) / 2 = 200 ms
        acquisition.AverageAttemptTime.TotalMilliseconds.Should().BeApproximately(200, 1);
    }

    [Fact]
    public void AverageAttemptTime_WithNoAttempts_ReturnsZero()
    {
        // Arrange
        var acquisition = new LockAcquisition("resource:db", "worker-1", AcquisitionMode.Blocking, TimeSpan.FromSeconds(5));

        // Act & Assert
        acquisition.AverageAttemptTime.Should().Be(TimeSpan.Zero);
    }

    // -------------------------------------------------------------------------
    // ToString
    // -------------------------------------------------------------------------

    [Fact]
    public void ToString_ContainsLockKeyRequesterIdAndMode()
    {
        // Arrange
        var acquisition = new LockAcquisition("resource:payments", "worker-42", AcquisitionMode.NonBlocking, TimeSpan.FromSeconds(5));

        // Act
        var str = acquisition.ToString();

        // Assert
        str.Should().Contain("resource:payments");
        str.Should().Contain("worker-42");
        str.Should().Contain("NonBlocking");
    }
}

public class LockConfigurationTests
{
    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithValidKey_SetsDefaultValues()
    {
        // Act
        var config = new LockConfiguration("resource:orders");

        // Assert
        config.LockKey.Should().Be("resource:orders");
        config.AutoRenewal.Should().BeTrue();
        config.UseFencingToken.Should().BeTrue();
        config.MaxRetries.Should().Be(Constants.LockConstants.DefaultMaxRetries);
        config.LockDuration.Should().Be(Constants.LockConstants.DefaultLockTimeout);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithNullOrWhiteSpaceKey_ThrowsArgumentException(string key)
    {
        // Act
        var act = () => new LockConfiguration(key);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("lockKey");
    }

    // -------------------------------------------------------------------------
    // Validate / IsValid
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_WithDefaultConfig_ReturnsNoErrors()
    {
        // Arrange
        var config = new LockConfiguration("resource:orders");

        // Act
        var errors = config.Validate().ToList();

        // Assert
        errors.Should().BeEmpty();
        config.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithAutoRenewalAndRenewalIntervalEqualToDuration_ReturnsError()
    {
        // Arrange — renewal interval == lock duration violates the auto-renewal invariant
        var config = new LockConfiguration("resource:orders")
        {
            AutoRenewal = true,
            LockDuration = TimeSpan.FromSeconds(30),
            RenewalInterval = TimeSpan.FromSeconds(30)
        };

        // Act
        var errors = config.Validate().ToList();

        // Assert
        errors.Should().NotBeEmpty();
        config.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithNegativeMaxRetries_ReturnsError()
    {
        // Arrange
        var config = new LockConfiguration("resource:orders")
        {
            MaxRetries = -1
        };

        // Act
        var errors = config.Validate().ToList();

        // Assert
        errors.Should().ContainMatch("*negative*");
        config.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithDurationBelowMinimum_ReturnsError()
    {
        // Arrange — minimum is 1s
        var config = new LockConfiguration("resource:orders")
        {
            LockDuration = TimeSpan.FromMilliseconds(500)
        };

        // Act
        var errors = config.Validate().ToList();

        // Assert
        errors.Should().NotBeEmpty();
        config.IsValid.Should().BeFalse();
    }
}

public class DistributedLockOptionsTests
{
    // -------------------------------------------------------------------------
    // Validate / IsValid
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_WithDefaultOptions_ReturnsNoErrors()
    {
        // Arrange — InMemory backend needs no connection string
        var options = new DistributedLockOptions();

        // Act
        var errors = options.Validate().ToList();

        // Assert
        errors.Should().BeEmpty();
        options.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNonInMemoryBackendAndEmptyConnectionString_ReturnsError()
    {
        // Arrange
        var options = new DistributedLockOptions
        {
            BackendType = BackendType.Redis,
            ConnectionString = string.Empty
        };

        // Act
        var errors = options.Validate().ToList();

        // Assert
        errors.Should().ContainMatch("*Connection string*");
        options.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenRenewalIntervalExceedsLockDuration_ReturnsError()
    {
        // Arrange — renewal interval longer than the lock itself is nonsensical
        var options = new DistributedLockOptions
        {
            DefaultLockDuration = TimeSpan.FromSeconds(10),
            DefaultRenewalInterval = TimeSpan.FromSeconds(15)
        };

        // Act
        var errors = options.Validate().ToList();

        // Assert
        errors.Should().ContainMatch("*less than lock duration*");
        options.IsValid.Should().BeFalse();
    }
}

public class LockMetricsTests
{
    // -------------------------------------------------------------------------
    // AcquisitionSuccessRate
    // -------------------------------------------------------------------------

    [Fact]
    public void AcquisitionSuccessRate_WithNoAttempts_ReturnsZero()
    {
        // Arrange
        var metrics = new LockMetrics();

        // Act & Assert
        metrics.AcquisitionSuccessRate.Should().Be(0);
    }

    [Fact]
    public void AcquisitionSuccessRate_WithOnlySuccesses_Returns100Percent()
    {
        // Arrange
        var metrics = new LockMetrics();
        metrics.RecordSuccessfulAcquisition(50);
        metrics.RecordSuccessfulAcquisition(75);

        // Act & Assert
        metrics.AcquisitionSuccessRate.Should().Be(100);
    }

    [Fact]
    public void AcquisitionSuccessRate_WithMixedResults_ReturnsCorrectPercentage()
    {
        // Arrange — 1 success out of 2 attempts = 50 %
        var metrics = new LockMetrics();
        metrics.RecordSuccessfulAcquisition(100);
        metrics.RecordFailedAcquisition();

        // Act & Assert
        metrics.AcquisitionSuccessRate.Should().BeApproximately(50, 0.01);
    }

    // -------------------------------------------------------------------------
    // RecordSuccessfulAcquisition / RecordRelease
    // -------------------------------------------------------------------------

    [Fact]
    public void RecordSuccessfulAcquisition_IncrementsCountersAndActiveLocks()
    {
        // Arrange
        var metrics = new LockMetrics();

        // Act
        metrics.RecordSuccessfulAcquisition(120);

        // Assert
        metrics.SuccessfulAcquisitions.Should().Be(1);
        metrics.TotalAcquisitionAttempts.Should().Be(1);
        metrics.CurrentActiveLocks.Should().Be(1);
        metrics.AverageAcquisitionTimeMs.Should().BeApproximately(120, 0.01);
    }

    [Fact]
    public void RecordRelease_DecrementsCurrentActiveLocksAndRecordsHoldTime()
    {
        // Arrange
        var metrics = new LockMetrics();
        metrics.RecordSuccessfulAcquisition(50);

        // Act
        metrics.RecordRelease(2000);

        // Assert
        metrics.CurrentActiveLocks.Should().Be(0);
        metrics.TotalReleases.Should().Be(1);
        metrics.AverageHoldTimeMs.Should().BeApproximately(2000, 0.01);
    }

    // -------------------------------------------------------------------------
    // RenewalSuccessRate
    // -------------------------------------------------------------------------

    [Fact]
    public void RenewalSuccessRate_WithNoRenewals_ReturnsZero()
    {
        // Arrange
        var metrics = new LockMetrics();

        // Act & Assert
        metrics.RenewalSuccessRate.Should().Be(0);
    }

    [Fact]
    public void RenewalSuccessRate_AfterSuccessfulRenewals_Returns100Percent()
    {
        // Arrange
        var metrics = new LockMetrics();
        metrics.RecordSuccessfulRenewal();
        metrics.RecordSuccessfulRenewal();

        // Act & Assert
        metrics.RenewalSuccessRate.Should().Be(100);
    }
}
