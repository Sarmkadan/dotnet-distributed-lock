#nullable enable

namespace SarmKadan.DistributedLock.Workers;

using System.Diagnostics;
using Microsoft.Extensions.Logging;

/// <summary>
/// Extension methods for <see cref="LockCleanupWorker"/> that provide additional utility functionality
/// for working with lock cleanup operations.
/// </summary>
public static class LockCleanupWorkerExtensions
{
    /// <summary>
    /// Runs cleanup with a timeout to prevent indefinite blocking.
    /// </summary>
    /// <param name="worker">The lock cleanup worker instance</param>
    /// <param name="timeout">Maximum time to wait for cleanup to complete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the cleanup operation</returns>
    public static async Task RunCleanupOnceAsyncWithTimeout(this LockCleanupWorker worker, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await worker.RunCleanupOnceAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException($"Lock cleanup did not complete within the specified timeout of {timeout.TotalSeconds} seconds");
        }
    }

    /// <summary>
    /// Runs cleanup and returns detailed statistics about the operation.
    /// </summary>
    /// <param name="worker">The lock cleanup worker instance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple containing cleanup duration and count of cleaned locks</returns>
    public static async Task<(TimeSpan Duration, int CleanedCount)> RunCleanupOnceAsyncWithStats(this LockCleanupWorker worker, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var startCount = worker.GetCleanedCount();

        await worker.RunCleanupOnceAsync(cancellationToken);

        stopwatch.Stop();
        var endCount = worker.GetCleanedCount();
        var cleanedCount = endCount - startCount;

        return (TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds), cleanedCount);
    }

    /// <summary>
    /// Gets the total count of locks cleaned by this worker instance.
    /// </summary>
    /// <param name="worker">The lock cleanup worker instance</param>
    /// <returns>Total count of cleaned locks</returns>
    public static int GetCleanedCount(this LockCleanupWorker worker)
    {
        // Use reflection to access the private _cleanedCount field
        var field = typeof(LockCleanupWorker).GetField("_cleanedCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field?.GetValue(worker) as int? ?? 0;
    }

    /// <summary>
    /// Configures the worker to run cleanup more frequently for testing purposes.
    /// </summary>
    /// <param name="worker">The lock cleanup worker instance</param>
    /// <param name="interval">Cleanup interval to set</param>
    /// <returns>The same worker instance for method chaining</returns>
    public static LockCleanupWorker WithTestInterval(this LockCleanupWorker worker, TimeSpan interval)
    {
        var optionsField = typeof(LockCleanupWorker).GetField("_options", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (optionsField?.GetValue(worker) is LockCleanupWorkerOptions options)
        {
            options.CleanupIntervalMs = (int)interval.TotalMilliseconds;
        }

        return worker;
    }

    /// <summary>
    /// Logs the current configuration of the worker.
    /// </summary>
    /// <param name="worker">The lock cleanup worker instance</param>
    /// <param name="logger">Logger instance</param>
    public static void LogConfiguration(this LockCleanupWorker worker, ILogger logger)
    {
        var optionsField = typeof(LockCleanupWorker).GetField("_options", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (optionsField?.GetValue(worker) is LockCleanupWorkerOptions options)
        {
            logger.LogInformation("LockCleanupWorker Configuration:");
            logger.LogInformation("- InitialDelayMs: {InitialDelayMs}ms", options.InitialDelayMs);
            logger.LogInformation("- CleanupIntervalMs: {CleanupIntervalMs}ms ({CleanupIntervalMinutes} minutes)",
                options.CleanupIntervalMs, TimeSpan.FromMilliseconds(options.CleanupIntervalMs).TotalMinutes);
            logger.LogInformation("- BatchSize: {BatchSize}", options.BatchSize);
            logger.LogInformation("- VerboseLogging: {VerboseLogging}", options.VerboseLogging);
            logger.LogInformation("- MinimumExpiredDuration: {MinimumExpiredDuration}", options.MinimumExpiredDuration);
        }
    }
}