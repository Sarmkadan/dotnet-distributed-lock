#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Workers;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock.Exceptions;
using SarmKadan.DistributedLock.Repository;

/// <summary>
/// Background worker that cleans up expired locks from the backend storage.
/// Prevents database/Redis bloat by removing locks that are no longer needed.
/// Runs on a configurable schedule and logs cleanup statistics.
/// </summary>
public class LockCleanupWorker : BackgroundService
{
    private const string StartedMessage = "Lock cleanup worker started";
    private const string StoppingMessage = "Stopping lock cleanup worker";
    private const string StoppedMessage = "Lock cleanup worker stopped. Total cleaned: {Count}";
    private const string SweepStartedMessage = "Starting lock cleanup sweep";
    private const string SweepConcurrentExpiryMessage = "Lock expired concurrently during cleanup sweep for {LockKey}";
    private const string SweepErrorMessage = "Error during lock cleanup";
    private const string SweepCompletedMessage =
        "Lock cleanup completed: cleaned {Count} locks, skipped {SkippedCount} renewed-in-flight locks, in {ElapsedMs}ms";
    private const string BatchLimitReachedMessage =
        "Lock cleanup batch limit reached; deferred {DeferredCount} expired locks to the next sweep";

    private readonly ILockRepository _repository;
    private readonly ILogger<LockCleanupWorker> _logger;
    private readonly LockCleanupWorkerOptions _options;
    private int _cleanedCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="LockCleanupWorker"/> class.
    /// </summary>
    /// <param name="repository">The lock repository to clean up.</param>
    /// <param name="logger">The logger used for sweep statistics.</param>
    /// <param name="options">The cleanup options; defaults are used when <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="repository"/> or <paramref name="logger"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="options"/> has a batch size outside the allowed range or a cleanup interval below the safe floor.
    /// </exception>
    public LockCleanupWorker(
        ILockRepository repository,
        ILogger<LockCleanupWorker> logger,
        LockCleanupWorkerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(logger);

        _repository = repository;
        _logger = logger;
        _options = options ?? new LockCleanupWorkerOptions();
        _options.EnsureValid();
    }

    public override string ToString() =>
        $"LockCleanupWorker {{ InitialDelayMs = {_options.InitialDelayMs}, CleanupIntervalMs = {_options.CleanupIntervalMs}, " +
        $"BatchSize = {_options.BatchSize}, VerboseLogging = {_options.VerboseLogging}, MinimumExpiredDuration = {_options.MinimumExpiredDuration} }}";

