#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Models;

/// <summary>
/// Captures lock contention statistics for a single lock key.
/// Tracks concurrent waiter counts, wait durations, and deadlock occurrences.
/// All mutation methods are safe to call from multiple threads.
/// </summary>
public class ContentionMetrics
{
    private long _totalContentionEvents;
    private long _peakWaiterCount;
    private long _totalWaiterRegistrations;
    private long _deadlocksDetected;
    private long _waitTimeSamples;
    private double _totalWaitTimeMs;
    private int _currentWaiters;

    private readonly object _syncRoot = new();

    /// <summary>Gets the lock key these metrics apply to.</summary>
    public string LockKey { get; }

    /// <summary>Gets the instantaneous number of owners waiting to acquire this lock.</summary>
    public int CurrentWaiters => Volatile.Read(ref _currentWaiters);

    /// <summary>Gets the highest simultaneous waiter count ever observed.</summary>
    public long PeakWaiters => Interlocked.Read(ref _peakWaiterCount);

    /// <summary>Gets the total number of contention events (acquisitions that found at least one other waiter).</summary>
    public long TotalContentionEvents => Interlocked.Read(ref _totalContentionEvents);

    /// <summary>Gets the cumulative number of individual waiter registrations.</summary>
    public long TotalWaiterRegistrations => Interlocked.Read(ref _totalWaiterRegistrations);

    /// <summary>Gets the number of deadlock cycles detected involving this lock.</summary>
    public long DeadlocksDetected => Interlocked.Read(ref _deadlocksDetected);

    /// <summary>
    /// Gets the average time in milliseconds that a caller waited before acquiring or abandoning the lock.
    /// Returns zero when no completed wait samples are available.
    /// </summary>
    public double AverageWaitTimeMs
    {
        get
        {
            var samples = Interlocked.Read(ref _waitTimeSamples);
            return samples == 0 ? 0d : _totalWaitTimeMs / samples;
        }
    }

    /// <summary>Gets the UTC timestamp when this object was created.</summary>
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    /// <summary>Gets the UTC timestamp of the most recent mutation.</summary>
    public DateTime LastUpdatedAt { get; private set; } = DateTime.UtcNow;

    /// <summary>Initializes a new <see cref="ContentionMetrics"/> for the given lock key.</summary>
    /// <param name="lockKey">The key of the lock being tracked.</param>
    public ContentionMetrics(string lockKey)
    {
        LockKey = lockKey ?? throw new ArgumentNullException(nameof(lockKey));
    }

    /// <summary>
    /// Records a new caller entering the contention queue.
    /// Raises the peak counter when the current waiter count exceeds the previous maximum.
    /// A contention event is counted whenever more than one waiter is present simultaneously.
    /// </summary>
    public void RecordWaiterAdded()
    {
        lock (_syncRoot)
        {
            _currentWaiters++;
            if (_currentWaiters > _peakWaiterCount)
                Interlocked.Exchange(ref _peakWaiterCount, _currentWaiters);

            if (_currentWaiters > 1)
                Interlocked.Increment(ref _totalContentionEvents);
        }

        Interlocked.Increment(ref _totalWaiterRegistrations);
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a caller leaving the contention queue and captures how long it waited.
    /// </summary>
    /// <param name="waitTimeMs">Elapsed wait time in milliseconds (must be non-negative).</param>
    public void RecordWaiterRemoved(double waitTimeMs)
    {
        if (waitTimeMs < 0) waitTimeMs = 0;

        lock (_syncRoot)
        {
            if (_currentWaiters > 0)
                _currentWaiters--;
        }

        Interlocked.Increment(ref _waitTimeSamples);
        _totalWaitTimeMs += waitTimeMs;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Increments the deadlock counter for this lock key.</summary>
    public void RecordDeadlock()
    {
        Interlocked.Increment(ref _deadlocksDetected);
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <inheritdoc />
    public override string ToString() =>
        $"ContentionMetrics(Key={LockKey}, Waiters={CurrentWaiters}, Peak={PeakWaiters}, " +
        $"Deadlocks={DeadlocksDetected}, AvgWait={AverageWaitTimeMs:F2}ms)";
}
