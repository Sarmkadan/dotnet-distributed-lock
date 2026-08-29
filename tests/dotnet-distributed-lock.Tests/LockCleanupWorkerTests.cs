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
using System.Threading;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Unit tests for <see cref="LockCleanupWorker"/> which provides background cleanup of expired locks.
/// Tests cover constructor validation, cleanup logic, cancellation handling, and string representation.
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
            new Lock("lock1", "owner1", TimeSpan.FromSeconds(30)) { ExpiresAt = DateTime.UtcNow.AddSeconds(-10) },
            new Lock("lock2", "owner2", TimeSpan.FromSeconds(30)) { ExpiresAt = DateTime.UtcNow.AddSeconds(-20) },
            new Lock("lock3", "owner3", TimeSpan.FromSeconds(30)) { ExpiresAt = DateTime.UtcNow.AddSeconds(-30) }
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
        var cleanedCount = (int)cleanedCountField.GetValue(_worker)!;
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
            new Lock("lock1", "owner1", TimeSpan.FromSeconds(30)) { ExpiresAt = DateTime.UtcNow.AddSeconds(-10) },
            new Lock("lock2", "owner2", TimeSpan.FromSeconds(30)) { ExpiresAt = DateTime.UtcNow.AddSeconds(-20) }
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
        var cleanedCount = (int)cleanedCountField.GetValue(_worker)!;
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
            CleanupIntervalMs = 200,
            BatchSize = 10,
            VerboseLogging = true,
            MinimumExpiredDuration = TimeSpan.FromMinutes(2)
        };

        var workerWithOptions = new LockCleanupWorker(_repositoryMock.Object, NullLogger<LockCleanupWorker>.Instance, options);

        // Act
        var result = workerWithOptions.ToString();

        // Assert
        result.Should().Contain("InitialDelayMs = 100");
        result.Should().Contain("CleanupIntervalMs = 200");
        result.Should().Contain("BatchSize = 10");
        result.Should().Contain("VerboseLogging = True");
        result.Should().Contain("MinimumExpiredDuration = 00:02:00");
    }
}