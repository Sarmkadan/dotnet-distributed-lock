#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Workers;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock.Caching;

/// <summary>
/// Background worker that periodically collects and aggregates metrics.
/// Gathers statistics on lock operations, cache performance, and system health.
/// Useful for monitoring, alerting, and performance optimization.
/// </summary>
public class MetricsCollectionWorker : BackgroundService
{
    private readonly ILockCacheManager _cacheManager;
    private readonly ILogger<MetricsCollectionWorker> _logger;
    private readonly MetricsCollectionWorkerOptions _options;
    private readonly List<MetricsSnapshot> _snapshots;

    public MetricsCollectionWorker(
        ILockCacheManager cacheManager,
        ILogger<MetricsCollectionWorker> logger,
        MetricsCollectionWorkerOptions? options = null)
    {
        _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new MetricsCollectionWorkerOptions();
        _snapshots = new List<MetricsSnapshot>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Metrics collection worker started");

        // Initial delay before starting collection
        await Task.Delay(_options.InitialDelayMs, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectMetricsAsync(stoppingToken);
                await Task.Delay(_options.CollectionIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in metrics collection worker");
            }
        }

        _logger.LogInformation("Metrics collection worker stopped");
    }

    /// <summary>
    /// Collects current metrics from various sources.
    /// </summary>
    private async Task CollectMetricsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = new MetricsSnapshot
            {
                Timestamp = DateTime.UtcNow,
                CacheStatistics = _cacheManager.GetStatistics()
            };

            lock (_snapshots)
            {
                _snapshots.Add(snapshot);

                // Keep only recent snapshots
                var cutoff = DateTime.UtcNow.AddSeconds(-_options.SnapshotRetentionSeconds);
                _snapshots.RemoveAll(s => s.Timestamp < cutoff);
            }

            _logger.LogDebug(
                "Collected metrics - Cache: {CachedItems} items, {HitRate:F2}% hit rate",
                snapshot.CacheStatistics.CachedItems,
                snapshot.CacheStatistics.HitRate);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect metrics");
        }
    }

    /// <summary>
    /// Gets the current metrics snapshot.
    /// </summary>
    public MetricsSnapshot? GetCurrentSnapshot()
    {
        lock (_snapshots)
        {
            return _snapshots.OrderByDescending(s => s.Timestamp).FirstOrDefault();
        }
    }

    /// <summary>
    /// Gets all metrics snapshots within a time window.
    /// </summary>
    public List<MetricsSnapshot> GetSnapshots(TimeSpan window)
    {
        var cutoff = DateTime.UtcNow.Subtract(window);

        lock (_snapshots)
        {
            return _snapshots.Where(s => s.Timestamp >= cutoff).ToList();
        }
    }

    /// <summary>
    /// Calculates average metrics over a time period.
    /// </summary>
    public MetricsSnapshot? GetAverageMetrics(TimeSpan window)
    {
        var snapshots = GetSnapshots(window);

        if (snapshots.Count == 0)
            return null;

        return new MetricsSnapshot
        {
            Timestamp = DateTime.UtcNow,
            CacheStatistics = new CacheStatistics
            {
                CachedItems = (int)snapshots.Average(s => s.CacheStatistics.CachedItems),
                Hits = snapshots.Sum(s => s.CacheStatistics.Hits),
                Misses = snapshots.Sum(s => s.CacheStatistics.Misses),
                HitRate = snapshots.Average(s => s.CacheStatistics.HitRate)
            }
        };
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping metrics collection worker");
        await base.StopAsync(cancellationToken);
    }
}

/// <summary>
/// A point-in-time snapshot of system metrics.
/// </summary>
public class MetricsSnapshot
{
    public DateTime Timestamp { get; set; }
    public CacheStatistics? CacheStatistics { get; set; }
    public Dictionary<string, object> CustomMetrics { get; set; } = new();
}

/// <summary>
/// Configuration for metrics collection worker.
/// </summary>
public class MetricsCollectionWorkerOptions
{
    /// <summary>
    /// Initial delay before first collection (milliseconds).
    /// </summary>
    public int InitialDelayMs { get; set; } = 5000; // 5 seconds

    /// <summary>
    /// Interval between metric collections (milliseconds).
    /// </summary>
    public int CollectionIntervalMs { get; set; } = 60000; // 1 minute

    /// <summary>
    /// How long to retain metric snapshots (seconds).
    /// Older snapshots are automatically removed.
    /// </summary>
    public int SnapshotRetentionSeconds { get; set; } = 3600; // 1 hour

    /// <summary>
    /// Enable detailed logging of metrics.
    /// </summary>
    public bool VerboseLogging { get; set; } = false;
}
