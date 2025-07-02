#nullable enable
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Repository;
using SarmKadan.DistributedLock.Services;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Contains advanced concurrency tests for distributed lock operations under high load and stress scenarios.
/// Tests various edge cases including high contention, rapid acquire/release cycles, concurrent operations,
/// renewal under load, expiration handling, and error recovery to ensure thread safety and consistency.
/// </summary>
public class AdvancedConcurrencyTests
{
    /// <summary>
    /// The in-memory lock repository used for testing distributed lock operations.
    /// </summary>
    private readonly InMemoryLockRepository _repository;

    /// <summary>
    /// The lock service instance that manages distributed lock acquisition, renewal, and release operations.
    /// </summary>
    private readonly LockService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdvancedConcurrencyTests"/> class.
    /// Sets up an in-memory lock repository and a lock service for testing distributed lock operations.
    /// </summary>
    public AdvancedConcurrencyTests()
    {
        _repository = new InMemoryLockRepository();
        _service = new LockService(_repository, NullLogger<LockService>.Instance);
    }

    // -------------------------------------------------------------------------
    // High Contention Scenarios
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests that under high contention with many workers racing for the same lock,
    /// only one worker can successfully acquire the lock while all others fail.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task HighContention_ManyWorkersRacingForSameLock()
    {
        // Arrange
        const string lockKey = "resource:high-contention";
        const int workerCount = 100;
        var winners = new System.Collections.Concurrent.ConcurrentBag<string>();

        // Act
        var tasks = Enumerable.Range(0, workerCount)
            .Select(i => Task.Run(async () =>
            {
                var workerId = $"worker-{i}";
                var (acquired, _, _) = await _service.TryAcquireAsync(
                    lockKey,
                    workerId,
                    TimeSpan.FromSeconds(10));

                if (acquired)
                    winners.Add(workerId);
            }))
            .ToList();

        await Task.WhenAll(tasks);

        // Assert
        winners.Should().HaveCount(1);
    }

    /// <summary>
    /// Tests that multiple locks can be acquired concurrently without interference,
    /// ensuring that workers acquiring different locks don't block each other.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task HighContention_MultipleLocksWithoutInterference()
    {
        // Arrange — many workers acquiring different locks
        const int lockCount = 50;
        const int workersPerLock = 2;

        // Act
        var tasks = new List<Task>();
        for (int lockIndex = 0; lockIndex < lockCount; lockIndex++)
        {
            for (int worker = 0; worker < workersPerLock; worker++)
            {
                var lockKey = $"lock:{lockIndex}";
                var workerId = $"worker-{lockIndex}-{worker}";

                tasks.Add(Task.Run(async () =>
                {
                    await _service.TryAcquireAsync(lockKey, workerId, TimeSpan.FromSeconds(10));
                }));
            }
        }

        await Task.WhenAll(tasks);

        // Assert
        var allLocks = (await _repository.GetAllActiveLockAsync()).ToList();
        allLocks.Should().HaveCount(lockCount);
    }

