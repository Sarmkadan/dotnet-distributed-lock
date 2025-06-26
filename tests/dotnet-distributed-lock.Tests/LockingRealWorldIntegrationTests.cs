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
/// Real-world integration tests demonstrating practical use cases from the README.
/// </summary>
public class LockingRealWorldIntegrationTests
{
    private readonly InMemoryLockRepository _repository;
    private readonly LockService _lockService;
    private readonly FencingTokenService _fencingService;
    private readonly DeadlockDetector _deadlockDetector;

    public LockingRealWorldIntegrationTests()
    {
        _repository = new InMemoryLockRepository();
        _lockService = new LockService(_repository, NullLogger<LockService>.Instance);
        _fencingService = new FencingTokenService(NullLogger<FencingTokenService>.Instance);
        _deadlockDetector = new DeadlockDetector(NullLogger<DeadlockDetector>.Instance);
    }

    // -------------------------------------------------------------------------
    // Database Migration Lock Scenario
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DatabaseMigrationLock_PreventsConcurrentMigrations()
    {
        // Scenario: Multiple application instances trying to run database migrations simultaneously.
        // Only one should proceed, others should wait or skip.

        const string migrationLockKey = "db-migration:main";
        var instance1 = "app-instance-1";
        var instance2 = "app-instance-2";
        var migrationDuration = TimeSpan.FromMinutes(5);

        // Act 1: Instance 1 tries to acquire migration lock
        var (instance1Acquired, lock1, _) = await _lockService.TryAcquireAsync(
            migrationLockKey, instance1, migrationDuration);

        // Act 2: Instance 2 tries to acquire same lock
        var (instance2Acquired, lock2, _) = await _lockService.TryAcquireAsync(
            migrationLockKey, instance2, migrationDuration);

        // Assert: Only one instance should acquire the lock
        instance1Acquired.Should().BeTrue();
        instance2Acquired.Should().BeFalse();

        // Act 3: Instance 1 completes migration and releases lock
        var released = await _lockService.ReleaseAsync(migrationLockKey, instance1);
        released.Should().BeTrue();

        // Act 4: Now instance 2 can acquire the lock
        var (instance2Acquired2, _, _) = await _lockService.TryAcquireAsync(
            migrationLockKey, instance2, migrationDuration);
        instance2Acquired2.Should().BeTrue();

        // Cleanup
        await _lockService.ReleaseAsync(migrationLockKey, instance2);
    }

    // -------------------------------------------------------------------------
    // Report Generation with Auto-Renewal Scenario
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ReportGenerationLock_MaintainsLockDuringLongOperation()
    {
        // Scenario: Generate a large report while maintaining the lock.
        // Lock should not expire while the operation is in progress.

        const string reportLockKey = "report-generation:monthly";
        const string workerId = "worker-1";
        var lockDuration = TimeSpan.FromSeconds(10);
        var reportDuration = TimeSpan.FromSeconds(3);

        // Act 1: Acquire lock for report generation
        var (acquired, @lock, _) = await _lockService.TryAcquireAsync(
            reportLockKey, workerId, lockDuration);

        acquired.Should().BeTrue();
        var initialExpiresAt = @lock!.ExpiresAt;

        // Act 2: Simulate long-running operation with renewal
        await Task.Delay(reportDuration);

        // Act 3: Renew lock to extend expiration
        var renewed = await _lockService.RenewAsync(reportLockKey, workerId, lockDuration);
        renewed.Should().BeTrue();

        var renewedLock = await _repository.GetByKeyAsync(reportLockKey);
        var renewedExpiresAt = renewedLock!.ExpiresAt;

        // Assert: Lock expiration should be extended
        renewedExpiresAt.Should().BeAfter(initialExpiresAt);

        // Cleanup
        await _lockService.ReleaseAsync(reportLockKey, workerId);
    }

    // -------------------------------------------------------------------------
    // Job Queue Processing with Fencing Tokens
    // -------------------------------------------------------------------------

    [Fact]
    public async Task JobProcessingWithFencingTokens_PreventsStaleWrites()
    {
        // Scenario: Process a job exclusively and prevent zombie writes if lock expires.
        // Use fencing tokens to ensure a stale worker can't write to shared resource.

        const string jobLockKey = "job-processing:queue-1";
        const string processorId = "processor-1";
        var lockDuration = TimeSpan.FromSeconds(5);

        // Act 1: Acquire lock and process job
        var (acquired, _, _) = await _lockService.TryAcquireAsync(
            jobLockKey, processorId, lockDuration);
        acquired.Should().BeTrue();

        // Act 2: Issue fencing token at the start of processing
        var token = _fencingService.IssueToken(jobLockKey);
        token.Should().NotBeNull();

        // Act 3: Validate token before writing (prevents zombie writes)
        var isValidAtStart = _fencingService.ValidateToken(jobLockKey, token);
        isValidAtStart.Should().BeTrue();

        // Act 4: Simulate lock expiration by incrementing token
        var newToken = _fencingService.IncrementToken(jobLockKey);

        // Act 5: Try to validate original token (should fail)
        var isValidAfterExpiry = _fencingService.ValidateToken(jobLockKey, token);

        // Assert: Stale token should be rejected
        isValidAfterExpiry.Should().BeFalse();
        token.SequenceNumber.Should().BeLessThan(newToken.SequenceNumber);

        // Cleanup
        await _lockService.ReleaseAsync(jobLockKey, processorId);
    }

