#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using FluentAssertions;
using SarmKadan.DistributedLock.Models;
using Xunit;

// Sub-namespace: the root test namespace already declares a LockMetricsTests class in LockAcquisitionTests.cs.
namespace SarmKadan.DistributedLock.Tests.Metrics;

/// <summary>
/// Unit tests for the <see cref="LockMetrics"/> model: default state, counters, running averages and derived rates.
/// </summary>
public class LockMetricsTests
{
    /// <summary>
    /// Tests that a new instance starts with all counters at zero, rates at zero, and timestamps set to the creation time.
    /// </summary>
    [Fact]
    public void Constructor_InitializesZeroCountersAndUtcTimestamps()
    {
        var before = DateTime.UtcNow;

        var metrics = new LockMetrics();

        var after = DateTime.UtcNow;
        metrics.TotalAcquisitionAttempts.Should().Be(0);
        metrics.CurrentActiveLocks.Should().Be(0);
        metrics.AverageAcquisitionTimeMs.Should().Be(0);
        metrics.AcquisitionSuccessRate.Should().Be(0);
        metrics.RenewalSuccessRate.Should().Be(0);
        metrics.OverallSuccessRate.Should().Be(0);
        metrics.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        metrics.LastUpdatedAt.Should().BeOnOrAfter(metrics.CreatedAt);
    }

    /// <summary>
    /// Tests that successful acquisitions increment counters and maintain a running average of acquisition time.
    /// </summary>
    [Fact]
    public void RecordSuccessfulAcquisition_UpdatesCountersAndRunningAverage()
    {
        var metrics = new LockMetrics();

        metrics.RecordSuccessfulAcquisition(100);
        metrics.RecordSuccessfulAcquisition(300);

        metrics.TotalAcquisitionAttempts.Should().Be(2);
        metrics.SuccessfulAcquisitions.Should().Be(2);
        metrics.FailedAcquisitions.Should().Be(0);
        metrics.CurrentActiveLocks.Should().Be(2);
        metrics.AverageAcquisitionTimeMs.Should().BeApproximately(200, 0.0001);
        metrics.AcquisitionSuccessRate.Should().Be(100);
    }

    /// <summary>
    /// Tests that failed acquisitions count as attempts but do not affect active locks or the average acquisition time.
    /// </summary>
    [Fact]
    public void RecordFailedAcquisition_CountsAttemptWithoutChangingAverageOrActiveLocks()
    {
        var metrics = new LockMetrics();
        metrics.RecordSuccessfulAcquisition(150);

        metrics.RecordFailedAcquisition();

        metrics.TotalAcquisitionAttempts.Should().Be(2);
        metrics.FailedAcquisitions.Should().Be(1);
        metrics.CurrentActiveLocks.Should().Be(1);
        metrics.AverageAcquisitionTimeMs.Should().BeApproximately(150, 0.0001);
        metrics.AcquisitionSuccessRate.Should().BeApproximately(50, 0.0001);
    }

    /// <summary>
    /// Tests that releases decrement active locks and maintain a running average of hold time, including a zero-duration hold.
    /// </summary>
    [Fact]
    public void RecordRelease_DecrementsActiveLocksAndTracksAverageHoldTime()
    {
        var metrics = new LockMetrics();
        metrics.RecordSuccessfulAcquisition(10);
        metrics.RecordSuccessfulAcquisition(10);

        metrics.RecordRelease(40);
        metrics.RecordRelease(0);

        metrics.TotalReleases.Should().Be(2);
        metrics.CurrentActiveLocks.Should().Be(0);
        metrics.AverageHoldTimeMs.Should().BeApproximately(20, 0.0001);
    }

    /// <summary>
    /// Tests that expired locks are counted separately from releases and also decrement active locks.
    /// </summary>
    [Fact]
    public void RecordExpiredLock_IncrementsExpiredCountAndDecrementsActiveLocks()
    {
        var metrics = new LockMetrics();
        metrics.RecordSuccessfulAcquisition(5);

        metrics.RecordExpiredLock();

        metrics.ExpiredLocks.Should().Be(1);
        metrics.TotalReleases.Should().Be(0);
        metrics.CurrentActiveLocks.Should().Be(0);
    }

    /// <summary>
    /// Tests that renewal success rate is computed from successful and failed renewals only, and is zero when none exist.
    /// </summary>
    [Fact]
    public void RenewalSuccessRate_ReflectsSuccessfulAndFailedRenewals()
    {
        var metrics = new LockMetrics();
        metrics.RenewalSuccessRate.Should().Be(0);

        metrics.RecordSuccessfulRenewal();
        metrics.RecordSuccessfulRenewal();
        metrics.RecordSuccessfulRenewal();
        metrics.RecordFailedRenewal();

        metrics.TotalRenewals.Should().Be(4);
        metrics.SuccessfulRenewals.Should().Be(3);
        metrics.FailedRenewals.Should().Be(1);
        metrics.RenewalSuccessRate.Should().BeApproximately(75, 0.0001);
        metrics.AcquisitionSuccessRate.Should().Be(0);
    }

    /// <summary>
    /// Tests that overall success rate combines acquisitions, renewals and releases, and that expired locks are excluded.
    /// </summary>
    [Fact]
    public void OverallSuccessRate_CombinesAcquisitionsRenewalsAndReleases()
    {
        var metrics = new LockMetrics();

        metrics.RecordSuccessfulAcquisition(10);
        metrics.RecordFailedAcquisition();
        metrics.RecordSuccessfulRenewal();
        metrics.RecordFailedRenewal();
        metrics.RecordRelease(5);
        metrics.RecordExpiredLock();

        metrics.OverallSuccessRate.Should().BeApproximately(60, 0.0001);
    }

    /// <summary>
    /// Tests that <see cref="LockMetrics.ToString"/> reports the active count and formatted rates and timings.
    /// </summary>
    [Fact]
    public void ToString_ReportsActiveLocksRatesAndTimings()
    {
        var metrics = new LockMetrics();
        metrics.RecordSuccessfulAcquisition(100);
        metrics.RecordFailedAcquisition();

        var text = metrics.ToString();

        text.Should().StartWith("LockMetrics(Active=1, AcqSuccess=");
        text.Should().Contain("RenewSuccess=0");
        text.Should().Contain("AvgAcqTime=100");
        text.Should().Contain("AvgHoldTime=0");
    }
}