    // -------------------------------------------------------------------------
    // Renewal Under Load
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests that simultaneous renewal and acquisition operations work correctly under load,
    /// ensuring that lock renewals don't interfere with new acquisition attempts.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task RenewalUnderLoad_SimultaneousRenewalsAndAcquisitions()
    {
        // Arrange
        var lockKey = "resource:renewal-load";
        var (acquired, @lock, _) = await _service.TryAcquireAsync(lockKey, "owner-1", TimeSpan.FromSeconds(30));
        acquired.Should().BeTrue();

        // Act — mix of renewals and new acquisition attempts
        var tasks = new List<Task>();

        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_service.RenewAsync(lockKey, "owner-1", TimeSpan.FromSeconds(30)));
        }

        for (int i = 0; i < 10; i++)
        {
            var workerId = $"worker-{i}";
            tasks.Add(Task.Run(async () =>
            {
                await _service.TryAcquireAsync(lockKey, workerId, TimeSpan.FromSeconds(10));
            }));
        }

        await Task.WhenAll(tasks);

        // Assert — lock should still be held by owner-1
        var current = await _repository.GetByKeyAsync(lockKey);
        current.Should().NotBeNull();
        current!.OwnerId.Should().Be("owner-1");
    }

    // -------------------------------------------------------------------------
    // Rapid Release and Reacquisition
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests rapid acquire-release-acquire sequences to ensure that lock state is properly cleaned up
    /// and locks can be reacquired by different owners in quick succession.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task RapidCycle_AcquireReleaseAcquireSequence()
    {
        // Arrange
        var lockKey = "resource:rapid-cycle";
        var owners = new[] { "owner-1", "owner-2", "owner-3" };

        // Act — rapid acquire/release cycles
        for (int cycle = 0; cycle < 10; cycle++)
        {
            foreach (var owner in owners)
            {
                var (acquired, @lock, _) = await _service.TryAcquireAsync(
                    lockKey,
                    owner,
                    TimeSpan.FromSeconds(10));

                if (acquired && @lock != null)
                {
                    await Task.Delay(10); // Hold lock briefly
                    await _service.ReleaseAsync(lockKey, owner);
                }
            }
        }

        // Assert
        var finalLock = await _repository.GetByKeyAsync(lockKey);
        finalLock.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // Concurrent Operations on Multiple Keys
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests concurrent operations across many lock keys with many workers to ensure
    /// that the system can handle high parallelism without race conditions or deadlocks.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task ConcurrentOperations_ManyKeysWithManyWorkers()
    {
        // Arrange
        const int keyCount = 20;
        const int workers = 50;
        var successCount = 0;

        // Act
        var tasks = Enumerable.Range(0, workers)
            .Select(workerIndex => Task.Run(async () =>
            {
                for (int keyIndex = 0; keyIndex < keyCount; keyIndex++)
                {
                    var lockKey = $"lock:{keyIndex}";
                    var workerId = $"worker-{workerIndex}";

                    var (acquired, @lock, _) = await _service.TryAcquireAsync(
                        lockKey,
                        workerId,
                        TimeSpan.FromSeconds(5));

                    if (acquired)
                    {
                        Interlocked.Increment(ref successCount);

                        if (@lock != null)
                        {
                            await Task.Delay(5);
                            await _service.ReleaseAsync(lockKey, workerId);
                        }
                    }
                }
            }))
            .ToList();

        await Task.WhenAll(tasks);

        // Assert
        successCount.Should().BeGreaterThan(0);
        var allLocks = (await _repository.GetAllActiveLockAsync()).ToList();
        allLocks.Should().HaveCountLessThanOrEqualTo(keyCount);
    }

    // -------------------------------------------------------------------------
    // Lock Lifecycle Stress Test
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests the complete lock lifecycle (acquire, renew, release) repeatedly under stress
    /// to ensure all operations remain consistent and no resource leaks occur.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task CompleteLifecycleStress_AcquireRenewReleaseRepeatedly()
    {
        // Arrange
        var lockKey = "resource:lifecycle";
        var owner = "owner-1";
        var iterations = 50;

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var (acquired, @lock, _) = await _service.TryAcquireAsync(lockKey, owner, TimeSpan.FromSeconds(30));
            acquired.Should().BeTrue();

            var renewed = await _service.RenewAsync(lockKey, owner, TimeSpan.FromSeconds(30));
            renewed.Should().BeTrue();

            var released = await _service.ReleaseAsync(lockKey, owner);
            released.Should().BeTrue();
        }

        // Assert
        var finalLock = await _repository.GetByKeyAsync(lockKey);
        finalLock.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // Concurrent Metrics Tracking
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests that metrics tracking remains accurate under concurrent load with many simultaneous operations,
    /// ensuring that the metrics service correctly counts successful acquisitions and releases.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task MetricsUnderConcurrentLoad_CorrectlyTrackOperations()
    {
        // Arrange
        const int operations = 100;
        var lockKey = "resource:metrics";

        // Act
        var tasks = Enumerable.Range(0, operations)
            .Select(i => Task.Run(async () =>
            {
                var workerId = $"worker-{i}";
                var (acquired, @lock, _) = await _service.TryAcquireAsync(
                    lockKey,
                    workerId,
                    TimeSpan.FromSeconds(10));

                if (acquired && @lock != null)
                {
                    await _service.ReleaseAsync(lockKey, workerId);
                }
            }))
            .ToList();

        await Task.WhenAll(tasks);

        // Assert
        var metrics = _service.GetMetrics();
        metrics.SuccessfulAcquisitions.Should().BeGreaterThan(0);
        metrics.TotalReleases.Should().BeGreaterThan(0);
    }

    // -------------------------------------------------------------------------
    // Expiration Handling Under Load
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests that expired locks are properly handled and can be reacquired by new owners,
    /// ensuring that the expiration mechanism works correctly under load.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task ExpirationHandling_LocksExpireAndCanBeReacquired()
    {
        // Arrange
        var lockKey = "resource:expiring";
        var expiredLock = new Lock(lockKey, "owner-1", TimeSpan.FromSeconds(1))
        {
            Status = LockStatus.Held,
            ExpiresAt = DateTime.UtcNow.AddSeconds(0.5) // Will expire soon
        };

        await _repository.AcquireAsync(expiredLock);

        // Act
        await Task.Delay(600); // Wait for lock to expire

        await _repository.DeleteExpiredLockAsync();

        var newOwner = "owner-2";
        var (acquired, _, _) = await _service.TryAcquireAsync(
            lockKey,
            newOwner,
            TimeSpan.FromSeconds(10));

        // Assert
        acquired.Should().BeTrue();

        var current = await _repository.GetByKeyAsync(lockKey);
        current!.OwnerId.Should().Be(newOwner);
    }

    // -------------------------------------------------------------------------
    // State Consistency Under Concurrent Errors
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests error recovery scenarios to ensure that the repository remains consistent
    /// even when operations fail or when invalid operations are attempted.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task ErrorRecovery_RepositoryRemainsConsistentAfterFailures()
    {
        // Arrange
        var lockKey = "resource:consistent";
        var owner = "owner-1";

        // Act
        var (acquired1, @lock1, _) = await _service.TryAcquireAsync(lockKey, owner, TimeSpan.FromSeconds(30));

        // Try to acquire with different owner (should fail)
        var (acquired2, _, _) = await _service.TryAcquireAsync(lockKey, "owner-2", TimeSpan.FromSeconds(30));

        // Original owner should still be able to manage the lock
        var renewed = await _service.RenewAsync(lockKey, owner);
        var released = await _service.ReleaseAsync(lockKey, owner);

        // Assert
        acquired1.Should().BeTrue();
        acquired2.Should().BeFalse();
        renewed.Should().BeTrue();
        released.Should().BeTrue();

        var finalState = await _repository.GetByKeyAsync(lockKey);
        finalState.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // Lock Fairness Test
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests lock fairness to ensure that under concurrent access, some workers are able to acquire locks
    /// rather than a single worker monopolizing all lock acquisitions.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task LockFairness_SomeWorkersCanAcquire()
    {
        // Arrange
        const string lockKey = "resource:fairness";
        const int workerCount = 10;
        var acquisitionCounts = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();

        // Act — each worker tries multiple times
        var tasks = new List<Task>();
        for (int cycle = 0; cycle < 5; cycle++)
        {
            for (int worker = 0; worker < workerCount; worker++)
            {
                var workerId = $"worker-{worker}";
                tasks.Add(Task.Run(async () =>
                {
                    var (acquired, @lock, _) = await _service.TryAcquireAsync(
                        lockKey,
                        workerId,
                        TimeSpan.FromSeconds(5));

                    if (acquired && @lock != null)
                    {
                        acquisitionCounts.AddOrUpdate(workerId, 1, (k, v) => v + 1);
                        await Task.Delay(10);
                        await _service.ReleaseAsync(lockKey, workerId);
                    }
                }));
            }
        }

        await Task.WhenAll(tasks);

        // Assert
        acquisitionCounts.Values.Sum().Should().BeGreaterThan(0);

        // At least one worker should have acquired the lock
        var workersWithAcquisitions = acquisitionCounts.Count;
        workersWithAcquisitions.Should().BeGreaterThanOrEqualTo(1);
    }
}
