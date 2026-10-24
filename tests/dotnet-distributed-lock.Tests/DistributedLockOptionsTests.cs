using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using SarmKadan.DistributedLock.Configuration;
using SarmKadan.DistributedLock.Enums;
using Xunit;

namespace SarmKadan.DistributedLock.Tests.ConfigurationOptions;

public class DistributedLockOptionsTests
{
    [Fact]
    public void Defaults_MatchDocumentedValues()
    {
        var options = new DistributedLockOptions();

        options.BackendType.Should().Be(BackendType.InMemory);
        options.ConnectionString.Should().BeEmpty();
        options.DefaultLockDuration.Should().Be(TimeSpan.FromSeconds(30));
        options.DefaultAcquisitionTimeout.Should().Be(TimeSpan.FromSeconds(5));
        options.DefaultRenewalInterval.Should().Be(TimeSpan.FromSeconds(10));
        options.DefaultMaxRetries.Should().Be(3);
        options.DefaultRetryDelayMs.Should().Be(100);
        options.DefaultAcquisitionMode.Should().Be(AcquisitionMode.Blocking);
        options.EnableAutoRenewal.Should().BeTrue();
        options.UseFencingTokens.Should().BeTrue();
    }

    [Fact]
    public void Validate_DefaultOptions_ReturnsNoErrorsAndIsValid()
    {
        var options = new DistributedLockOptions();

        options.Validate().Should().BeEmpty();
        options.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(BackendType.Redis, "")]
    [InlineData(BackendType.PostgreSQL, "   ")]
    [InlineData(BackendType.SQLite, "")]
    public void Validate_NonInMemoryBackendWithoutConnectionString_ReportsError(BackendType backend, string connectionString)
    {
        var options = new DistributedLockOptions { BackendType = backend, ConnectionString = connectionString };

        options.Validate().Should().ContainSingle()
            .Which.Should().Be("Connection string is required for non-InMemory backends.");
        options.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0.5, true)]
    [InlineData(1, false)]
    [InlineData(3600, false)]
    [InlineData(3601, true)]
    public void Validate_LockDurationBoundaries_AreInclusive(double seconds, bool expectError)
    {
        // Renewal interval is kept well below every duration so only the duration rule is exercised.
        var options = new DistributedLockOptions
        {
            DefaultLockDuration = TimeSpan.FromSeconds(seconds),
            DefaultRenewalInterval = TimeSpan.FromMilliseconds(100)
        };

        var errors = options.Validate().ToList();

        errors.Any(e => e.StartsWith("Default lock duration", StringComparison.Ordinal)).Should().Be(expectError);
    }

    [Fact]
    public void Validate_RenewalIntervalNonPositiveOrNotShorterThanDuration_ReportsSingleError()
    {
        var zeroInterval = new DistributedLockOptions { DefaultRenewalInterval = TimeSpan.Zero };
        var intervalEqualsDuration = new DistributedLockOptions
        {
            DefaultRenewalInterval = TimeSpan.FromSeconds(30),
            DefaultLockDuration = TimeSpan.FromSeconds(30)
        };

        zeroInterval.Validate().Should().ContainSingle()
            .Which.Should().Be("Default renewal interval must be greater than zero.");
        intervalEqualsDuration.Validate().Should().ContainSingle()
            .Which.Should().Be("Renewal interval must be less than lock duration.");
    }

    [Fact]
    public void Validate_CountersAtZeroAreAllowed_NegativeOrNonPositiveValuesAreReportedInOrder()
    {
        var atZero = new DistributedLockOptions
        {
            DefaultMaxRetries = 0,
            DefaultRetryDelayMs = 0,
            DefaultMaxRenewals = 0
        };
        var invalid = new DistributedLockOptions
        {
            DefaultAcquisitionTimeout = TimeSpan.Zero,
            DefaultMaxRetries = -1,
            DefaultRetryDelayMs = -1,
            MaxConcurrentLocks = 0,
            DefaultMaxRenewals = -1
        };

        atZero.IsValid.Should().BeTrue();
        invalid.Validate().Should().Equal(
            "Default acquisition timeout must be greater than zero.",
            "Max retries cannot be negative.",
            "Retry delay cannot be negative.",
            "Max concurrent locks must be greater than zero.",
            "Default max renewals must be non-negative.");
    }

    [Theory]
    [InlineData(0.0, true)]
    [InlineData(0.01, true)]
    [InlineData(0.02, false)]
    [InlineData(0.98, false)]
    [InlineData(0.99, true)]
    public void Validate_RenewalFraction_BoundariesAreExclusive(double fraction, bool expectError)
    {
        var options = new DistributedLockOptions { DefaultRenewalFraction = fraction };

        var errors = options.Validate().ToList();

        errors.Any(e => e.StartsWith("Default renewal fraction", StringComparison.Ordinal)).Should().Be(expectError);
    }
}
