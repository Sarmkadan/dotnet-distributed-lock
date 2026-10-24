#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SarmKadan.DistributedLock.Repository;
using SarmKadan.DistributedLock.Workers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Unit tests for <see cref="LockCleanupWorker"/> which provides background cleanup of expired locks.
/// Tests cover constructor validation, on-demand cleanup logic, the background <c>ExecuteAsync</c> loop
/// (error recovery, scheduling, batch limits, cancellation), and string representation.
/// </summary>
public class LockCleanupWorkerTests
{
    private readonly Mock<ILockRepository> _repositoryMock;
    private readonly LockCleanupWorker _worker;

    public LockCleanupWorkerTests()
    {
        _repositoryMock = new Mock<ILockRepository>();
        _worker = new LockCleanupWorker(_repositoryMock.Object, NullLogger<LockCleanupWorker>.Instance);
    }

    // -------------------------------------------------------------------------
    // Constructor guards
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new LockCleanupWorker(null!, NullLogger<LockCleanupWorker>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new LockCleanupWorker(_repositoryMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    // -------------------------------------------------------------------------
    // RunCleanupOnceAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunCleanupOnceAsync_DeletesExpiredLocksAndReturnsCount()
    {
        // Arrange
        var expiredLocks = new[]
        {
            new Lock("lock1", "owner1", TimeSpan.FromSeconds(30)) { ExpiresAt = DateTime.UtcNow.AddMinutes(-10) },
            new Lock("lock2", "owner2", TimeSpan.FromSeconds(30)) { ExpiresAt = DateTime.UtcNow.AddMinutes(-20) },
            new Lock("lock3", "owner3", TimeSpan.FromSeconds(30)) { ExpiresAt = DateTime.UtcNow.AddMinutes(-30) }
        };

        _repositoryMock
            .Setup(r => r.GetExpiredLocksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredLocks);

        // Setup DeleteLockIfExpirationMatchesAsync to return true for each lock
        _repositoryMock
            .Setup(r => r.DeleteLockIfExpirationMatchesAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _worker.RunCleanupOnceAsync();

        // Assert
        var cleanedCountField = typeof(LockCleanupWorker).GetField("_cleanedCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cleanedCount = (int)cleanedCountField!.GetValue(_worker)!;
        cleanedCount.Should().Be(3);
        _repositoryMock.Verify(
            r => r.DeleteLockIfExpirationMatchesAsync(
                "lock1",
                expiredLocks[0].ExpiresAt,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _repositoryMock.Verify(
            r => r.DeleteLockIfExpirationMatchesAsync(
                "lock2",
                expiredLocks[1].ExpiresAt,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _repositoryMock.Verify(
            r => r.DeleteLockIfExpirationMatchesAsync(
                "lock3",
                expiredLocks[2].ExpiresAt,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunCleanupOnceAsync_SkipsLocksWhenDeleteReturnsFalse()
    {
        // Arrange
        var expiredLocks = new[]
        {
            new Lock("lock1", "owner1", TimeSpan.FromSeconds(30)) { ExpiresAt = DateTime.UtcNow.AddMinutes(-10) },
            new Lock("lock2", "owner2", TimeSpan.FromSeconds(30)) { ExpiresAt = DateTime.UtcNow.AddMinutes(-20) }
        };

        _repositoryMock
            .Setup(r => r.GetExpiredLocksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredLocks);

        // First lock deleted successfully, second lock fails (expired again)
        _repositoryMock
            .Setup(r => r.DeleteLockIfExpirationMatchesAsync(
                "lock1",
                expiredLocks[0].ExpiresAt,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repositoryMock
            .Setup(r => r.DeleteLockIfExpirationMatchesAsync(
                "lock2",
                expiredLocks[1].ExpiresAt,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _worker.RunCleanupOnceAsync();

        // Assert
        var cleanedCountField = typeof(LockCleanupWorker).GetField("_cleanedCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cleanedCount = (int)cleanedCountField!.GetValue(_worker)!;
        cleanedCount.Should().Be(1); // only the first lock counted as cleaned
        _repositoryMock.Verify(
            r => r.DeleteLockIfExpirationMatchesAsync(
                "lock1",
                expiredLocks[0].ExpiresAt,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _repositoryMock.Verify(
            r => r.DeleteLockIfExpirationMatchesAsync(
                "lock2",
                expiredLocks[1].ExpiresAt,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunCleanupOnceAsync_ThrowsOperationCanceledException_WhenCancellationRequested()
    {
        // Arrange
        var expiredLocks = new[]
        {
            new Lock("lock1", "owner1", TimeSpan.FromSeconds(30)) { ExpiresAt = DateTime.UtcNow.AddSeconds(-10) }
        };

        _repositoryMock
            .Setup(r => r.GetExpiredLocksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredLocks);

        // Setup DeleteLockIfExpirationMatchesAsync to throw OperationCanceledException when called
        _repositoryMock
            .Setup(r => r.DeleteLockIfExpirationMatchesAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        Func<Task> act = async () => await _worker.RunCleanupOnceAsync(new CancellationToken(true));

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // -------------------------------------------------------------------------
    // ToString
    // -------------------------------------------------------------------------

    [Fact]
    public void ToString_ReturnsFormattedOptions()
    {
        // Arrange
        var options = new LockCleanupWorkerOptions
        {
            InitialDelayMs = 100,
            CleanupIntervalMs = 1000,
            BatchSize = 10,
            VerboseLogging = true,
            MinimumExpiredDuration = TimeSpan.FromMinutes(2)
        };

        var workerWithOptions = new LockCleanupWorker(_repositoryMock.Object, NullLogger<LockCleanupWorker>.Instance, options);

        // Act
        var result = workerWithOptions.ToString();

        // Assert
        result.Should().Contain("InitialDelayMs = 100");
        result.Should().Contain("CleanupIntervalMs = 1000");
        result.Should().Contain("BatchSize = 10");
        result.Should().Contain("VerboseLogging = True");
        result.Should().Contain("MinimumExpiredDuration = 00:02:00");
    }

    // -------------------------------------------------------------------------
    // ExecuteAsync (background loop, driven through StartAsync/StopAsync)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Keeps the background loop's sweep timing observable without relying on the production clock internals.
    /// </summary>
    private sealed class SweepProbe
    {
        private readonly SemaphoreSlim _sweepStarted = new(0);
        private readonly ConcurrentQueue<long> _sweepTimestamps = new();

        /// <summary>Gets the number of sweeps that have started.</summary>
        public int SweepCount => _sweepTimestamps.Count;

        /// <summary>Gets the start timestamps (<see cref="Stopwatch"/> ticks) of every sweep, in order.</summary>
        public long[] SweepTimestamps => _sweepTimestamps.ToArray();

        /// <summary>Records that a sweep has started and releases anyone waiting on it.</summary>
        public void RecordSweepStarted()
        {
            _sweepTimestamps.Enqueue(Stopwatch.GetTimestamp());
            _sweepStarted.Release();
        }

        /// <summary>Waits until the given number of additional sweeps have started, failing the test on timeout.</summary>
        /// <param name="count">The number of sweep starts to wait for.</param>
        public async Task WaitForSweepsAsync(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var signaled = await _sweepStarted.WaitAsync(SweepTimeout);
                signaled.Should().BeTrue($"sweep {i + 1} of {count} should start within {SweepTimeout}");
            }
        }
    }

    private static readonly TimeSpan SweepTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Options that make the first sweep run immediately and the loop tick at the shortest allowed interval.
    /// </summary>
    private static LockCleanupWorkerOptions FastOptions(int batchSize = LockCleanupWorkerOptions.DefaultBatchSize) => new()
    {
        InitialDelayMs = 0,
        CleanupIntervalMs = LockCleanupWorkerOptions.MinCleanupIntervalMs,
        BatchSize = batchSize
    };

    private static Lock LockExpiringAt(string key, DateTime expiresAt) =>
        new(key, "owner", TimeSpan.FromSeconds(30)) { ExpiresAt = expiresAt };

    private LockCleanupWorker CreateWorker(LockCleanupWorkerOptions options) =>
        new(_repositoryMock.Object, NullLogger<LockCleanupWorker>.Instance, options);

    /// <summary>
    /// Wires <c>GetExpiredLocksAsync</c> so that the Nth call (1-based) returns <paramref name="sweep"/>'s result
    /// and each call is recorded by the returned probe.
    /// </summary>
    private SweepProbe SetupSweeps(Func<int, Task<IEnumerable<Lock>>> sweep)
    {
        var probe = new SweepProbe();
        var calls = 0;
        _repositoryMock
            .Setup(r => r.GetExpiredLocksAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                probe.RecordSweepStarted();
                return sweep(++calls);
            });
        return probe;
    }

    private void SetupDeletesToSucceed() =>
        _repositoryMock
            .Setup(r => r.DeleteLockIfExpirationMatchesAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

    /// <summary>Verifies that a sweep which throws does not end the loop; the next sweep still runs.</summary>
    [Fact]
    public async Task ExecuteAsync_KeepsSweeping_WhenASweepThrows()
    {
        // Arrange
        var probe = SetupSweeps(call => call == 1
            ? Task.FromException<IEnumerable<Lock>>(new InvalidOperationException("transient backend failure"))
            : Task.FromResult<IEnumerable<Lock>>([]));
        var worker = CreateWorker(FastOptions());

        // Act
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await probe.WaitForSweepsAsync(2);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        // Assert
        probe.SweepCount.Should().BeGreaterThanOrEqualTo(2);
    }

    /// <summary>Verifies that cancelling the host while the loop waits out its interval stops the worker promptly.</summary>
    [Fact]
    public async Task ExecuteAsync_StopsPromptly_WhenCancelledDuringIntervalDelay()
    {
        // Arrange
        var probe = SetupSweeps(_ => Task.FromResult<IEnumerable<Lock>>([]));
        var worker = CreateWorker(new LockCleanupWorkerOptions
        {
            InitialDelayMs = 0,
            CleanupIntervalMs = LockCleanupWorkerOptions.DefaultCleanupIntervalMs
        });
        await worker.StartAsync(CancellationToken.None);
        await probe.WaitForSweepsAsync(1);

        // Act: the loop is now parked in a one-hour delay; a stop must not wait for it.
        // StopAsync returns once the loop exits or the token fires, so a stalled loop would take the full timeout.
        using var stopTimeout = new CancellationTokenSource(SweepTimeout);
        var stopwatch = Stopwatch.StartNew();
        await worker.StopAsync(stopTimeout.Token);
        stopwatch.Stop();

        // Assert
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
        probe.SweepCount.Should().Be(1);
    }

    /// <summary>Verifies the initial delay precedes the first sweep and the configured interval separates later sweeps.</summary>
    [Fact]
    public async Task ExecuteAsync_RespectsInitialDelayAndCleanupInterval()
    {
        // Arrange
        const int initialDelayMs = 300;
        var probe = SetupSweeps(_ => Task.FromResult<IEnumerable<Lock>>([]));
        var worker = CreateWorker(new LockCleanupWorkerOptions
        {
            InitialDelayMs = initialDelayMs,
            CleanupIntervalMs = LockCleanupWorkerOptions.MinCleanupIntervalMs
        });

        // Act
        var started = Stopwatch.GetTimestamp();
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await probe.WaitForSweepsAsync(2);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        // Assert: allow 10% slack for timer granularity; Task.Delay never fires meaningfully early.
        var sweeps = probe.SweepTimestamps;
        Stopwatch.GetElapsedTime(started, sweeps[0]).TotalMilliseconds
            .Should().BeGreaterThanOrEqualTo(initialDelayMs * 0.9);
        Stopwatch.GetElapsedTime(sweeps[0], sweeps[1]).TotalMilliseconds
            .Should().BeGreaterThanOrEqualTo(LockCleanupWorkerOptions.MinCleanupIntervalMs * 0.9);
    }

    /// <summary>Verifies that only expired locks are deleted, and active or within-grace-period locks are never touched.</summary>
    [Fact]
    public async Task ExecuteAsync_DeletesOnlyExpiredLocks_AndNeverTouchesActiveOnes()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var expired = new[]
        {
            LockExpiringAt("expired-1", now.AddMinutes(-10)),
            LockExpiringAt("expired-2", now.AddMinutes(-20))
        };
        var untouchable = new[]
        {
            LockExpiringAt("active-1", now.AddMinutes(5)),
            LockExpiringAt("active-2", now.AddHours(1)),
            // Expired, but inside the default 5-minute MinimumExpiredDuration grace window
            LockExpiringAt("grace-1", now.AddMinutes(-1))
        };
        var mixed = expired.Concat(untouchable).ToArray();
        string[] untouchableKeys = ["active-1", "active-2", "grace-1"];

        SetupDeletesToSucceed();
        var probe = SetupSweeps(call => Task.FromResult<IEnumerable<Lock>>(call == 1 ? mixed : []));
        var worker = CreateWorker(FastOptions());

        // Act
        await worker.StartAsync(CancellationToken.None);
        try
        {
            // The second sweep only begins after the first has fully completed its deletes.
            await probe.WaitForSweepsAsync(2);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        // Assert
        _repositoryMock.Verify(
            r => r.DeleteLockIfExpirationMatchesAsync("expired-1", expired[0].ExpiresAt, It.IsAny<CancellationToken>()),
            Times.Once);
        _repositoryMock.Verify(
            r => r.DeleteLockIfExpirationMatchesAsync("expired-2", expired[1].ExpiresAt, It.IsAny<CancellationToken>()),
            Times.Once);
        _repositoryMock.Verify(
            r => r.DeleteLockIfExpirationMatchesAsync(
                It.Is<string>(key => untouchableKeys.Contains(key)),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _repositoryMock.Verify(
            r => r.DeleteLockIfExpirationMatchesAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Exactly(expired.Length));
    }

    /// <summary>Verifies that a single sweep deletes no more than <see cref="LockCleanupWorkerOptions.BatchSize"/> locks.</summary>
    [Fact]
    public async Task ExecuteAsync_DeletesAtMostBatchSizeLocksPerSweep()
    {
        // Arrange
        const int batchSize = 2;
        var now = DateTime.UtcNow;
        var expired = Enumerable.Range(1, 5)
            .Select(i => LockExpiringAt($"expired-{i}", now.AddMinutes(-10 * i)))
            .ToArray();

        SetupDeletesToSucceed();
        string[] deferredKeys = ["expired-3", "expired-4", "expired-5"];
        var probe = SetupSweeps(call => Task.FromResult<IEnumerable<Lock>>(call == 1 ? expired : []));
        var worker = CreateWorker(FastOptions(batchSize));

        // Act
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await probe.WaitForSweepsAsync(2);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        // Assert: the first two eligible locks in repository order are deleted; the rest are deferred.
        _repositoryMock.Verify(
            r => r.DeleteLockIfExpirationMatchesAsync("expired-1", expired[0].ExpiresAt, It.IsAny<CancellationToken>()),
            Times.Once);
        _repositoryMock.Verify(
            r => r.DeleteLockIfExpirationMatchesAsync("expired-2", expired[1].ExpiresAt, It.IsAny<CancellationToken>()),
            Times.Once);
        _repositoryMock.Verify(
            r => r.DeleteLockIfExpirationMatchesAsync(
                It.Is<string>(key => deferredKeys.Contains(key)),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _repositoryMock.Verify(
            r => r.DeleteLockIfExpirationMatchesAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Exactly(batchSize));
    }
}