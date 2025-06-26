#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock.Models;

namespace SarmKadan.DistributedLock.Services;

/// <summary>
/// Tracks lock contention and detects potential deadlocks across distributed lock operations.
/// </summary>
public interface IDeadlockDetector
{
    /// <summary>
    /// Notifies the detector that <paramref name="ownerId"/> is now waiting to acquire <paramref name="lockKey"/>.
    /// If recording this waiter would complete a circular wait chain the detector logs a warning
    /// and increments the deadlock counter for the lock.
    /// </summary>
    /// <param name="ownerId">Identifier of the caller waiting for the lock.</param>
    /// <param name="lockKey">Key of the lock being requested.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    Task RecordWaitingAsync(string ownerId, string lockKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies the detector that <paramref name="ownerId"/> is no longer waiting for <paramref name="lockKey"/>
    /// and records how long the wait lasted.
    /// </summary>
    /// <param name="ownerId">Identifier of the caller that stopped waiting.</param>
    /// <param name="lockKey">Key of the lock that was waited on.</param>
    /// <param name="waitTimeMs">Total wait duration in milliseconds.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    Task RecordWaitEndedAsync(string ownerId, string lockKey, double waitTimeMs, CancellationToken cancellationToken = default);

    /// <summary>Records that <paramref name="ownerId"/> has successfully acquired <paramref name="lockKey"/>.</summary>
    /// <param name="ownerId">Identifier of the new lock owner.</param>
    /// <param name="lockKey">Key of the acquired lock.</param>
    void RecordAcquired(string ownerId, string lockKey);

    /// <summary>Removes the ownership record when <paramref name="ownerId"/> releases <paramref name="lockKey"/>.</summary>
    /// <param name="ownerId">Identifier of the owner releasing the lock.</param>
    /// <param name="lockKey">Key of the lock being released.</param>
    void RecordReleased(string ownerId, string lockKey);

    /// <summary>
    /// Determines whether registering <paramref name="ownerId"/> as a waiter for <paramref name="lockKey"/>
    /// would create a circular wait (deadlock) based on the current ownership and wait-for state.
    /// When a cycle is found the detector logs a warning and increments the deadlock counter
    /// for <paramref name="lockKey"/>.
    /// </summary>
    /// <param name="ownerId">Candidate waiter to evaluate.</param>
    /// <param name="lockKey">Lock the candidate wants to acquire.</param>
    /// <returns><c>true</c> if a deadlock cycle would be formed; otherwise <c>false</c>.</returns>
    bool WouldDeadlock(string ownerId, string lockKey);

    /// <summary>Returns contention metrics for a specific lock key, or <c>null</c> if no data has been recorded yet.</summary>
    /// <param name="lockKey">Lock key to query.</param>
    ContentionMetrics? GetMetrics(string lockKey);

    /// <summary>Returns a snapshot of contention metrics for all tracked lock keys.</summary>
    IReadOnlyCollection<ContentionMetrics> GetAllMetrics();
}

/// <summary>
/// Default implementation of <see cref="IDeadlockDetector"/>.
/// Maintains an in-memory wait-for graph to detect circular waiter chains.
/// All public methods are safe to call concurrently from multiple threads.
/// </summary>
public sealed class DeadlockDetector : IDeadlockDetector
{
    // lockKey → ownerId currently holding that lock
    private readonly ConcurrentDictionary<string, string> _ownership = new(StringComparer.Ordinal);

    // ownerId → lockKey it is currently waiting for
    private readonly ConcurrentDictionary<string, string> _waitingFor = new(StringComparer.Ordinal);

    // lockKey → per-key contention metrics
    private readonly ConcurrentDictionary<string, ContentionMetrics> _metrics = new(StringComparer.Ordinal);

    private readonly ILogger<DeadlockDetector> _logger;

    /// <summary>Initializes a new <see cref="DeadlockDetector"/>.</summary>
    /// <param name="logger">Logger for deadlock warnings and diagnostic messages.</param>
    public DeadlockDetector(ILogger<DeadlockDetector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task RecordWaitingAsync(string ownerId, string lockKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(lockKey);

        WouldDeadlock(ownerId, lockKey);

        _waitingFor[ownerId] = lockKey;
        GetOrCreateMetrics(lockKey).RecordWaiterAdded();

        _logger.LogDebug("Owner {OwnerId} registered as waiter for lock {LockKey}", ownerId, lockKey);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RecordWaitEndedAsync(
        string ownerId,
        string lockKey,
        double waitTimeMs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(lockKey);

        _waitingFor.TryRemove(ownerId, out _);
        GetOrCreateMetrics(lockKey).RecordWaiterRemoved(waitTimeMs);

        _logger.LogDebug(
            "Owner {OwnerId} ended wait for lock {LockKey} after {WaitTimeMs:F2}ms",
            ownerId, lockKey, waitTimeMs);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void RecordAcquired(string ownerId, string lockKey)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(lockKey);

        _ownership[lockKey] = ownerId;
        _waitingFor.TryRemove(ownerId, out _);
    }

    /// <inheritdoc />
    public void RecordReleased(string ownerId, string lockKey)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(lockKey);

        _ownership.TryRemove(new KeyValuePair<string, string>(lockKey, ownerId));
    }

    /// <inheritdoc />
    public bool WouldDeadlock(string ownerId, string lockKey)
    {
        // Walk the wait-for chain starting at lockKey:
        //   lockKey → its current holder → what that holder is waiting for → ...
        // If ownerId appears as a holder anywhere in the chain, a cycle exists.
        var visitedHolders = new HashSet<string>(StringComparer.Ordinal);
        string? current = lockKey;

        while (current is not null)
        {
            if (!_ownership.TryGetValue(current, out var holder))
                break; // no one holds this lock — chain ends, no deadlock

            if (holder == ownerId)
            {
                // Circular dependency detected: record it against the requested lock.
                GetOrCreateMetrics(lockKey).RecordDeadlock();
                _logger.LogWarning(
                    "Potential deadlock: owner {OwnerId} waiting for lock {LockKey} would form a circular wait chain",
                    ownerId, lockKey);
                return true;
            }

            if (!visitedHolders.Add(holder))
                break; // already visited this holder; avoid infinite loop on concurrent state changes

            _waitingFor.TryGetValue(holder, out current);
        }

        return false;
    }

    /// <inheritdoc />
    public ContentionMetrics? GetMetrics(string lockKey) =>
        _metrics.TryGetValue(lockKey, out var m) ? m : null;

    /// <inheritdoc />
    public IReadOnlyCollection<ContentionMetrics> GetAllMetrics() =>
        _metrics.Values.ToList();

    private ContentionMetrics GetOrCreateMetrics(string lockKey) =>
        _metrics.GetOrAdd(lockKey, key => new ContentionMetrics(key));
}