    // -------------------------------------------------------------------------
    // Multi-Resource Coordination Scenario
    // -------------------------------------------------------------------------

    [Fact]
    public async Task MultiResourceCoordination_AcquireAndReleaseMultipleLocks()
    {
        // Scenario: Coordinate operations across multiple resources.
        // Need exclusive access to multiple resources simultaneously.

        var resources = new[] { "resource:a", "resource:b", "resource:c" };
        var instanceId = Guid.NewGuid().ToString();
        var duration = TimeSpan.FromSeconds(30);
        var acquiredLocks = new List<string>();

        // Act 1: Acquire locks in consistent order to prevent deadlock
        Array.Sort(resources);
        foreach (var resource in resources)
        {
            var (acquired, _, _) = await _lockService.TryAcquireAsync(resource, instanceId, duration);
            if (acquired)
            {
                acquiredLocks.Add(resource);
            }
        }

        // Assert: Should have acquired all locks
        acquiredLocks.Should().HaveCount(3);

        // Act 2: Perform coordinated operation (all locks held)
        var allLocked = await Task.WhenAll(resources.Select(r => _lockService.IsLockedAsync(r)));
        allLocked.Should().OnlyContain(isLocked => isLocked);

        // Act 3: Release all locks
        foreach (var resource in resources)
        {
            var released = await _lockService.ReleaseAsync(resource, instanceId);
            released.Should().BeTrue();
        }

        // Act 4: Verify all locks are released
        var anyLocked = await Task.WhenAll(resources.Select(r => _lockService.IsLockedAsync(r)));
        anyLocked.Should().OnlyContain(isLocked => !isLocked);
    }

    // -------------------------------------------------------------------------
    // Scheduled Task Execution Scenario
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ScheduledTaskExecution_OnlyOneInstanceRunsTask()
    {
        // Scenario: Scheduled task should run on only one instance in a distributed system.
        // Other instances should skip if lock is held.

        const string taskName = "daily-cleanup";
        const string lockKey = "task:" + taskName;
        var instances = new[] { "server-1", "server-2", "server-3" };
        var duration = TimeSpan.FromMinutes(5);
        var executedOn = new System.Collections.Concurrent.ConcurrentBag<string>();

        // Act: All instances try to acquire the task lock
        var tasks = instances.Select(async instance =>
        {
            var (acquired, _, _) = await _lockService.TryAcquireAsync(lockKey, instance, duration);
            if (acquired)
            {
                executedOn.Add(instance);
                // Simulate task execution
                await Task.Delay(100);
                // Release lock
                await _lockService.ReleaseAsync(lockKey, instance);
            }
        });

        await Task.WhenAll(tasks);

        // Assert: Only one instance should execute the task
        executedOn.Should().HaveCount(1);
    }

    // -------------------------------------------------------------------------
    // Deadlock Detection Scenario
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeadlockDetection_DetectsCircularWaits()
    {
        // Scenario: Detect when multiple workers are waiting for locks
        // in a way that creates a circular dependency (deadlock).

        // Setup: Worker A holds lock 1, Worker B holds lock 2
        _deadlockDetector.RecordAcquired("worker-a", "lock:1");
        _deadlockDetector.RecordAcquired("worker-b", "lock:2");

        // Worker A waits for lock 2
        await _deadlockDetector.RecordWaitingAsync("worker-a", "lock:2");

        // Act: Worker B tries to wait for lock 1 (would complete a cycle)
        var wouldDeadlock = _deadlockDetector.WouldDeadlock("worker-b", "lock:1");

        // Assert: Deadlock should be detected
        wouldDeadlock.Should().BeTrue();

        // Act 2: Check metrics
        var metrics = _deadlockDetector.GetMetrics("lock:1");
        metrics.Should().NotBeNull();
        metrics!.DeadlocksDetected.Should().BeGreaterThan(0);
    }

