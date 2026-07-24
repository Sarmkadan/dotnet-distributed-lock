#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Services;

/// <summary>
/// Abstraction over the storage and retrieval of lock operation metrics.
/// Decouples metric aggregation from any particular hosting model (ASP.NET Core,
/// background worker, in-process library usage, etc.), so metrics can be recorded
/// and queried without depending on the web host being present.
/// </summary>
public interface IMetricsStore
{
    /// <summary>
    /// Records the outcome of a single lock acquisition attempt for a named lock.
    /// </summary>
    /// <param name="lockName">The name of the lock the attempt was made against.</param>
    /// <param name="successful">Whether the acquisition attempt succeeded.</param>
    /// <param name="holdTimeMs">The hold time, in milliseconds, associated with the attempt.</param>
    /// <param name="contentionDetected">Whether contention was detected for this attempt.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="lockName"/> is null or empty.</exception>
    void RecordAcquisitionAttempt(string lockName, bool successful, long holdTimeMs, bool contentionDetected);

    /// <summary>
    /// Attempts to retrieve the current metrics snapshot for a specific lock.
    /// </summary>
    /// <param name="lockName">The name of the lock to look up.</param>
    /// <param name="metrics">
    /// When this method returns <see langword="true"/>, contains the metrics snapshot for the lock;
    /// otherwise, <see langword="null"/>.
    /// </param>
    /// <returns><see langword="true"/> if metrics exist for the given lock; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="lockName"/> is null or empty.</exception>
    bool TryGetLockMetrics(string lockName, out LockMetricsSnapshot? metrics);

    /// <summary>
    /// Gets a snapshot of metrics for every lock currently tracked by the store.
    /// </summary>
    /// <returns>A read-only collection of per-lock metric snapshots, keyed by lock name.</returns>
    IReadOnlyDictionary<string, LockMetricsSnapshot> GetAllLockMetrics();

    /// <summary>
    /// Clears all recorded metrics from the store.
    /// </summary>
    void Reset();
}

/// <summary>
/// An immutable snapshot of the metrics tracked for a single named lock.
/// </summary>
public sealed record LockMetricsSnapshot
{
    /// <summary>Gets the total number of acquisition attempts recorded for the lock.</summary>
    public long AcquisitionAttempts { get; init; }

    /// <summary>Gets the number of successful acquisition attempts recorded for the lock.</summary>
    public long SuccessfulAcquisitions { get; init; }

    /// <summary>Gets the number of failed acquisition attempts recorded for the lock.</summary>
    public long FailedAcquisitions { get; init; }

    /// <summary>Gets the running average hold time, in milliseconds, for the lock.</summary>
    public double AverageHoldTimeMs { get; init; }

    /// <summary>Gets the maximum observed hold time, in milliseconds, for the lock.</summary>
    public long MaxHoldTimeMs { get; init; }

    /// <summary>Gets the number of contention events recorded for the lock.</summary>
    public long ContentionCount { get; init; }

    /// <summary>Gets the timestamp of the most recent acquisition attempt, if any.</summary>
    public DateTime? LastAcquisitionTime { get; init; }
}
