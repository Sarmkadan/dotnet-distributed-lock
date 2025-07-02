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

/// <summary>
/// Contains unit tests for the <see cref="LockAcquisition"/> class, which represents the process of attempting to acquire a distributed lock.
/// Tests cover constructor validation, attempt recording, retry logic, timing metrics, and string representation.
/// </summary>
public class LockAcquisitionTests
{
    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests that the constructor correctly initializes all properties when valid arguments are provided.
    /// Verifies that <c>LockKey</c>, <c>RequesterId</c>, <c>Mode</c>, <c>Timeout</c>, <c>MaxRetries</c>, <c>AttemptCount</c>, <c>IsSuccessful</c>, <c>Attempts</c>, and <c>Id</c> are set to their expected values.
    /// </summary>
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

    /// <summary>
    /// Tests that the constructor throws an <see cref="ArgumentException"/> when a null or whitespace lock key is provided.
    /// Verifies that the exception has the correct parameter name.
    /// </summary>
    /// <param name="key">The null or whitespace lock key to test with.</param>
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

    /// <summary>
    /// Tests that the constructor throws an <see cref="ArgumentException"/> when a null or whitespace requester ID is provided.
    /// Verifies that the exception has the correct parameter name.
    /// </summary>
    /// <param name="requesterId">The null or whitespace requester ID to test with.</param>
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

    /// <summary>
    /// Tests that <see cref="LockAcquisition.RecordAttempt(bool)"/> sets <c>IsSuccessful</c> to true and records the acquisition timestamp when the attempt succeeds.
    /// Verifies that <c>AcquiredAt</c> is set to a recent timestamp and attempt tracking is updated correctly.
    /// </summary>
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

    /// <summary>
    /// Tests that <see cref="LockAcquisition.RecordAttempt(bool, string)"/> increments attempt count and stores error message when the attempt fails.
    /// Verifies that <c>IsSuccessful</c> remains false, <c>AcquiredAt</c> remains null, and attempt tracking is updated correctly.
    /// </summary>
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

    /// <summary>
    /// Tests that <see cref="LockAcquisition.CanRetry"/> returns true when attempts remain within the max retries limit.
    /// Verifies that <c>CanRetry</c> property returns true when <c>AttemptCount</c> is less than <c>MaxRetries</c>.
    /// </summary>
    [Fact]
    public void CanRetry_WhenAttemptsRemaining_ReturnsTrue()
    {
        // Arrange — 1 attempt, 3 max retries
        var acquisition = new LockAcquisition("resource:db", "worker-1", AcquisitionMode.Blocking, TimeSpan.FromSeconds(5), 3);
        acquisition.RecordAttempt(false, "busy");

        // Act & Assert
        acquisition.CanRetry.Should().BeTrue();
    }

    /// <summary>
    /// Tests that <see cref="LockAcquisition.CanRetry"/> returns false when max retries have been exhausted.
    /// Verifies that <c>CanRetry</c> property returns false when <c>AttemptCount</c> equals <c>MaxRetries</c>.
    /// </summary>
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

    /// <summary>
    /// Tests that <see cref="LockAcquisition.CanRetry"/> returns false after a successful acquisition, regardless of remaining retries.
    /// Verifies that <c>CanRetry</c> property returns false when <c>IsSuccessful</c> is true, short‑circuiting retry eligibility.
    /// </summary>
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

    /// <summary>
    /// Tests that <see cref="LockAcquisition.AverageAttemptTime"/> returns the correct average duration when multiple attempts have been recorded with timing information.
    /// Verifies that the average is calculated correctly as (100 ms + 300 ms) / 2 = 200 ms.
    /// </summary>
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

    /// <summary>
    /// Tests that <see cref="LockAcquisition.AverageAttemptTime"/> returns <see cref="TimeSpan.Zero"/> when no attempts have been recorded.
    /// Verifies that the <c>AverageAttemptTime</c> property returns zero when the <c>Attempts</c> collection is empty.
    /// </summary>
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

    /// <summary>
    /// Tests that <see cref="LockAcquisition.ToString"/> includes the lock key, requester ID, and acquisition mode in its string representation.
    /// Verifies that the string output contains the expected components for debugging and logging purposes.
    /// </summary>
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

/// <summary>
/// Contains unit tests for the <see cref="LockConfiguration"/> class, which represents configuration settings for acquiring distributed locks.
/// Tests cover constructor validation and configuration validation rules.
/// </summary>
public class LockConfigurationTests
{
    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests that the constructor correctly initializes default values when a valid lock key is provided.
    /// Verifies that <c>AutoRenewal</c>, <c>UseFencingToken</c>, <c>MaxRetries</c>, and <c>LockDuration</c> are set to their expected default values.
    /// </summary>
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

    /// <summary>
    /// Tests that the constructor throws an <see cref="ArgumentException"/> when a null or whitespace lock key is provided.
    /// Verifies that the exception has the correct parameter name.
    /// </summary>
    /// <param name="key">The null or whitespace lock key to test with.</param>
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

    /// <summary>
    /// Tests that <see cref="LockConfiguration.Validate"/> returns no validation errors when using the default configuration.
    /// Verifies that <c>IsValid</c> property returns true when all configuration values are within acceptable ranges.
    /// </summary>
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

