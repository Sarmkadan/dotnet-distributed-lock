#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Services;

using System.Collections.Concurrent;

/// <summary>
/// Thread-safe, in-memory implementation of <see cref="IMetricsStore"/>.
/// Intended to be registered as a singleton so a single set of counters is shared
/// between the component that records metrics (typically an event subscriber) and
/// any component that reads them (such as an HTTP controller).
/// </summary>
public sealed class InMemoryMetricsStore : IMetricsStore
{
    private sealed class MutableLockMetrics
    {
        public long AcquisitionAttempts;
        public long SuccessfulAcquisitions;
        public long FailedAcquisitions;
        public double AverageHoldTimeMs;
        public long MaxHoldTimeMs;
        public long ContentionCount;
        public DateTime? LastAcquisitionTime;
        public readonly object SyncRoot = new();
    }

    private readonly ConcurrentDictionary<string, MutableLockMetrics> _metrics = new();

    /// <inheritdoc />
    /// <exception cref="ArgumentException">Thrown when <paramref name="lockName"/> is null or empty.</exception>
    public void RecordAcquisitionAttempt(string lockName, bool successful, long holdTimeMs, bool contentionDetected)
    {
        ArgumentException.ThrowIfNullOrEmpty(lockName);

        var entry = _metrics.GetOrAdd(lockName, static _ => new MutableLockMetrics());

        lock (entry.SyncRoot)
        {
            entry.AcquisitionAttempts++;

            if (successful)
                entry.SuccessfulAcquisitions++;
            else
                entry.FailedAcquisitions++;

            entry.AverageHoldTimeMs = (entry.AverageHoldTimeMs + holdTimeMs) / 2;
            entry.MaxHoldTimeMs = Math.Max(entry.MaxHoldTimeMs, holdTimeMs);
            entry.LastAcquisitionTime = DateTime.UtcNow;

            if (contentionDetected)
                entry.ContentionCount++;
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">Thrown when <paramref name="lockName"/> is null or empty.</exception>
    public bool TryGetLockMetrics(string lockName, out LockMetricsSnapshot? metrics)
    {
        ArgumentException.ThrowIfNullOrEmpty(lockName);

        if (!_metrics.TryGetValue(lockName, out var entry))
        {
            metrics = null;
            return false;
        }

        metrics = ToSnapshot(entry);
        return true;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, LockMetricsSnapshot> GetAllLockMetrics() =>
        _metrics.ToDictionary(kvp => kvp.Key, kvp => ToSnapshot(kvp.Value));

    /// <inheritdoc />
    public void Reset() => _metrics.Clear();

    private static LockMetricsSnapshot ToSnapshot(MutableLockMetrics entry)
    {
        lock (entry.SyncRoot)
        {
            return new LockMetricsSnapshot
            {
                AcquisitionAttempts = entry.AcquisitionAttempts,
                SuccessfulAcquisitions = entry.SuccessfulAcquisitions,
                FailedAcquisitions = entry.FailedAcquisitions,
                AverageHoldTimeMs = entry.AverageHoldTimeMs,
                MaxHoldTimeMs = entry.MaxHoldTimeMs,
                ContentionCount = entry.ContentionCount,
                LastAcquisitionTime = entry.LastAcquisitionTime
            };
        }
    }
}
