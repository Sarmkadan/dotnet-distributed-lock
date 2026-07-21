#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SarmKadan.DistributedLock.Services;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Tests for the DeadlockDetector class.
/// </summary>
public class DeadlockDetectorTests
{
    private readonly DeadlockDetector _detector = new(NullLogger<DeadlockDetector>.Instance);

    /// <summary>
    /// Tests that the constructor throws an ArgumentNullException when the logger is null.
    /// </summary>
    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new DeadlockDetector(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    // -------------------------------------------------------------------------
    // WouldDeadlock Tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests that WouldDeadlock returns false when there are no existing ownerships.
    /// </summary>
    [Fact]
    public void WouldDeadlock_WithNoExistingOwnership_ReturnsFalse()
    {
        // Act — no locks held, so no deadlock possible
        var result = _detector.WouldDeadlock("owner-A", "lock:1");

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Tests that WouldDeadlock returns true when there is a simple circular wait.
    /// </summary>
    [Fact]
    public async Task WouldDeadlock_WithSimpleCircularWait_ReturnsTrue()
    {
        // Arrange
        // owner-A holds lock:1
        // owner-B wants lock:1 and holds lock:2
        // owner-A wants lock:2 → circular dependency
        _detector.RecordAcquired("owner-A", "lock:1");
        _detector.RecordAcquired("owner-B", "lock:2");

        // owner-B is waiting for lock:1
        await _detector.RecordWaitingAsync("owner-B", "lock:1");

        // Act — owner-A tries to wait for lock:2 (which owner-B holds)
        var isDeadlock = _detector.WouldDeadlock("owner-A", "lock:2");

        // Assert
        isDeadlock.Should().BeTrue();
    }

    /// <summary>
    /// Tests that WouldDeadlock returns false when there is no circular wait.
    /// </summary>
    [Fact]
    public void WouldDeadlock_WithoutCircularWait_ReturnsFalse()
    {
        // Arrange
        // owner-A holds lock:1
        // owner-B wants lock:1
        // (no circular wait)
        _detector.RecordAcquired("owner-A", "lock:1");

        // Act
        var isDeadlock = _detector.WouldDeadlock("owner-B", "lock:1");

        // Assert
        isDeadlock.Should().BeFalse();
    }

    /// <summary>
    /// Tests that WouldDeadlock detects a deadlock with a longer chain.
    /// </summary>
    [Fact]
    public async Task WouldDeadlock_WithLongerChain_DetectsDeadlock()
    {
        // Arrange — create a chain: lock:1 ← owner-A ← lock:2 ← owner-B ← lock:3 ← owner-C
        // Then owner-C waits for lock:1 → circular
        _detector.RecordAcquired("owner-A", "lock:1");
        _detector.RecordAcquired("owner-B", "lock:2");
        _detector.RecordAcquired("owner-C", "lock:3");

        // Simulate wait chain
        await _detector.RecordWaitingAsync("owner-A", "lock:2"); // owner-A waits for lock:2
        await _detector.RecordWaitingAsync("owner-B", "lock:3"); // owner-B waits for lock:3

        // Act — owner-C tries to wait for lock:1
        var isDeadlock = _detector.WouldDeadlock("owner-C", "lock:1");

        // Assert
        isDeadlock.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // RecordAcquired and RecordReleased
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests that RecordWaitingAsync throws an ArgumentNullException when the owner ID is null.
    /// </summary>
    [Fact]
    public async Task RecordWaitingAsync_WithNullOwnerId_ThrowsArgumentNullException()
    {
        // Act & Assert
        await FluentActions.Invoking(() => _detector.RecordWaitingAsync(null!, "lock:1"))
            .Should().ThrowAsync<ArgumentNullException>().WithParameterName("ownerId");
    }

    /// <summary>
    /// Tests that RecordWaitingAsync throws an ArgumentNullException when the lock key is null.
    /// </summary>
    [Fact]
    public async Task RecordWaitingAsync_WithNullLockKey_ThrowsArgumentNullException()
    {
        // Act & Assert
        await FluentActions.Invoking(() => _detector.RecordWaitingAsync("owner-1", null!))
            .Should().ThrowAsync<ArgumentNullException>().WithParameterName("lockKey");
    }

    /// <summary>
    /// Tests that RecordWaitingAsync tracks a waiter.
    /// </summary>
    [Fact]
    public async Task RecordWaitingAsync_TracksWaiter()
    {
        // Act
        await _detector.RecordWaitingAsync("owner-A", "lock:1");

        // Assert
        var metrics = _detector.GetMetrics("lock:1");
        metrics.Should().NotBeNull();
        metrics!.CurrentWaiters.Should().Be(1);
    }

    /// <summary>
    /// Tests that RecordAcquired registers ownership.
    /// </summary>
    [Fact]
    public void RecordAcquired_RegistersOwnership()
    {
        // Act
        _detector.RecordAcquired("owner-A", "lock:1");

        // Assert
        var isDeadlock = _detector.WouldDeadlock("owner-B", "lock:1");
        isDeadlock.Should().BeFalse(); // owner-B waiting for lock:1 (owned by owner-A) is not a deadlock by itself
    }

    /// <summary>
    /// Tests that RecordReleased clears ownership.
    /// </summary>
    [Fact]
    public void RecordReleased_ClearsOwnership()
    {
        // Arrange
        _detector.RecordAcquired("owner-A", "lock:1");

        // Act
        _detector.RecordReleased("owner-A", "lock:1");

        // Assert — lock is no longer owned, so different owner can acquire without deadlock
        var isDeadlock = _detector.WouldDeadlock("owner-B", "lock:1");
        isDeadlock.Should().BeFalse();
    }

    /// <summary>
    /// Tests that RecordReleased does not remove ownership when the owner is incorrect.
    /// </summary>
    [Fact]
    public void RecordReleased_WithWrongOwner_DoesNotRemove()
    {
        // Arrange
        _detector.RecordAcquired("owner-A", "lock:1");

        // Act — wrong owner tries to release
        _detector.RecordReleased("owner-B", "lock:1");

        // Assert — lock should still be owned by owner-A
        var isDeadlock = _detector.WouldDeadlock("owner-B", "lock:1");
        isDeadlock.Should().BeFalse(); // still not a deadlock, just contention
    }

    // -------------------------------------------------------------------------
    // Metrics
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests that GetMetrics returns null when there is no data.
    /// </summary>
    [Fact]
    public async Task GetMetrics_WithNoData_ReturnsNull()
    {
        // Act
        var metrics = _detector.GetMetrics("nonexistent");

        // Assert
        metrics.Should().BeNull();
    }

    /// <summary>
    /// Tests that GetMetrics returns metrics after a waiter is added.
    /// </summary>
    [Fact]
    public async Task GetMetrics_AfterWaiterAdded_ReturnsMetrics()
    {
        // Arrange
        await _detector.RecordWaitingAsync("owner-A", "lock:1");

        // Act
        var metrics = _detector.GetMetrics("lock:1");

        // Assert
        metrics.Should().NotBeNull();
        metrics!.LockKey.Should().Be("lock:1");
        metrics.CurrentWaiters.Should().Be(1);
    }

    /// <summary>
    /// Tests that GetMetrics increments the deadlock counter after a deadlock is detected.
    /// </summary>
    [Fact]
    public async Task GetMetrics_AfterDeadlockDetected_IncrementsCounter()
    {
        // Arrange
        _detector.RecordAcquired("owner-A", "lock:1");
        _detector.RecordAcquired("owner-B", "lock:2");
        await _detector.RecordWaitingAsync("owner-A", "lock:2");

        // Act — deadlock: owner-B wants lock:1 (held by owner-A who waits for lock:2 held by owner-B)
        await _detector.RecordWaitingAsync("owner-B", "lock:1");

        // Assert
        var metrics = _detector.GetMetrics("lock:1");
        metrics!.DeadlocksDetected.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Tests that GetAllMetrics returns all tracked locks.
    /// </summary>
    [Fact]
    public async Task GetAllMetrics_ReturnsAllTrackedLocks()
    {
        // Arrange
        await _detector.RecordWaitingAsync("owner-A", "lock:1");
        await _detector.RecordWaitingAsync("owner-B", "lock:2");
        await _detector.RecordWaitingAsync("owner-C", "lock:3");

        // Act
        var allMetrics = _detector.GetAllMetrics();

        // Assert
        allMetrics.Should().HaveCount(3);
        allMetrics.Select(m => m.LockKey).Should().Contain(new[] { "lock:1", "lock:2", "lock:3" });
    }

    // -------------------------------------------------------------------------
    // RecordWaitEnded
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests that RecordWaitEndedAsync removes a waiter.
    /// </summary>
    [Fact]
    public async Task RecordWaitEndedAsync_RemovesWaiter()
    {
        // Arrange
        await _detector.RecordWaitingAsync("owner-A", "lock:1");

        // Act
        await _detector.RecordWaitEndedAsync("owner-A", "lock:1", 500);

        // Assert — waiter should be removed
        var metrics = _detector.GetMetrics("lock:1");
        metrics!.CurrentWaiters.Should().Be(0);
    }

    /// <summary>
    /// Tests that RecordWaitEndedAsync records the wait time.
    /// </summary>
    [Fact]
    public async Task RecordWaitEndedAsync_RecordsWaitTime()
    {
        // Arrange
        await _detector.RecordWaitingAsync("owner-A", "lock:1");

        // Act
        await _detector.RecordWaitEndedAsync("owner-A", "lock:1", 1234.5);

        // Assert
        var metrics = _detector.GetMetrics("lock:1");
        metrics!.AverageWaitTimeMs.Should().BeApproximately(1234.5, 0.1);
    }

    /// <summary>
    /// Tests that RecordWaitEndedAsync calculates the average wait time for multiple waits.
    /// </summary>
    [Fact]
    public async Task RecordWaitEndedAsync_MultipleWaits_CalculatesAverageWaitTime()
    {
        // Arrange
        await _detector.RecordWaitingAsync("owner-A", "lock:1");
        await _detector.RecordWaitingAsync("owner-B", "lock:1");

        // Act
        await _detector.RecordWaitEndedAsync("owner-A", "lock:1", 100);
        await _detector.RecordWaitEndedAsync("owner-B", "lock:1", 200);

        // Assert
        var metrics = _detector.GetMetrics("lock:1");
        metrics!.AverageWaitTimeMs.Should().BeApproximately(150, 1);
    }

    // -------------------------------------------------------------------------
    // Concurrency
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests that concurrent waiting and acquisition maintains consistency.
    /// </summary>
    [Fact]
    public async Task ConcurrentWaitingAndAcquisition_MaintainsConsistency()
    {
        // Arrange & Act — simulate concurrent access patterns
        var tasks = new List<Task>();

        for (int i = 0; i < 10; i++)
        {
            var owner = $"owner-{i}";
            var lockKey = $"lock-{i % 3}";
            tasks.Add(_detector.RecordWaitingAsync(owner, lockKey));
            tasks.Add(Task.Run(() => _detector.RecordAcquired(owner, lockKey)));
        }

        // Act
        await Task.WhenAll(tasks);

        // Assert — no exceptions thrown and metrics are available
        var allMetrics = _detector.GetAllMetrics();
        allMetrics.Should().NotBeEmpty();
    }

        // -------------------------------------------------------------------------
        // Edge Cases: Self-wait, 3-node cycle, diamond shape, cycle broken
        // -------------------------------------------------------------------------

        /// <summary>
        /// Tests that WouldDeadlock returns true when an owner tries to wait for a lock they already hold.
        /// Self-wait scenario: A holds lock:1 and tries to wait for lock:1.
        /// </summary>
        [Fact]
        public void WouldDeadlock_SelfWait_ReturnsTrue()
        {
        // Arrange - owner-A holds lock:1
        _detector.RecordAcquired("owner-A", "lock:1");

        // Act - owner-A tries to wait for lock:1 (which they already hold)
        var isDeadlock = _detector.WouldDeadlock("owner-A", "lock:1");

        // Assert - self-wait should be detected as a deadlock
        isDeadlock.Should().BeTrue();
        }

        /// <summary>
        /// Tests that WouldDeadlock detects a 3-node cycle: A->B->C->A.
        /// </summary>
        [Fact]
        public async Task WouldDeadlock_ThreeNodeCycle_DetectsDeadlock()
        {
        // Arrange - create a 3-node cycle:
        // owner-A holds lock:1
        // owner-B holds lock:2
        // owner-C holds lock:3
        // owner-A waits for lock:2 (held by owner-B)
        // owner-B waits for lock:3 (held by owner-C)
        // owner-C waits for lock:1 (held by owner-A) -> cycle A->B->C->A

        _detector.RecordAcquired("owner-A", "lock:1");
        _detector.RecordAcquired("owner-B", "lock:2");
        _detector.RecordAcquired("owner-C", "lock:3");

        await _detector.RecordWaitingAsync("owner-A", "lock:2");
        await _detector.RecordWaitingAsync("owner-B", "lock:3");

        // Act - owner-C tries to wait for lock:1
        var isDeadlock = _detector.WouldDeadlock("owner-C", "lock:1");

        // Assert - 3-node cycle should be detected
        isDeadlock.Should().BeTrue();
        }

        /// <summary>
        /// Tests that WouldDeadlock returns false for a diamond wait shape with no cycle.
        /// Diamond shape: A holds lock:1, B holds lock:2, C waits for lock:1, D waits for lock:2
        /// D tries to wait for lock:1 (held by A) - this creates a diamond but no cycle.
        /// </summary>
        [Fact]
        public async Task WouldDeadlock_DiamondShape_NoCycle_ReturnsFalse()
        {
        // Arrange - create diamond shape with no cycle:
        // owner-A holds lock:1
        // owner-B holds lock:2
        // owner-C waits for lock:1 (held by owner-A)
        // owner-D waits for lock:2 (held by owner-B)
        // owner-D tries to wait for lock:1 (held by owner-A) - no cycle formed

        _detector.RecordAcquired("owner-A", "lock:1");
        _detector.RecordAcquired("owner-B", "lock:2");

        await _detector.RecordWaitingAsync("owner-C", "lock:1");
        await _detector.RecordWaitingAsync("owner-D", "lock:2");

        // Act - owner-D tries to wait for lock:1 (held by owner-A)
        var isDeadlock = _detector.WouldDeadlock("owner-D", "lock:1");

        // Assert - diamond shape with no cycle should NOT be detected as deadlock
        isDeadlock.Should().BeFalse();
        }

        /// <summary>
        /// Tests that WouldDeadlock returns false after a waiter is removed, breaking the cycle.
        /// </summary>
        [Fact]
        public async Task WouldDeadlock_AfterWaiterRemoved_CycleBroken_ReturnsFalse()
        {
        // Arrange - create a cycle A->B->A
        // owner-A holds lock:1
        // owner-B holds lock:2
        // owner-A waits for lock:2
        // owner-B waits for lock:1 -> cycle A->B->A

        _detector.RecordAcquired("owner-A", "lock:1");
        _detector.RecordAcquired("owner-B", "lock:2");

        await _detector.RecordWaitingAsync("owner-A", "lock:2");
        await _detector.RecordWaitingAsync("owner-B", "lock:1");

        // Verify deadlock is detected initially
        var initialDeadlock = _detector.WouldDeadlock("owner-B", "lock:1");
        initialDeadlock.Should().BeTrue();

        // Act - owner-A acquires lock:2, breaking the cycle
        _detector.RecordAcquired("owner-A", "lock:2");

        // Assert - cycle is broken, no deadlock should be detected
        var isDeadlock = _detector.WouldDeadlock("owner-B", "lock:1");
        isDeadlock.Should().BeFalse();
        }

        /// <summary>
        /// Tests that WouldDeadlock correctly handles complex wait chains with multiple branches.
        /// </summary>
        [Fact]
        public async Task WouldDeadlock_ComplexWaitChain_DetectsDeadlock()
        {
        // Arrange - create complex wait chain with cycle:
        // owner-A holds lock:1
        // owner-B holds lock:2
        // owner-C holds lock:3
        // owner-D holds lock:4
        // owner-A waits for lock:2
        // owner-B waits for lock:3
        // owner-C waits for lock:4
        // owner-D waits for lock:1 -> cycle A->B->C->D->A

        _detector.RecordAcquired("owner-A", "lock:1");
        _detector.RecordAcquired("owner-B", "lock:2");
        _detector.RecordAcquired("owner-C", "lock:3");
        _detector.RecordAcquired("owner-D", "lock:4");

        await _detector.RecordWaitingAsync("owner-A", "lock:2");
        await _detector.RecordWaitingAsync("owner-B", "lock:3");
        await _detector.RecordWaitingAsync("owner-C", "lock:4");

        // Act - owner-D tries to wait for lock:1
        var isDeadlock = _detector.WouldDeadlock("owner-D", "lock:1");

        // Assert - complex 4-node cycle should be detected
        isDeadlock.Should().BeTrue();
        }

        /// <summary>
        /// Tests that RecordWaitEndedAsync removes a waiter and breaks any potential deadlock chains.
        /// </summary>
        [Fact]
        public async Task RecordWaitEndedAsync_RemovesWaiter_BreaksDeadlockChain()
        {
        // Arrange - create cycle A->B->A
        _detector.RecordAcquired("owner-A", "lock:1");
        _detector.RecordAcquired("owner-B", "lock:2");
        await _detector.RecordWaitingAsync("owner-A", "lock:2");
        await _detector.RecordWaitingAsync("owner-B", "lock:1");

        // Verify deadlock exists
        _detector.WouldDeadlock("owner-B", "lock:1").Should().BeTrue();

        // Act - owner-A ends wait for lock:2
        await _detector.RecordWaitEndedAsync("owner-A", "lock:2", 500);

        // Assert - cycle is broken, no deadlock
        var isDeadlock = _detector.WouldDeadlock("owner-B", "lock:1");
        isDeadlock.Should().BeFalse();
        }

        /// <summary>
        /// Tests that WouldDeadlock handles empty wait chains correctly.
        /// </summary>
        [Fact]
        public void WouldDeadlock_EmptyWaitChain_ReturnsFalse()
        {
        // Act - no locks held, no waiters
        var isDeadlock = _detector.WouldDeadlock("owner-A", "lock:1");

        // Assert - empty chain should not be a deadlock
        isDeadlock.Should().BeFalse();
        }
}