    /// <summary>
    /// Tests that <see cref="LockConfiguration.Validate"/> returns a validation error when <c>AutoRenewal</c> is enabled with <c>RenewalInterval</c> equal to <c>LockDuration</c>.
    /// Verifies that <c>IsValid</c> property returns false when the auto‑renewal invariant is violated.
    /// </summary>
    [Fact]
    public void Validate_WithAutoRenewalAndRenewalIntervalEqualToDuration_ReturnsError()
    {
        // Arrange — renewal interval == lock duration violates the auto‑renewal invariant
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

    /// <summary>
    /// Tests that <see cref="LockConfiguration.Validate"/> returns a validation error when <c>MaxRetries</c> is set to a negative value.
    /// Verifies that <c>IsValid</c> property returns false and error messages contain the word “negative”.
    /// </summary>
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

    /// <summary>
    /// Tests that <see cref="LockConfiguration.Validate"/> returns a validation error when <c>LockDuration</c> is set below the minimum allowed duration.
    /// Verifies that <c>IsValid</c> property returns false when the lock duration is too short (less than 1 second).
    /// </summary>
    [Fact]
    public void Validate_WithDurationBelowMinimum_ReturnsError()
    {
        // Arrange — minimum is 1 s
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

/// <summary>
/// Contains unit tests for the <see cref="DistributedLockOptions"/> class, which represents global configuration options for the distributed lock system.
/// Tests cover validation of backend type, connection strings, and timing configurations.
/// </summary>
public class DistributedLockOptionsTests
{
    // -------------------------------------------------------------------------
    // Validate / IsValid
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests that <see cref="DistributedLockOptions.Validate"/> returns no validation errors when using the default options.
    /// Verifies that <c>IsValid</c> property returns true when all options are within acceptable ranges for the <c>InMemory</c> backend.
    /// </summary>
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

    /// <summary>
    /// Tests that <see cref="DistributedLockOptions.Validate"/> returns a validation error when using a non‑<c>InMemory</c> backend with an empty connection string.
    /// Verifies that <c>IsValid</c> property returns false and error messages contain “Connection string”.
    /// </summary>
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

    /// <summary>
    /// Tests that <see cref="DistributedLockOptions.Validate"/> returns a validation error when <c>DefaultRenewalInterval</c> exceeds <c>DefaultLockDuration</c>.
    /// Verifies that <c>IsValid</c> property returns false and error messages contain “less than lock duration”.
    /// </summary>
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

/// <summary>
/// Contains unit tests for the <see cref="LockMetrics"/> class, which tracks acquisition and renewal metrics for distributed locks.
/// Tests cover success rates, counter increments, active lock tracking, and timing measurements.
/// </summary>
public class LockMetricsTests
{
    // -------------------------------------------------------------------------
    // AcquisitionSuccessRate
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests that <see cref="LockMetrics.AcquisitionSuccessRate"/> returns 0 when no acquisition attempts have been recorded.
    /// Verifies that the success rate calculation returns 0 when <c>TotalAcquisitionAttempts</c> is 0.
    /// </summary>
    [Fact]
    public void AcquisitionSuccessRate_WithNoAttempts_ReturnsZero()
    {
        // Arrange
        var metrics = new LockMetrics();

        // Act & Assert
        metrics.AcquisitionSuccessRate.Should().Be(0);
    }

    /// <summary>
    /// Tests that <see cref="LockMetrics.AcquisitionSuccessRate"/> returns 100 % when only successful acquisitions have been recorded.
    /// Verifies that the success rate calculation returns 100 when all attempts are successful.
    /// </summary>
    [Fact]
    public void AcquisitionSuccessRate_WithOnlySuccesses_Returns100Percent()
    {
        // Arrange
        var metrics = new LockMetrics();

        // Act
        metrics.RecordSuccessfulAcquisition(50);
        metrics.RecordSuccessfulAcquisition(75);

        // Assert
        metrics.AcquisitionSuccessRate.Should().Be(100);
    }

    /// <summary>
    /// Tests that <see cref="LockMetrics.AcquisitionSuccessRate"/> returns the correct percentage when both successful and failed acquisitions have been recorded.
    /// Verifies that the success rate calculation returns 50 % when 1 out of 2 attempts succeeded.
    /// </summary>
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

    /// <summary>
    /// Tests that <see cref="LockMetrics.RecordSuccessfulAcquisition(double)"/> increments counters and active lock count, and updates the average acquisition time.
    /// </summary>
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

    /// <summary>
    /// Tests that <see cref="LockMetrics.RecordRelease(double)"/> decrements the current active lock count and records the hold time.
    /// </summary>
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

    /// <summary>
    /// Tests that <see cref="LockMetrics.RenewalSuccessRate"/> returns 0 when no renewals have been recorded.
    /// </summary>
    [Fact]
    public void RenewalSuccessRate_WithNoRenewals_ReturnsZero()
    {
        // Arrange
        var metrics = new LockMetrics();

        // Act & Assert
        metrics.RenewalSuccessRate.Should().Be(0);
    }

    /// <summary>
    /// Tests that <see cref="LockMetrics.RenewalSuccessRate"/> returns 100 % after successful renewals have been recorded.
    /// </summary>
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
