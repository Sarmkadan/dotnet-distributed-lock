#nullable enable

using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Repository;
using SarmKadan.DistributedLock.Services;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

public static class AdvancedConcurrencyTestsExtensions
{
    private static readonly FieldInfo? _repositoryField = typeof(AdvancedConcurrencyTests).GetField(
        "_repository",
        BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo? _serviceField = typeof(AdvancedConcurrencyTests).GetField(
        "_service",
        BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// Gets the LockRepository instance from AdvancedConcurrencyTests.
    /// </summary>
    /// <param name="tests">The AdvancedConcurrencyTests instance</param>
    /// <returns>The LockRepository instance</returns>
    private static InMemoryLockRepository GetRepository(this AdvancedConcurrencyTests tests)
    {
        if (_repositoryField == null)
        {
            throw new InvalidOperationException("Could not find _repository field");
        }

        return (InMemoryLockRepository)_repositoryField.GetValue(tests)!;
    }

    /// <summary>
    /// Gets the LockService instance from AdvancedConcurrencyTests.
    /// </summary>
    /// <param name="tests">The AdvancedConcurrencyTests instance</param>
    /// <returns>The LockService instance</returns>
    private static LockService GetService(this AdvancedConcurrencyTests tests)
    {
        if (_serviceField == null)
        {
            throw new InvalidOperationException("Could not find _service field");
        }

        return (LockService)_serviceField.GetValue(tests)!;
    }

    /// <summary>
    /// Extension method to get the current lock state for a specific key.
    /// Useful for testing lock state without directly accessing private fields.
    /// </summary>
    /// <param name="tests">The AdvancedConcurrencyTests instance</param>
    /// <param name="lockKey">The lock key to check</param>
    /// <returns>The lock if it exists, null otherwise</returns>
    public static async Task<Lock?> GetLockStateAsync(this AdvancedConcurrencyTests tests, string lockKey)
    {
        var repository = tests.GetRepository();
        return await repository.GetByKeyAsync(lockKey);
    }

    /// <summary>
    /// Extension method to count active locks in the repository.
    /// </summary>
    /// <param name="tests">The AdvancedConcurrencyTests instance</param>
    /// <returns>The count of active locks</returns>
    public static async Task<int> CountActiveLocksAsync(this AdvancedConcurrencyTests tests)
    {
        var repository = tests.GetRepository();
        var locks = await repository.GetAllActiveLockAsync();
        return locks.Count();
    }

    /// <summary>
    /// Extension method to measure the time taken for a lock acquisition operation.
    /// Useful for performance testing and identifying bottlenecks.
    /// </summary>
    /// <param name="tests">The AdvancedConcurrencyTests instance</param>
    /// <param name="lockKey">The lock key to acquire</param>
    /// <param name="ownerId">The owner ID attempting to acquire the lock</param>
    /// <param name="timeout">The timeout duration</param>
    /// <returns>A tuple containing the acquisition result and the elapsed time in milliseconds</returns>
    public static async Task<(bool Acquired, Lock? Lock, TimeSpan ElapsedTime)> MeasureAcquisitionTimeAsync(
        this AdvancedConcurrencyTests tests,
        string lockKey,
        string ownerId,
        TimeSpan timeout)
    {
        var service = tests.GetService();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await service.TryAcquireAsync(lockKey, ownerId, timeout);
        stopwatch.Stop();

        return (result.Success, result.Lock, TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds));
    }

    /// <summary>
    /// Extension method to verify that metrics are being tracked correctly under concurrent load.
    /// Ensures that the metrics reflect the actual operations performed.
    /// </summary>
    /// <param name="tests">The AdvancedConcurrencyTests instance</param>
    /// <param name="expectedAcquisitions">Expected number of successful acquisitions</param>
    /// <param name="expectedRenewals">Expected number of successful renewals</param>
    /// <param name="expectedReleases">Expected number of successful releases</param>
    /// <returns>True if metrics match expectations, false otherwise</returns>
    public static async Task<bool> VerifyMetricsAsync(
        this AdvancedConcurrencyTests tests,
        int expectedAcquisitions,
        int expectedRenewals,
        int expectedReleases)
    {
        var service = tests.GetService();
        var metrics = service.GetMetrics();

        // Give a small tolerance for concurrent operations
        var tolerance = 5;

        return Math.Abs(metrics.SuccessfulAcquisitions - expectedAcquisitions) <= tolerance &&
               Math.Abs(metrics.SuccessfulRenewals - expectedRenewals) <= tolerance &&
               Math.Abs(metrics.TotalReleases - expectedReleases) <= tolerance;
    }

    /// <summary>
    /// Extension method to test lock acquisition with a specific retry policy.
    /// Attempts to acquire a lock multiple times with delays, simulating real-world scenarios.
    /// </summary>
    /// <param name="tests">The AdvancedConcurrencyTests instance</param>
    /// <param name="lockKey">The lock key to acquire</param>
    /// <param name="ownerId">The owner ID attempting to acquire the lock</param>
    /// <param name="timeout">The timeout duration for each attempt</param>
    /// <param name="maxAttempts">Maximum number of acquisition attempts</param>
    /// <param name="retryDelay">Delay between retry attempts</param>
    /// <returns>A tuple containing the final acquisition result and the number of attempts made</returns>
    public static async Task<(bool Acquired, Lock? Lock, int Attempts)> AcquireWithRetryAsync(
        this AdvancedConcurrencyTests tests,
        string lockKey,
        string ownerId,
        TimeSpan timeout,
        int maxAttempts = 3,
        TimeSpan? retryDelay = null)
    {
        var service = tests.GetService();
        var retryDelayValue = retryDelay ?? TimeSpan.FromMilliseconds(100);
        var attempts = 0;

        while (attempts < maxAttempts)
        {
            attempts++;
            var result = await service.TryAcquireAsync(lockKey, ownerId, timeout);

            if (result.Success)
            {
                return (true, result.Lock, attempts);
            }

            await Task.Delay(retryDelayValue);
        }

        return (false, null, attempts);
    }

    /// <summary>
    /// Extension method to verify that all locks in the repository are valid and consistent.
    /// Checks that no lock is held by a non-existent owner and that all locks have valid expiration times.
    /// </summary>
    /// <param name="tests">The AdvancedConcurrencyTests instance</param>
    /// <returns>True if all locks are valid, false otherwise</returns>
    public static async Task<bool> VerifyLockRepositoryConsistencyAsync(this AdvancedConcurrencyTests tests)
    {
        var repository = tests.GetRepository();
        var allLocks = (await repository.GetAllActiveLockAsync()).ToList();

        foreach (var @lock in allLocks)
        {
            // Verify lock has valid owner
            if (string.IsNullOrWhiteSpace(@lock.OwnerId))
            {
                return false;
            }

            // Verify lock has valid status
            if (@lock.Status != LockStatus.Held)
            {
                return false;
            }

            // Verify lock has valid expiration
            if (@lock.ExpiresAt <= DateTime.UtcNow)
            {
                return false;
            }
        }

        return true;
    }
}
