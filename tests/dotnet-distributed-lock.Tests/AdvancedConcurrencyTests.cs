#nullable enable
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Repository;
using SarmKadan.DistributedLock.Services;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

public class AdvancedConcurrencyTests
{
    private readonly InMemoryLockRepository _repository;
    private readonly LockService _service;

    public AdvancedConcurrencyTests()
    {
        _repository = new InMemoryLockRepository();
        _service = new LockService(_repository, NullLogger<LockService>.Instance);
    }

    // -------------------------------------------------------------------------
    // High Contention Scenarios
    // -------------------------------------------------------------------------

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
