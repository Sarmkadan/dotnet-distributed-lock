#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =====================================================================

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SarmKadan.DistributedLock.Services;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Unit tests for <see cref="LockMonitor"/> class that verify lock monitoring functionality including registration,
/// unregistration, auto-renewal behavior, and thread safety.
/// </summary>
public class LockMonitorTests : IAsyncLifetime
{
    /// <summary>
    /// Mock of <see cref="ILockService"/> for testing purposes.
    /// </summary>
    private readonly Mock<ILockService> _lockServiceMock;

    /// <summary>
    /// Instance of <see cref="LockMonitor"/> being tested.
    /// </summary>
    private readonly LockMonitor _monitor;

    /// <summary>
    /// Initializes a new instance of <see cref="LockMonitorTests"/> class.
    /// </summary>
    public LockMonitorTests()
    {
        _lockServiceMock = new Mock<ILockService>();
        _monitor = new LockMonitor(_lockServiceMock.Object, NullLogger<LockMonitor>.Instance);
    }

    /// <summary>
    /// Initializes test resources asynchronously.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task InitializeAsync() => await Task.CompletedTask;

    /// <summary>
    /// Disposes test resources asynchronously.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task DisposeAsync()
    {
        await _monitor.StopMonitoringAsync();
        _monitor.Dispose();
    }

    /// <summary>
    /// Tests that constructor throws <see cref="ArgumentNullException"/> when null lockService is provided.
    /// </summary>
    [Fact]
    public void Constructor_WithNullLockService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new LockMonitor(null!, NullLogger<LockMonitor>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("lockService");
    }

    /// <summary>
    /// Tests that constructor throws <see cref="ArgumentNullException"/> when null logger is provided.
    /// </summary>
    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new LockMonitor(_lockServiceMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    // -------------------------------------------------------------------------
    // RegisterLock and UnregisterLock
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests that <see cref="LockMonitor.RegisterLock"/> adds a lock to the monitoring list.
    /// </summary>
    [Fact]
    public void RegisterLock_AddsLockToMonitoring()
    {
        // Act
        _monitor.RegisterLock("lock:1", "owner-1", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));

        // Assert
        var monitored = _monitor.GetMonitoredLocks().ToList();
        monitored.Should().Contain("lock:1");
    }

    /// <summary>
    /// Tests that <see cref="LockMonitor.RegisterLock"/> tracks multiple locks correctly.
    /// </summary>
    [Fact]
    public void RegisterLock_MultipleLocks_TracksAll()
    {
        // Act
        _monitor.RegisterLock("lock:1", "owner-1", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
        _monitor.RegisterLock("lock:2", "owner-2", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
        _monitor.RegisterLock("lock:3", "owner-3", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));

        // Assert
        var monitored = _monitor.GetMonitoredLocks().ToList();
        monitored.Should().HaveCount(3);
    }

    /// <summary>
    /// Tests that <see cref="LockMonitor.RegisterLock"/> does not duplicate locks when the same lock is registered twice.
    /// </summary>
    [Fact]
    public void RegisterLock_SameLockTwice_DoesNotDuplicate()
    {
        // Act
        _monitor.RegisterLock("lock:1", "owner-1", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
        _monitor.RegisterLock("lock:1", "owner-2", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));

        // Assert — only one entry for lock:1
        var monitored = _monitor.GetMonitoredLocks().ToList();
        monitored.Count(l => l == "lock:1").Should().Be(1);
    }

    /// <summary>
    /// Tests that <see cref="LockMonitor.UnregisterLock"/> removes a lock from monitoring.
    /// </summary>
    [Fact]
    public void UnregisterLock_RemovesLock()
    {
        // Arrange
        _monitor.RegisterLock("lock:1", "owner-1", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));

        // Act
        _monitor.UnregisterLock("lock:1");

        // Assert
        var monitored = _monitor.GetMonitoredLocks().ToList();
        monitored.Should().NotContain("lock:1");
    }

    /// <summary>
    /// Tests that <see cref="LockMonitor.UnregisterLock"/> does not throw when attempting to unregister a non-existent lock.
    /// </summary>
    [Fact]
    public void UnregisterLock_NonExistent_DoesNotThrow()
    {
        // Act & Assert
        _monitor.Invoking(m => m.UnregisterLock("nonexistent")).Should().NotThrow();
    }

    // -------------------------------------------------------------------------
    // StartMonitoring and StopMonitoring
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests that <see cref="LockMonitor.StartMonitoring"/> starts monitoring with default interval when no interval is specified.
    /// </summary>
    [Fact]
    public void StartMonitoring_WithoutInterval_StartsWithDefault()
    {
        // Act
        _monitor.StartMonitoring();

        // Assert — no exceptions
        _monitor.GetMonitoredLocks().Should().BeEmpty();
    }

