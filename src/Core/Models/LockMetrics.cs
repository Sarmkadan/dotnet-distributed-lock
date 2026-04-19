#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Models;

/// <summary>
/// Tracks metrics and statistics for distributed lock operations.
/// </summary>
public class LockMetrics
{
    public long TotalAcquisitionAttempts { get; set; }
    public long SuccessfulAcquisitions { get; set; }
    public long FailedAcquisitions { get; set; }
    public long TotalRenewals { get; set; }
    public long SuccessfulRenewals { get; set; }
    public long FailedRenewals { get; set; }
    public long TotalReleases { get; set; }
    public long ExpiredLocks { get; set; }
    public double AverageAcquisitionTimeMs { get; set; }
    public double AverageHoldTimeMs { get; set; }
    public long CurrentActiveLocks { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }

    public LockMetrics()
    {
        CreatedAt = DateTime.UtcNow;
        LastUpdatedAt = DateTime.UtcNow;
    }

    // Calculates the success rate for acquisitions
    public double AcquisitionSuccessRate
    {
        get
        {
            if (TotalAcquisitionAttempts == 0) return 0;
            return (double)SuccessfulAcquisitions / TotalAcquisitionAttempts * 100;
        }
    }

    // Calculates the success rate for renewals
    public double RenewalSuccessRate
    {
        get
        {
            var total = SuccessfulRenewals + FailedRenewals;
            if (total == 0) return 0;
            return (double)SuccessfulRenewals / total * 100;
        }
    }

    // Calculates the overall success rate
    public double OverallSuccessRate
    {
        get
        {
            var totalOperations = SuccessfulAcquisitions + FailedAcquisitions +
                                 SuccessfulRenewals + FailedRenewals + TotalReleases;
            if (totalOperations == 0) return 0;
            var successfulOperations = SuccessfulAcquisitions + SuccessfulRenewals + TotalReleases;
            return (double)successfulOperations / totalOperations * 100;
        }
    }

    // Records a successful acquisition
    public void RecordSuccessfulAcquisition(double acquisitionTimeMs)
    {
        TotalAcquisitionAttempts++;
        SuccessfulAcquisitions++;
        CurrentActiveLocks++;
        UpdateAverageAcquisitionTime(acquisitionTimeMs);
        LastUpdatedAt = DateTime.UtcNow;
    }

    // Records a failed acquisition
    public void RecordFailedAcquisition()
    {
        TotalAcquisitionAttempts++;
        FailedAcquisitions++;
        LastUpdatedAt = DateTime.UtcNow;
    }

    // Records a successful renewal
    public void RecordSuccessfulRenewal()
    {
        TotalRenewals++;
        SuccessfulRenewals++;
        LastUpdatedAt = DateTime.UtcNow;
    }

    // Records a failed renewal
    public void RecordFailedRenewal()
    {
        TotalRenewals++;
        FailedRenewals++;
        LastUpdatedAt = DateTime.UtcNow;
    }

    // Records a successful release
    public void RecordRelease(double holdTimeMs)
    {
        TotalReleases++;
        CurrentActiveLocks--;
        UpdateAverageHoldTime(holdTimeMs);
        LastUpdatedAt = DateTime.UtcNow;
    }

    // Records an expired lock
    public void RecordExpiredLock()
    {
        ExpiredLocks++;
        CurrentActiveLocks--;
        LastUpdatedAt = DateTime.UtcNow;
    }

    private void UpdateAverageAcquisitionTime(double newTimeMs)
    {
        var totalMs = AverageAcquisitionTimeMs * (SuccessfulAcquisitions - 1) + newTimeMs;
        AverageAcquisitionTimeMs = totalMs / SuccessfulAcquisitions;
    }

    private void UpdateAverageHoldTime(double newTimeMs)
    {
        var totalMs = AverageHoldTimeMs * (TotalReleases - 1) + newTimeMs;
        AverageHoldTimeMs = totalMs / TotalReleases;
    }

    public override string ToString() =>
        $"LockMetrics(Active={CurrentActiveLocks}, AcqSuccess={AcquisitionSuccessRate:F2}%, " +
        $"RenewSuccess={RenewalSuccessRate:F2}%, AvgAcqTime={AverageAcquisitionTimeMs:F2}ms, " +
        $"AvgHoldTime={AverageHoldTimeMs:F2}ms)";
}
