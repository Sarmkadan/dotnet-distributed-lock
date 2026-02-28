#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Repository;
using SarmKadan.DistributedLock.Services;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Integration tests covering end-to-end lock acquisition, renewal, and release workflows.
/// </summary>
public class LockingIntegrationTests
{
    private readonly InMemoryLockRepository _repository;
    private readonly LockService _lockService;
    private readonly FencingTokenService _fencingService;

    public LockingIntegrationTests()
    {
        _repository = new InMemoryLockRepository();
        _lockService = new LockService(_repository, NullLogger<LockService>.Instance);
        _fencingService = new FencingTokenService(NullLogger<FencingTokenService>.Instance);
    }

    // -------------------------------------------------------------------------
    // Basic Workflow: Acquire → Renew → Release
    // -------------------------------------------------------------------------

    [Fact]
    public async Task BasicWorkflow_AcquireRenewRelease_Succeeds()
    {
        // Arrange
        const string lockKey = "resource:database";
        const string ownerId = "worker-1";
        var duration = TimeSpan.FromSeconds(30);

        // Act 1: Acquire the lock
        var (acquired, @lock, acquireError) = await _lockService.TryAcquireAsync(lockKey, ownerId, duration);

        // Assert 1
        acquired.Should().BeTrue();
        @lock.Should().NotBeNull();
        @lock!.Key.Should().Be(lockKey);
        @lock.OwnerId.Should().Be(ownerId);
        @lock.Status.Should().Be(LockStatus.Held);

        // Act 2: Renew the lock
        var renewed = await _lockService.RenewAsync(lockKey, ownerId, duration);

        // Assert 2
        renewed.Should().BeTrue();

        // Act 3: Release the lock
        var released = await _lockService.ReleaseAsync(lockKey, ownerId);

        // Assert 3
        released.Should().BeTrue();

        // Assert 4: Lock is no longer held
        var isLocked = await _lockService.IsLockedAsync(lockKey);
        isLocked.Should().BeFalse();
    }

    [Fact]
    public async Task BasicWorkflow_CannotAcquireTwice()
    {
        // Arrange
        const string lockKey = "resource:payments";
        const string owner1 = "worker-1";
        const string owner2 = "worker-2";
        var duration = TimeSpan.FromSeconds(30);

        // Act 1: First owner acquires
        var (acquired1, _, _) = await _lockService.TryAcquireAsync(lockKey, owner1, duration);

        // Act 2: Second owner tries to acquire
        var (acquired2, _, _) = await _lockService.TryAcquireAsync(lockKey, owner2, duration);

        // Assert
        acquired1.Should().BeTrue();
        acquired2.Should().BeFalse();
    }