    // -------------------------------------------------------------------------
    // Contention Analysis Scenario
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ContentionAnalysis_TrackLockContention()
    {
        // Scenario: Monitor lock contention to identify bottlenecks.
        // Track which locks have high contention.

        const string contentionLockKey = "resource:critical-section";
        var workerCount = 20;
        var duration = TimeSpan.FromSeconds(5);

        // Simulate contention by having many workers try to acquire the same lock
        var tasks = Enumerable.Range(0, workerCount)
            .Select(i => Task.Run(async () =>
            {
                var workerId = $"worker-{i}";
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                await _deadlockDetector.RecordWaitingAsync(workerId, contentionLockKey);

                var (acquired, _, _) = await _lockService.TryAcquireAsync(
                    contentionLockKey, workerId, duration);

                stopwatch.Stop();
                await _deadlockDetector.RecordWaitEndedAsync(workerId, contentionLockKey, stopwatch.Elapsed.TotalMilliseconds);

                if (acquired)
                {
                    _deadlockDetector.RecordAcquired(workerId, contentionLockKey);
                    await Task.Delay(100); // Hold lock briefly
                    await _lockService.ReleaseAsync(contentionLockKey, workerId);
                    _deadlockDetector.RecordReleased(workerId, contentionLockKey);
                }
            }))
            .ToList();

        await Task.WhenAll(tasks);

        // Assert: Verify contention metrics were collected
        var contentionMetrics = _deadlockDetector.GetMetrics(contentionLockKey);
        contentionMetrics.Should().NotBeNull();
        contentionMetrics!.TotalWaiters.Should().BeGreaterThan(0);
        contentionMetrics.AverageWaitTimeMs.Should().BeGreaterThan(0);
    }

    // -------------------------------------------------------------------------
    // Lock Lifecycle Scenario
    // -------------------------------------------------------------------------

    [Fact]
    public async Task LockLifecycle_CompleteWorkflow()
    {
        // Scenario: Complete lifecycle of a lock: acquire -> use -> renew -> release

        const string lockKey = "resource:lifecycle-test";
        const string ownerId = "worker-1";
        var initialDuration = TimeSpan.FromSeconds(5);
        var renewalDuration = TimeSpan.FromSeconds(10);

        // State 1: Acquire lock
        var (acquired, @lock, _) = await _lockService.TryAcquireAsync(lockKey, ownerId, initialDuration);
        acquired.Should().BeTrue();
        @lock!.Status.Should().Be(LockStatus.Held);

        var acquiredAt = @lock.AcquiredAt;
        var initialExpiresAt = @lock.ExpiresAt;

        // State 2: Lock is held
        var isLocked = await _lockService.IsLockedAsync(lockKey);
        isLocked.Should().BeTrue();

        // State 3: Renew lock before expiration
        var renewed = await _lockService.RenewAsync(lockKey, ownerId, renewalDuration);
        renewed.Should().BeTrue();

        var renewedLock = await _repository.GetByKeyAsync(lockKey);
        var renewedExpiresAt = renewedLock!.ExpiresAt;
        renewedLock.RenewalCount.Should().Be(1);

        // State 4: Check that expiration was extended
        renewedExpiresAt.Should().BeAfter(initialExpiresAt);

        // State 5: Release lock
        var released = await _lockService.ReleaseAsync(lockKey, ownerId);
        released.Should().BeTrue();

        // State 6: Lock is no longer held
        isLocked = await _lockService.IsLockedAsync(lockKey);
        isLocked.Should().BeFalse();

        // State 7: Can acquire again after release
        var (reacquired, _, _) = await _lockService.TryAcquireAsync(lockKey, "worker-2", initialDuration);
        reacquired.Should().BeTrue();

        // Cleanup
        await _lockService.ReleaseAsync(lockKey, "worker-2");
    }

    // -------------------------------------------------------------------------
    // High Throughput Scenario
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HighThroughput_RapidAcquisitionAndRelease()
    {
        // Scenario: System handles rapid lock acquisitions and releases
        // without deadlocks or resource leaks.

        const string lockKey = "resource:high-throughput";
        var operationCount = 100;
        var duration = TimeSpan.FromSeconds(1);
        var successCount = 0;

        // Act: Rapid acquire/release cycle
        for (int i = 0; i < operationCount; i++)
        {
            var workerId = $"worker-{i % 10}";
            var (acquired, @lock, _) = await _lockService.TryAcquireAsync(lockKey, workerId, duration);

            if (acquired && @lock != null)
            {
                successCount++;
                // Simulate brief work
                await Task.Delay(5);
                await _lockService.ReleaseAsync(lockKey, workerId);
            }
        }

        // Assert: Most operations should succeed
        successCount.Should().BeGreaterThan(operationCount / 2);

        // Verify no locks are left held
        var isLocked = await _lockService.IsLockedAsync(lockKey);
        isLocked.Should().BeFalse();
    }
}
