#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock.Models;

namespace SarmKadan.DistributedLock.Services;

/// <summary>
/// Monitors locks and handles automatic renewal based on configuration.
/// </summary>
public sealed class LockMonitor : IDisposable
{
    private readonly ILockService _lockService;
    private readonly ILogger<LockMonitor> _logger;
    private readonly Dictionary<string, MonitoredLock> _monitoredLocks = new();
    private readonly ReaderWriterLockSlim _lockSlim = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _monitoringTask;

    public LockMonitor(ILockService lockService, ILogger<LockMonitor> logger)
    {
        _lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Registers a lock for monitoring and auto-renewal
    public void RegisterLock(string lockKey, string ownerId, TimeSpan renewalInterval, TimeSpan lockDuration)
    {
        _lockSlim.EnterWriteLock();
        try
        {
            if (!_monitoredLocks.ContainsKey(lockKey))
            {
                _monitoredLocks[lockKey] = new MonitoredLock
                {
                    LockKey = lockKey,
                    OwnerId = ownerId,
                    RenewalInterval = renewalInterval,
                    LockDuration = lockDuration,
                    LastRenewalAttempt = DateTime.UtcNow
                };
                _logger.LogInformation("Registered lock for monitoring: {LockKey}", lockKey);
            }
        }
        finally
        {
            _lockSlim.ExitWriteLock();
        }
    }

    // Unregisters a lock from monitoring
    public void UnregisterLock(string lockKey)
    {
        _lockSlim.EnterWriteLock();
        try
        {
            if (_monitoredLocks.Remove(lockKey))
            {
                _logger.LogInformation("Unregistered lock from monitoring: {LockKey}", lockKey);
            }
        }
        finally
        {
            _lockSlim.ExitWriteLock();
        }
    }

    // Starts the monitoring loop
    public void StartMonitoring(TimeSpan? monitoringInterval = null)
    {
        if (_cancellationTokenSource is not null)
        {
            _logger.LogWarning("Monitoring is already running");
            return;
        }

        var interval = monitoringInterval ?? TimeSpan.FromMilliseconds(Constants.LockConstants.DefaultMonitoringIntervalMilliseconds);
        _cancellationTokenSource = new CancellationTokenSource();

        _monitoringTask = MonitoringLoopAsync(interval, _cancellationTokenSource.Token);
        _logger.LogInformation("Lock monitoring started with interval {Interval}ms", interval.TotalMilliseconds);
    }

    // Stops the monitoring loop
    public async Task StopMonitoringAsync()
    {
        if (_cancellationTokenSource is null)
        {
            _logger.LogWarning("Monitoring is not running");
            return;
        }

        _cancellationTokenSource.Cancel();
        if (_monitoringTask is not null)
        {
            await _monitoringTask;
        }
        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = null;
        _logger.LogInformation("Lock monitoring stopped");
    }

    // Gets the current list of monitored locks
    public IEnumerable<string> GetMonitoredLocks()
    {
        _lockSlim.EnterReadLock();
        try
        {
            return _monitoredLocks.Keys.ToList();
        }
        finally
        {
            _lockSlim.ExitReadLock();
        }
    }

    private async Task MonitoringLoopAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, cancellationToken);

                _lockSlim.EnterReadLock();
                List<MonitoredLock> locksToRenew;
                try
                {
                    var now = DateTime.UtcNow;
                    locksToRenew = _monitoredLocks.Values
                        .Where(ml => (now - ml.LastRenewalAttempt) >= ml.RenewalInterval)
                        .ToList();
                }
                finally
                {
                    _lockSlim.ExitReadLock();
                }

                foreach (var monitoredLock in locksToRenew)
                {
                    try
                    {
                        var renewed = await _lockService.RenewAsync(
                            monitoredLock.LockKey,
                            monitoredLock.OwnerId,
                            monitoredLock.LockDuration,
                            cancellationToken
                        );

                        if (renewed)
                        {
                            _lockSlim.EnterWriteLock();
                            try
                            {
                                if (_monitoredLocks.TryGetValue(monitoredLock.LockKey, out var ml))
                                {
                                    ml.LastRenewalAttempt = DateTime.UtcNow;
                                    ml.RenewalCount++;
                                }
                            }
                            finally
                            {
                                _lockSlim.ExitWriteLock();
                            }
                            _logger.LogDebug("Auto-renewed lock: {LockKey}", monitoredLock.LockKey);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to auto-renew lock: {LockKey}", monitoredLock.LockKey);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during auto-renewal for lock {LockKey}", monitoredLock.LockKey);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in monitoring loop");
            }
        }
    }

    public void Dispose()
    {
        StopMonitoringAsync().GetAwaiter().GetResult();
        _lockSlim?.Dispose();
    }

    private class MonitoredLock
    {
        public string LockKey { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public TimeSpan RenewalInterval { get; set; }
        public TimeSpan LockDuration { get; set; }
        public DateTime LastRenewalAttempt { get; set; }
        public int RenewalCount { get; set; }
    }
}