    /// <summary>
    /// Tests that <see cref="LockMonitor.StartMonitoring"/> does not throw when called on an already running monitor.
    /// </summary>
    [Fact]
    public void StartMonitoring_AlreadyRunning_DoesNotThrow()
    {
        // Arrange
        _monitor.StartMonitoring();

        // Act & Assert — should not throw
        _monitor.Invoking(m => m.StartMonitoring()).Should().NotThrow();
    }

    /// <summary>
    /// Tests that <see cref="LockMonitor.StopMonitoringAsync"/> stops the monitoring loop.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task StopMonitoring_StopsTheLoop()
    {
        // Arrange
        _monitor.StartMonitoring(TimeSpan.FromMilliseconds(100));

        // Act
        await _monitor.StopMonitoringAsync();

        // Assert — no longer monitoring
        _monitor.GetMonitoredLocks().Should().BeEmpty();
    }

    /// <summary>
    /// Tests that <see cref="LockMonitor.StopMonitoringAsync"/> does not throw when called on a non-running monitor.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task StopMonitoring_WhenNotRunning_DoesNotThrow()
    {
        // Act & Assert
        await _monitor.Invoking(m => m.StopMonitoringAsync()).Should().NotThrowAsync();
    }

    // -------------------------------------------------------------------------
    // Auto-Renewal Behavior
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests that <see cref="LockMonitor.StartMonitoring"/> renews locks at the specified renewal interval.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task StartMonitoring_RenewsLocksAtInterval()
    {
        // Arrange
        var renewalInterval = TimeSpan.FromMilliseconds(50);
        _lockServiceMock
            .Setup(s => s.RenewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _monitor.RegisterLock("lock:1", "owner-1", renewalInterval, TimeSpan.FromSeconds(30));
        _monitor.StartMonitoring(TimeSpan.FromMilliseconds(25)); // check every 25ms

        // Act — wait for at least one renewal attempt
        await Task.Delay(200);

        // Assert
        _lockServiceMock.Verify(
            s => s.RenewAsync("lock:1", "owner-1", TimeSpan.FromSeconds(30), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        // Cleanup
        await _monitor.StopMonitoringAsync();
    }

    /// <summary>
    /// Tests that <see cref="LockMonitor.StartMonitoring"/> skips locks that are not due for renewal based on their renewal interval.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task Monitoring_SkipsLocksNotDueForRenewal()
    {
        // Arrange
        _lockServiceMock
            .Setup(s => s.RenewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Very long renewal interval so it won't renew during test
        _monitor.RegisterLock("lock:1", "owner-1", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
        _monitor.StartMonitoring(TimeSpan.FromMilliseconds(50));

        // Act
        await Task.Delay(150);

        // Assert
        _lockServiceMock.Verify(
            s => s.RenewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never, "renewal should not happen outside of interval");

        // Cleanup
        await _monitor.StopMonitoringAsync();
    }

    /// <summary>
    /// Tests that <see cref="LockMonitor.StartMonitoring"/> continues monitoring even when lock renewal fails.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task Monitoring_HandlesRenewalFailure()
    {
        // Arrange
        _lockServiceMock
            .Setup(s => s.RenewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // renewal fails

        _monitor.RegisterLock("lock:1", "owner-1", TimeSpan.FromMilliseconds(50), TimeSpan.FromSeconds(30));
        _monitor.StartMonitoring(TimeSpan.FromMilliseconds(25));

        // Act
        await Task.Delay(150);

        // Assert — should continue monitoring even if renewal fails
        var monitored = _monitor.GetMonitoredLocks().ToList();
        monitored.Should().Contain("lock:1");

        // Cleanup
        await _monitor.StopMonitoringAsync();
    }

    /// <summary>
    /// Tests that <see cref="LockMonitor.StartMonitoring"/> continues monitoring even when lock renewal throws an exception.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task Monitoring_HandlesRenewalException()
    {
        // Arrange
        _lockServiceMock
            .Setup(s => s.RenewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("backend error"));

        _monitor.RegisterLock("lock:1", "owner-1", TimeSpan.FromMilliseconds(50), TimeSpan.FromSeconds(30));
        _monitor.StartMonitoring(TimeSpan.FromMilliseconds(25));

        // Act
        await Task.Delay(150);

        // Assert — should continue monitoring even if renewal throws
        var monitored = _monitor.GetMonitoredLocks().ToList();
        monitored.Should().Contain("lock:1");

        // Cleanup
        await _monitor.StopMonitoringAsync();
    }

    // -------------------------------------------------------------------------
    // Thread Safety
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests that concurrent registration and unregistration operations maintain consistency.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task RegisterAndUnregisterConcurrently_MaintainsConsistency()
    {
        // Act
        var tasks = new List<Task>();
        for (int i = 0; i < 20; i++)
        {
            var lockKey = $"lock:{i}";
            tasks.Add(Task.Run(() => _monitor.RegisterLock(lockKey, "owner-1", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30))));
        }

        await Task.WhenAll(tasks);

        // Assert
        var monitored = _monitor.GetMonitoredLocks().ToList();
        monitored.Should().HaveCount(20);

        // Cleanup
        await _monitor.StopMonitoringAsync();
    }
}