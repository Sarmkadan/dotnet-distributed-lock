#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Repository;
using SarmKadan.DistributedLock.Services;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

public class LockServiceTests
{
    private readonly Mock<ILockRepository> _repositoryMock;
    private readonly LockService _service;

    public LockServiceTests()
    {
        _repositoryMock = new Mock<ILockRepository>();
        _service = new LockService(_repositoryMock.Object, NullLogger<LockService>.Instance);
    }

    // -------------------------------------------------------------------------
    // Constructor guards
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new LockService(null!, NullLogger<LockService>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new LockService(_repositoryMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    // -------------------------------------------------------------------------
    // TryAcquireAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TryAcquireAsync_WhenRepositoryGrantsLock_ReturnsTrueWithLock()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.AcquireAsync(It.IsAny<Lock>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var (success, @lock, errorMessage) =
            await _service.TryAcquireAsync("resource:checkout", "worker-1", TimeSpan.FromSeconds(30)).ConfigureAwait(false);

        // Assert
        success.Should().BeTrue();
        @lock.Should().NotBeNull();
        @lock!.Key.Should().Be("resource:checkout");
        @lock.OwnerId.Should().Be("worker-1");
        @lock.Status.Should().Be(LockStatus.Held);
        errorMessage.Should().BeNull();
    }

    [Fact]
    public async Task TryAcquireAsync_WhenRepositoryDeniesLock_ReturnsFalseWithErrorMessage()
    {
        // Arrange — another owner holds the lock
        _repositoryMock
            .Setup(r => r.AcquireAsync(It.IsAny<Lock>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var (success, @lock, errorMessage) =
            await _service.TryAcquireAsync("resource:checkout", "worker-2").ConfigureAwait(false);

        // Assert
        success.Should().BeFalse();
        @lock.Should().BeNull();
        errorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TryAcquireAsync_WhenRepositoryThrows_ReturnsFalseWithExceptionMessage()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.AcquireAsync(It.IsAny<Lock>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("backend unavailable"));

        // Act
        var (success, @lock, errorMessage) =
            await _service.TryAcquireAsync("resource:checkout", "worker-1").ConfigureAwait(false);

        // Assert
        success.Should().BeFalse();
        @lock.Should().BeNull();
        errorMessage.Should().Contain("backend unavailable");
    }

    // -------------------------------------------------------------------------
    // RenewAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RenewAsync_WhenRepositoryRenews_ReturnsTrue()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.RenewAsync("resource:orders", "worker-1", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var renewed = await _service.RenewAsync("resource:orders", "worker-1").ConfigureAwait(false);

        // Assert
        renewed.Should().BeTrue();
    }

    [Fact]
    public async Task RenewAsync_WhenRepositoryReturnsFalse_ReturnsFalse()
    {
        // Arrange — lock no longer owned by this worker
        _repositoryMock
            .Setup(r => r.RenewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var renewed = await _service.RenewAsync("resource:orders", "worker-99").ConfigureAwait(false);

        // Assert
        renewed.Should().BeFalse();
    }

    [Fact]
    public async Task RenewAsync_WhenRepositoryThrows_ReturnsFalse()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.RenewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("network timeout"));

        // Act
        var renewed = await _service.RenewAsync("resource:orders", "worker-1").ConfigureAwait(false);

        // Assert
        renewed.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // ReleaseAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ReleaseAsync_WhenLockNotFound_ReturnsFalse()
    {
        // Arrange — GetByKeyAsync returns null (lock does not exist)
        _repositoryMock
            .Setup(r => r.GetByKeyAsync("resource:missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lock?)null);

        // Act
        var released = await _service.ReleaseAsync("resource:missing", "worker-1").ConfigureAwait(false);

        // Assert
        released.Should().BeFalse();
        _repositoryMock.Verify(
            r => r.ReleaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReleaseAsync_WhenLockFoundAndReleased_ReturnsTrue()
    {
        // Arrange
        var existingLock = new Lock("resource:payments", "worker-1", TimeSpan.FromSeconds(30))
        {
            Status = LockStatus.Held
        };
        _repositoryMock
            .Setup(r => r.GetByKeyAsync("resource:payments", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLock);
        _repositoryMock
            .Setup(r => r.ReleaseAsync("resource:payments", "worker-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var released = await _service.ReleaseAsync("resource:payments", "worker-1").ConfigureAwait(false);

        // Assert
        released.Should().BeTrue();
        _repositoryMock.Verify(
            r => r.ReleaseAsync("resource:payments", "worker-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // -------------------------------------------------------------------------
    // IsLockedAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task IsLockedAsync_WhenRepositoryReturnsTrue_ReturnsTrue()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.ExistsAsync("resource:active", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var isLocked = await _service.IsLockedAsync("resource:active").ConfigureAwait(false);

        // Assert
        isLocked.Should().BeTrue();
    }

    [Fact]
    public async Task IsLockedAsync_WhenRepositoryThrows_ReturnsFalse()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("connection refused"));

        // Act
        var isLocked = await _service.IsLockedAsync("resource:flaky").ConfigureAwait(false);

        // Assert — service should swallow the exception and default to false
        isLocked.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // GetAllActiveLockAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllActiveLockAsync_WhenRepositoryReturnsLocks_ReturnsThem()
    {
        // Arrange
        var locks = new List<Lock>
        {
            new("res:one", "owner-1", TimeSpan.FromSeconds(30)) { Status = LockStatus.Held },
            new("res:two", "owner-2", TimeSpan.FromSeconds(30)) { Status = LockStatus.Held }
        };
        _repositoryMock
            .Setup(r => r.GetAllActiveLockAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(locks);

        // Act
        var result = await _service.GetAllActiveLockAsync().ConfigureAwait(false);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllActiveLockAsync_WhenRepositoryThrows_ReturnsEmptyEnumerable()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetAllActiveLockAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("storage error"));

        // Act
        var result = await _service.GetAllActiveLockAsync().ConfigureAwait(false);

        // Assert
        result.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // GetMetrics
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetMetrics_AfterSuccessfulAcquisition_ReflectsAcquisitionCount()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.AcquireAsync(It.IsAny<Lock>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _service.TryAcquireAsync("resource:metrics-test", "owner-1", TimeSpan.FromSeconds(10)).ConfigureAwait(false);

        // Act
        var metrics = _service.GetMetrics();

        // Assert
        metrics.Should().NotBeNull();
        metrics.SuccessfulAcquisitions.Should().BeGreaterThan(0);
    }
}

public class InMemoryLockRepositoryTests
{
    private readonly InMemoryLockRepository _repository = new();

    // -------------------------------------------------------------------------
    // AcquireAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AcquireAsync_WhenKeyDoesNotExist_ReturnsTrue()
    {
        // Arrange
        var @lock = new Lock("resource:new", "owner-1", TimeSpan.FromSeconds(30));

        // Act
        var acquired = await _repository.AcquireAsync(@lock).ConfigureAwait(false);

        // Assert
        acquired.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireAsync_WhenKeyAlreadyHeldAndNotExpired_ReturnsFalse()
    {
        // Arrange — first acquisition succeeds
        var first = new Lock("resource:held", "owner-1", TimeSpan.FromSeconds(30));
        await _repository.AcquireAsync(first).ConfigureAwait(false);

        // Act — second attempt by different owner
        var second = new Lock("resource:held", "owner-2", TimeSpan.FromSeconds(30));
        var acquired = await _repository.AcquireAsync(second).ConfigureAwait(false);

        // Assert
        acquired.Should().BeFalse();
    }

    [Fact]
    public async Task AcquireAsync_WhenExistingLockIsExpired_AllowsReacquisition()
    {
        // Arrange — store a lock that is already expired
        var expired = new Lock("resource:expired", "owner-1", TimeSpan.FromSeconds(30))
        {
            ExpiresAt = DateTime.UtcNow.AddSeconds(-1)
        };
        await _repository.AcquireAsync(expired).ConfigureAwait(false);

        // Act — new owner acquires the expired slot
        var fresh = new Lock("resource:expired", "owner-2", TimeSpan.FromSeconds(30));
        var acquired = await _repository.AcquireAsync(fresh).ConfigureAwait(false);

        // Assert
        acquired.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // GetByKeyAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByKeyAsync_WhenKeyDoesNotExist_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByKeyAsync("resource:ghost").ConfigureAwait(false);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByKeyAsync_WhenLockExists_ReturnsLock()
    {
        // Arrange
        var @lock = new Lock("resource:exists", "owner-1", TimeSpan.FromSeconds(30));
        await _repository.AcquireAsync(@lock).ConfigureAwait(false);

        // Act
        var retrieved = await _repository.GetByKeyAsync("resource:exists").ConfigureAwait(false);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.OwnerId.Should().Be("owner-1");
    }

    // -------------------------------------------------------------------------
    // ReleaseAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ReleaseAsync_WhenOwnerMatches_ReturnsTrueAndRemovesLock()
    {
        // Arrange
        var @lock = new Lock("resource:release", "owner-1", TimeSpan.FromSeconds(30));
        await _repository.AcquireAsync(@lock).ConfigureAwait(false);

        // Act
        var released = await _repository.ReleaseAsync("resource:release", "owner-1").ConfigureAwait(false);

        // Assert
        released.Should().BeTrue();
        var check = await _repository.GetByKeyAsync("resource:release").ConfigureAwait(false);
        check.Should().BeNull();
    }

    [Fact]
    public async Task ReleaseAsync_WhenOwnerMismatch_ReturnsFalse()
    {
        // Arrange
        var @lock = new Lock("resource:owned", "owner-1", TimeSpan.FromSeconds(30));
        await _repository.AcquireAsync(@lock).ConfigureAwait(false);

        // Act — wrong owner tries to release
        var released = await _repository.ReleaseAsync("resource:owned", "impostor").ConfigureAwait(false);

        // Assert
        released.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // RenewAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RenewAsync_WhenOwnerMatches_ExtendsDurationAndReturnsTrue()
    {
        // Arrange
        var @lock = new Lock("resource:renew", "owner-1", TimeSpan.FromSeconds(30))
        {
            Status = LockStatus.Held
        };
        await _repository.AcquireAsync(@lock).ConfigureAwait(false);

        // Act
        var renewed = await _repository.RenewAsync("resource:renew", "owner-1", TimeSpan.FromSeconds(60)).ConfigureAwait(false);

        // Assert
        renewed.Should().BeTrue();
    }

    [Fact]
    public async Task RenewAsync_WhenOwnerMismatch_ReturnsFalse()
    {
        // Arrange
        var @lock = new Lock("resource:no-renew", "owner-1", TimeSpan.FromSeconds(30))
        {
            Status = LockStatus.Held
        };
        await _repository.AcquireAsync(@lock).ConfigureAwait(false);

        // Act
        var renewed = await _repository.RenewAsync("resource:no-renew", "owner-2", TimeSpan.FromSeconds(60)).ConfigureAwait(false);

        // Assert
        renewed.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // ExistsAsync / DeleteExpiredLockAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExistsAsync_WhenActiveLockPresent_ReturnsTrue()
    {
        // Arrange
        var @lock = new Lock("resource:active", "owner-1", TimeSpan.FromSeconds(30));
        await _repository.AcquireAsync(@lock).ConfigureAwait(false);

        // Act
        var exists = await _repository.ExistsAsync("resource:active").ConfigureAwait(false);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteExpiredLockAsync_RemovesOnlyExpiredLocks()
    {
        // Arrange — one valid, one expired
        var valid = new Lock("resource:valid", "owner-1", TimeSpan.FromSeconds(30));
        var expired = new Lock("resource:dead", "owner-2", TimeSpan.FromSeconds(30))
        {
            ExpiresAt = DateTime.UtcNow.AddSeconds(-1)
        };
        await _repository.AcquireAsync(valid).ConfigureAwait(false);
        await _repository.AcquireAsync(expired).ConfigureAwait(false);

        // Act
        var deleted = await _repository.DeleteExpiredLockAsync().ConfigureAwait(false);

        // Assert
        deleted.Should().Be(1);
        (await _repository.ExistsAsync("resource:valid")).Should().BeTrue().ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------
    // GetAllActiveLockAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllActiveLockAsync_ReturnsOnlyNonExpiredLocks()
    {
        // Arrange
        await _repository.ClearAllAsync().ConfigureAwait(false);

        var active = new Lock("res:a", "owner-1", TimeSpan.FromSeconds(30));
        var stale = new Lock("res:b", "owner-2", TimeSpan.FromSeconds(30))
        {
            ExpiresAt = DateTime.UtcNow.AddSeconds(-5)
        };
        await _repository.AcquireAsync(active).ConfigureAwait(false);
        await _repository.AcquireAsync(stale).ConfigureAwait(false);

        // Act
        var result = (await _repository.GetAllActiveLockAsync()).ToList().ConfigureAwait(false);

        // Assert
        result.Should().HaveCount(1);
        result.Single().Key.Should().Be("res:a");
    }
}