    /// <summary>
    /// Performs a single cleanup sweep on demand, outside of the periodic background schedule.
    /// Useful for manual triggering, testing, and benchmarking.
    /// </summary>
    public Task RunCleanupOnceAsync(CancellationToken cancellationToken = default)
        => PerformCleanupAsync(cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(StartedMessage);

        // Initial delay before first cleanup
        await Task.Delay(_options.InitialDelayMs, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformCleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (LockExpiredException ex)
            {
                // Benign: a lock expired again between being read and being deleted
                // (e.g. lost the compare-and-delete race to another sweep). Not a
                // backend failure, so log at a lower level and keep the schedule.
                _logger.LogDebug(ex, SweepConcurrentExpiryMessage, ex.LockKey);
            }
            catch (Exception ex)
            {
                // Transient backend error (connection drop, timeout, etc). Log and
                // keep the loop alive - a single failed sweep must not crash the worker.
                _logger.LogError(ex, SweepErrorMessage);
            }

            // Always wait out the configured interval between sweeps, whether the
            // previous sweep succeeded or failed, so a failing backend doesn't turn
            // this into a tight retry loop.
            try
            {
                await Task.Delay(_options.CleanupIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation(StoppedMessage, _cleanedCount);
    }

    /// <summary>
    /// Performs the cleanup operation by deleting expired locks from the repository.
    /// Locks that have not been expired for at least the configured minimum duration
    /// are skipped, and no more than the configured batch size are submitted for
    /// deletion in a single sweep. Any remaining eligible locks are deferred to the
    /// next sweep.
    /// Uses a read-then-compare-and-delete sequence per lock: the current expired
    /// locks are snapshotted first, then each one is deleted only if its expiration
    /// timestamp still matches what was observed. This closes the race where a
    /// holder renews a lock between the sweep reading it as expired and the sweep
    /// deleting it, which would otherwise delete a now-live lock out from under its owner.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    private async Task PerformCleanupAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug(SweepStartedMessage);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var expiredLocks = await _repository.GetExpiredLocksAsync(cancellationToken);
        var expirationCutoff = DateTime.UtcNow - _options.MinimumExpiredDuration;

        var cleaned = 0;
        var skipped = 0;
        var deletionAttempts = 0;
        var deferred = 0;

        foreach (var expiredLock in expiredLocks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (expiredLock.ExpiresAt > expirationCutoff)
            {
                skipped++;
                continue;
            }

            if (deletionAttempts >= _options.BatchSize)
            {
                deferred++;
                continue;
            }

            deletionAttempts++;
            var deleted = await _repository.DeleteLockIfExpirationMatchesAsync(
                expiredLock.Key,
                expiredLock.ExpiresAt,
                cancellationToken);

            if (deleted)
                cleaned++;
            else
                skipped++;
        }

        stopwatch.Stop();

        Interlocked.Add(ref _cleanedCount, cleaned);

        if (cleaned > 0 || skipped > 0 || _options.VerboseLogging)
        {
            _logger.LogInformation(
                SweepCompletedMessage,
                cleaned,
                skipped,
                stopwatch.Elapsed.TotalMilliseconds);
        }

        if (deferred > 0)
        {
            _logger.LogDebug(
                BatchLimitReachedMessage,
                deferred);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(StoppingMessage);
        await base.StopAsync(cancellationToken);
    }
}

/// <summary>
/// Configuration for lock cleanup worker behavior.
/// </summary>
public class LockCleanupWorkerOptions
{
    public const int DefaultInitialDelayMs = 30_000;
    public const int DefaultCleanupIntervalMs = 3_600_000;
    public const int DefaultBatchSize = 1000;
    public static readonly TimeSpan DefaultMinimumExpiredDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Largest allowed <see cref="BatchSize"/>; bounds the number of deletions per sweep.
    /// </summary>
    public const int MaxBatchSize = 100_000;

    /// <summary>
    /// Smallest allowed <see cref="CleanupIntervalMs"/> (one second); prevents tight-loop hammering of the backend.
    /// </summary>
    public const int MinCleanupIntervalMs = 1_000;

    /// <summary>
    /// Initial delay before first cleanup (milliseconds).
    /// Allows system to stabilize before starting cleanup operations.
    /// </summary>
    public int InitialDelayMs { get; set; } = DefaultInitialDelayMs;

    /// <summary>
    /// Interval between cleanup runs (milliseconds).
    /// Must be at least <see cref="MinCleanupIntervalMs"/>. Default: run every hour.
    /// </summary>
    public int CleanupIntervalMs { get; set; } = DefaultCleanupIntervalMs;

    /// <summary>
    /// Batch size for cleanup operations, between 1 and <see cref="MaxBatchSize"/> inclusive.
    /// Prevents overwhelming the backend with single large operation.
    /// </summary>
    public int BatchSize { get; set; } = DefaultBatchSize;

    /// <summary>
    /// Whether to log detailed cleanup statistics.
    /// </summary>
    public bool VerboseLogging { get; set; } = false;

    /// <summary>
    /// Only clean up locks expired for at least this duration.
    /// Prevents over-aggressive cleanup of locks that just expired.
    /// </summary>
    public TimeSpan MinimumExpiredDuration { get; set; } = DefaultMinimumExpiredDuration;

    /// <summary>
    /// Ensures the batch size and cleanup interval are within their safe bounds.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="BatchSize"/> is less than 1 or greater than <see cref="MaxBatchSize"/>,
    /// or <see cref="CleanupIntervalMs"/> is less than <see cref="MinCleanupIntervalMs"/>.
    /// </exception>
    public void EnsureValid()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(BatchSize, 1, nameof(BatchSize));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(BatchSize, MaxBatchSize, nameof(BatchSize));
        ArgumentOutOfRangeException.ThrowIfLessThan(CleanupIntervalMs, MinCleanupIntervalMs, nameof(CleanupIntervalMs));
    }
}
