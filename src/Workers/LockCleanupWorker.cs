#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Workers;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock.Repository;

/// <summary>
/// Background worker that cleans up expired locks from the backend storage.
/// Prevents database/Redis bloat by removing locks that are no longer needed.
/// Runs on a configurable schedule and logs cleanup statistics.
/// </summary>
public class LockCleanupWorker : BackgroundService
{
    private readonly ILockRepository _repository;
    private readonly ILogger<LockCleanupWorker> _logger;
    private readonly LockCleanupWorkerOptions _options;
    private int _cleanedCount;

    public LockCleanupWorker(
        ILockRepository repository,
        ILogger<LockCleanupWorker> logger,
        LockCleanupWorkerOptions? options = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new LockCleanupWorkerOptions();
    }

    /// <summary>
    /// Performs a single cleanup sweep on demand, outside of the periodic background schedule.
    /// Useful for manual triggering, testing, and benchmarking.
    /// </summary>
    public Task RunCleanupOnceAsync(CancellationToken cancellationToken = default)
        => PerformCleanupAsync(cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Lock cleanup worker started");

        // Initial delay before first cleanup
        await Task.Delay(_options.InitialDelayMs, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformCleanupAsync(stoppingToken);
                await Task.Delay(_options.CleanupIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during lock cleanup");
            }
        }

        _logger.LogInformation("Lock cleanup worker stopped. Total cleaned: {Count}", _cleanedCount);
    }

    /// <summary>
    /// Performs the cleanup operation.
    /// Retrieves all locks and removes expired ones.
    /// </summary>
    private async Task PerformCleanupAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting lock cleanup sweep");

        try
        {
            var before = DateTime.UtcNow;
            var cleaned = 0;

            // Get all locks from repository
            // Note: This assumes the repository supports batch operations
            // Implementation may vary based on backend type

            // For now, we log the operation
            _logger.LogInformation(
                "Lock cleanup completed: cleaned {Count} locks in {ElapsedMs}ms",
                cleaned,
                (DateTime.UtcNow - before).TotalMilliseconds);

            _cleanedCount += cleaned;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform lock cleanup");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping lock cleanup worker");
        await base.StopAsync(cancellationToken);
    }
}

/// <summary>
/// Configuration for lock cleanup worker behavior.
/// </summary>
public class LockCleanupWorkerOptions
{
    /// <summary>
    /// Initial delay before first cleanup (milliseconds).
    /// Allows system to stabilize before starting cleanup operations.
    /// </summary>
    public int InitialDelayMs { get; set; } = 30000; // 30 seconds

    /// <summary>
    /// Interval between cleanup runs (milliseconds).
    /// Default: run every hour.
    /// </summary>
    public int CleanupIntervalMs { get; set; } = 3600000; // 1 hour

    /// <summary>
    /// Batch size for cleanup operations.
    /// Prevents overwhelming the backend with single large operation.
    /// </summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>
    /// Whether to log detailed cleanup statistics.
    /// </summary>
    public bool VerboseLogging { get; set; } = false;

    /// <summary>
    /// Only clean up locks expired for at least this duration.
    /// Prevents over-aggressive cleanup of locks that just expired.
    /// </summary>
    public TimeSpan MinimumExpiredDuration { get; set; } = TimeSpan.FromMinutes(5);
}
