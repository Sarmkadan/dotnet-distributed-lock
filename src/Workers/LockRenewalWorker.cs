#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Workers;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock.Core.Exceptions;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using SarmKadan.DistributedLock.Services;
using SarmKadan.DistributedLock.Utilities.Extensions;

/// <summary>
/// Background worker that automatically renews locks before they expire.
/// Monitors locks with auto-renew enabled and extends their duration.
/// Essential for maintaining locks in long-running operations.
/// </summary>
public class LockRenewalWorker : BackgroundService
{
    private readonly ILockService _lockService;
    private readonly ILogger<LockRenewalWorker> _logger;
    private readonly LockRenewalWorkerOptions _options;
    private readonly ConcurrentDictionary<string, RenewalSchedule> _renewalSchedules;

    public LockRenewalWorker(
        ILockService lockService,
        ILogger<LockRenewalWorker> logger,
        LockRenewalWorkerOptions? options = null)
    {
        _lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new LockRenewalWorkerOptions();
        _renewalSchedules = new ConcurrentDictionary<string, RenewalSchedule>();
    }

    /// <summary>
    /// Registers a lock for automatic renewal.
    /// </summary>
    public void RegisterForRenewal(string lockId, ulong fencingToken, TimeSpan renewalInterval)
    {
        var schedule = new RenewalSchedule
        {
            LockId = lockId,
            FencingToken = fencingToken,
            RenewalInterval = renewalInterval,
            NextRenewalTime = DateTime.UtcNow.Add(renewalInterval)
        };

        _renewalSchedules.AddOrUpdate(lockId, schedule, (_, __) => schedule);
        _logger.LogInformation("Registered lock for renewal: {LockId}", lockId);
    }

    /// <summary>
    /// Checks if a lock is currently registered for renewal.
    /// </summary>
    /// <param name="lockId">The unique identifier of the lock.</param>
    /// <returns>True if the lock is registered for renewal; otherwise, false.</returns>
    public bool IsRegisteredForRenewal(string lockId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockId);
        return _renewalSchedules.ContainsKey(lockId);
    }

    /// <summary>
    /// Gets the renewal schedule for a lock if it exists.
    /// </summary>
    /// <param name="lockId">The unique identifier of the lock.</param>
    /// <param name="schedule">When this method returns, contains the renewal schedule if found; otherwise, null.</param>
    /// <returns>True if the schedule was found; otherwise, false.</returns>
    public bool TryGetRenewalSchedule(string lockId, [NotNullWhen(true)] out RenewalSchedule? schedule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockId);
        return _renewalSchedules.TryGetValue(lockId, out schedule);
    }

    /// <summary>
    /// Gets the time remaining until the next scheduled renewal for the specified lock.
    /// </summary>
    /// <param name="lockId">The unique identifier of the lock.</param>
    /// <returns>The time remaining until next renewal, or null if lock is not registered or renewal is not due.</returns>
    public TimeSpan? GetTimeUntilNextRenewal(string lockId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockId);

        if (_renewalSchedules.TryGetValue(lockId, out var schedule) && schedule.NextRenewalTime > DateTime.UtcNow)
        {
            return schedule.NextRenewalTime - DateTime.UtcNow;
        }

        return null;
    }

    /// <summary>
    /// Unregisters a lock from automatic renewal.
    /// </summary>
    public void UnregisterFromRenewal(string lockId)
    {
        if (_renewalSchedules.TryRemove(lockId, out _))
        {
            _logger.LogInformation("Unregistered lock from renewal: {LockId}", lockId);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Lock renewal worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRenewalsAsync(stoppingToken);
                await Task.Delay(_options.CheckIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in lock renewal worker");
            }
        }

        _logger.LogInformation("Lock renewal worker stopped");
    }

    /// <summary>
    /// Processes all pending lock renewals.
    /// </summary>
    private async Task ProcessRenewalsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var dueLocks = _renewalSchedules.Values
            .Where(s => s.NextRenewalTime <= now)
            .ToList();

        if (dueLocks.Count == 0)
            return;

        _logger.LogInformation("Processing {Count} locks due for renewal", dueLocks.Count);

        var renewalTasks = dueLocks.Select(schedule =>
            RenewLockAsync(schedule, cancellationToken));

        await Task.WhenAll(renewalTasks);
    }

    /// <summary>
    /// Renews a single lock if it still needs renewal.
    /// </summary>
    private async Task RenewLockAsync(RenewalSchedule schedule, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Renewing lock: {LockId}", schedule.LockId);

            var additionalDuration = (int)schedule.RenewalInterval.TotalSeconds;
            var updatedLock = await _lockService.RenewLockAsync(
                schedule.LockId,
                schedule.FencingToken,
                TimeSpan.FromSeconds(additionalDuration),
                cancellationToken);

            // Reschedule next renewal
            schedule.NextRenewalTime = updatedLock.ExpiresAt
                .Subtract(schedule.RenewalInterval.AddRandomJitter(_options.JitterPercentage));

            _logger.LogDebug("Successfully renewed lock: {LockId}", schedule.LockId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to renew lock: {LockId}", schedule.LockId); // Change to Error

            // Propagate the exception as LockRenewalFailedException
            throw new LockRenewalFailedException(
                schedule.LockId,
                $"Lock renewal failed for lock ID '{schedule.LockId}'.",
                ex);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping lock renewal worker");
        await base.StopAsync(cancellationToken);
    }

    public sealed class RenewalSchedule
    {
        public required string LockId { get; init; }
        public required ulong FencingToken { get; init; }
        public required TimeSpan RenewalInterval { get; init; }
        public DateTime NextRenewalTime { get; set; }
    }
}

/// <summary>
/// Configuration for lock renewal worker behavior.
/// </summary>
public class LockRenewalWorkerOptions
{
    /// <summary>
    /// How often to check for locks that need renewal (milliseconds).
    /// </summary>
    public int CheckIntervalMs { get; set; } = 5000;

    /// <summary>
    /// Delay before retrying failed renewals (seconds).
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 10;

    /// <summary>
    /// Maximum jitter to apply to renewal timing (percentage).
    /// Prevents thundering herd of renewals at same time.
    /// </summary>
    public double JitterPercentage { get; set; } = 10;
}
