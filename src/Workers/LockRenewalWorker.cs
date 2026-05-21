#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Workers;

using System.Collections.Concurrent;
using SarmKadan.DistributedLock.Core.Services;
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
                await ProcessRenewalsAsync(stoppingToken).ConfigureAwait(false);
                await Task.Delay(_options.CheckIntervalMs, stoppingToken).ConfigureAwait(false);
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

        await Task.WhenAll(renewalTasks).ConfigureAwait(false);
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
                .Subtract(schedule.RenewalInterval)
                .AddRandomJitter(_options.JitterPercentage);

            _logger.LogDebug("Successfully renewed lock: {LockId}", schedule.LockId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to renew lock: {LockId}", schedule.LockId);

            // On failure, retry sooner rather than later
            schedule.NextRenewalTime = DateTime.UtcNow.AddSeconds(_options.RetryDelaySeconds);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping lock renewal worker");
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private class RenewalSchedule
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