    [Fact]
    public async Task BasicWorkflow_DifferentKeysAreIndependent()
    {
        // Arrange
        const string owner = "worker-1";
        var duration = TimeSpan.FromSeconds(30);

        // Act
        var (acq1, _, _) = await _lockService.TryAcquireAsync("lock:1", owner, duration);
        var (acq2, _, _) = await _lockService.TryAcquireAsync("lock:2", owner, duration);
        var (acq3, _, _) = await _lockService.TryAcquireAsync("lock:3", owner, duration);

        // Assert
        acq1.Should().BeTrue();
        acq2.Should().BeTrue();
        acq3.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Renewal Workflow
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RenewalWorkflow_MultipleRenewalsExtendExpiration()
    {
        // Arrange
        const string lockKey = "resource:orders";
        const string ownerId = "worker-1";
        var initialDuration = TimeSpan.FromSeconds(10);
        var renewalDuration = TimeSpan.FromSeconds(20);

        // Act 1: Acquire
        var (acquired, @lock, _) = await _lockService.TryAcquireAsync(lockKey, ownerId, initialDuration);
        var firstExpiresAt = @lock!.ExpiresAt;

        // Act 2: Renew
        await _lockService.RenewAsync(lockKey, ownerId, renewalDuration);
        var renewedLock = await _repository.GetByKeyAsync(lockKey);
        var secondExpiresAt = renewedLock!.ExpiresAt;

        // Act 3: Renew again
        await _lockService.RenewAsync(lockKey, ownerId, renewalDuration);
        var thirdLock = await _repository.GetByKeyAsync(lockKey);
        var thirdExpiresAt = thirdLock!.ExpiresAt;

        // Assert
        secondExpiresAt.Should().BeAfter(firstExpiresAt);
        thirdExpiresAt.Should().BeAfter(secondExpiresAt);
    }

    [Fact]
    public async Task RenewalWorkflow_CannotRenewExpiredLock()
    {
        // Arrange
        const string lockKey = "resource:sessions";
        const string ownerId = "worker-1";
        var @lock = new Lock(lockKey, ownerId, TimeSpan.FromSeconds(5))
        {
            Status = LockStatus.Held,
            ExpiresAt = DateTime.UtcNow.AddSeconds(-1) // Already expired
        };
        await _repository.AcquireAsync(@lock);

        // Act
        var renewed = await _lockService.RenewAsync(lockKey, ownerId);

        // Assert
        renewed.Should().BeFalse();
    }

    [Fact]
    public async Task RenewalWorkflow_OnlyOwnerCanRenew()
    {
        // Arrange
        const string lockKey = "resource:users";
        const string owner1 = "worker-1";
        const string owner2 = "worker-2";
        var duration = TimeSpan.FromSeconds(30);

        await _lockService.TryAcquireAsync(lockKey, owner1, duration);

        // Act
        var renewed = await _lockService.RenewAsync(lockKey, owner2, duration);

        // Assert
        renewed.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // Fencing Token Workflow
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FencingTokenWorkflow_IssueAndValidate()
    {
        // Arrange
        const string lockKey = "resource:critical";
        const string ownerId = "worker-1";
        var duration = TimeSpan.FromSeconds(30);

        // Act 1: Acquire lock
        var (acquired, _, _) = await _lockService.TryAcquireAsync(lockKey, ownerId, duration);

        // Act 2: Issue fencing token
        var token = _fencingService.IssueToken(lockKey);

        // Act 3: Validate token
        var isValid = _fencingService.ValidateToken(lockKey, token);

        // Assert
        acquired.Should().BeTrue();
        token.Should().NotBeNull();
        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task FencingTokenWorkflow_PreventStaleWrites()
    {
        // Arrange
        const string lockKey = "resource:data";
        const string ownerId = "worker-1";
        var duration = TimeSpan.FromSeconds(30);

        await _lockService.TryAcquireAsync(lockKey, ownerId, duration);

        // Act 1: Worker gets a fencing token
        var token1 = _fencingService.IssueToken(lockKey);

        // Act 2: Token is incremented (simulating new holder)
        var token2 = _fencingService.IncrementToken(lockKey);

        // Act 3: Original worker tries to validate stale token
        var isValid = _fencingService.ValidateToken(lockKey, token1);

        // Assert — stale token should be rejected
        token1.SequenceNumber.Should().BeLessThan(token2.SequenceNumber);
        isValid.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // Concurrency Tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ConcurrencyTest_MultipleWorkersRacing()
    {
        // Arrange
        const string lockKey = "resource:shared";
        var duration = TimeSpan.FromSeconds(10);
        var winners = new System.Collections.Concurrent.ConcurrentBag<string>();

        // Act — multiple workers racing to acquire the same lock
        var tasks = Enumerable.Range(0, 10)
            .Select(i => Task.Run(async () =>
            {
                var workerId = $"worker-{i}";
                var (acquired, _, _) = await _lockService.TryAcquireAsync(lockKey, workerId, duration);
                if (acquired)
                {
                    winners.Add(workerId);
                }
            }))
            .ToList();

        await Task.WhenAll(tasks);

        // Assert — exactly one worker should win
        winners.Should().HaveCount(1);
    }

    [Fact]
    public async Task ConcurrencyTest_ConcurrentRenewal()
    {
        // Arrange
        const string lockKey = "resource:concurrent-renew";
        const string ownerId = "worker-1";
        var duration = TimeSpan.FromSeconds(30);

        var (acquired, _, _) = await _lockService.TryAcquireAsync(lockKey, ownerId, duration);
        acquired.Should().BeTrue();

        // Act — multiple renewals at the same time
        var renewalTasks = Enumerable.Range(0, 5)
            .Select(_ => _lockService.RenewAsync(lockKey, ownerId, duration))
            .ToList();

        var results = await Task.WhenAll(renewalTasks);

        // Assert — all renewals should succeed (or at least some should)
        results.Count(r => r).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ConcurrencyTest_AcquireReleaseRapidly()
    {
        // Arrange
        const string lockKey = "resource:rapid";
        var duration = TimeSpan.FromSeconds(5);

        // Act — rapid acquire/release cycle
        var tasks = new List<Task>();
        for (int iteration = 0; iteration < 10; iteration++)
        {
            var (acquired, @lock, _) = await _lockService.TryAcquireAsync(
                lockKey,
                $"worker-{iteration % 3}",
                duration);

            if (acquired && @lock != null)
            {
                tasks.Add(_lockService.ReleaseAsync(lockKey, @lock.OwnerId));
            }
        }

        await Task.WhenAll(tasks);

        // Assert — no lock should be held at the end
        var isLocked = await _lockService.IsLockedAsync(lockKey);
        isLocked.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // Edge Cases
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EdgeCase_ReleaseUnheldLock()
    {
        // Act
        var released = await _lockService.ReleaseAsync("resource:nonexistent", "worker-1");

        // Assert
        released.Should().BeFalse();
    }

    [Fact]
    public async Task EdgeCase_ReleaseWithWrongOwner()
    {
        // Arrange
        const string lockKey = "resource:owned";
        const string owner1 = "worker-1";
        const string owner2 = "worker-2";
        var duration = TimeSpan.FromSeconds(30);

        await _lockService.TryAcquireAsync(lockKey, owner1, duration);

        // Act
        var released = await _lockService.ReleaseAsync(lockKey, owner2);

        // Assert
        released.Should().BeFalse();

        // Cleanup
        await _lockService.ReleaseAsync(lockKey, owner1);
    }

    [Fact]
    public async Task EdgeCase_GetAllLocksFiltersExpired()
    {
        // Arrange — create some active and expired locks
        var activeLock = new Lock("lock:active", "owner-1", TimeSpan.FromSeconds(30))
        {
            Status = LockStatus.Held
        };
        var expiredLock = new Lock("lock:expired", "owner-2", TimeSpan.FromSeconds(30))
        {
            Status = LockStatus.Held,
            ExpiresAt = DateTime.UtcNow.AddSeconds(-1)
        };

        await _repository.AcquireAsync(activeLock);
        await _repository.AcquireAsync(expiredLock);

        // Act
        var allLocks = await _lockService.GetAllActiveLockAsync();

        // Assert
        var lockKeys = allLocks.Select(l => l.Key).ToList();
        lockKeys.Should().Contain("lock:active");
        lockKeys.Should().NotContain("lock:expired");
    }

    // -------------------------------------------------------------------------
    // Metrics Verification
    // -------------------------------------------------------------------------

    [Fact]
    public async Task MetricsTest_TrackingAcquisitionAndRelease()
    {
        // Arrange
        const string lockKey = "resource:metrics-test";
        const string ownerId = "worker-1";
        var duration = TimeSpan.FromSeconds(10);

        // Act
        var (acquired, _, _) = await _lockService.TryAcquireAsync(lockKey, ownerId, duration);
        await _lockService.ReleaseAsync(lockKey, ownerId);

        // Assert
        var metrics = _lockService.GetMetrics();
        metrics.SuccessfulAcquisitions.Should().BeGreaterThan(0);
        metrics.TotalReleases.Should().BeGreaterThan(0);
    }
}
