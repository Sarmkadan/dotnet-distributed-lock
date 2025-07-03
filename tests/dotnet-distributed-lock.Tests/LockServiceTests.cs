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

/// <summary>
/// Unit tests for <see cref="LockService"/> which provides distributed lock management functionality.
/// Tests cover constructor validation, lock acquisition, renewal, release, status checks, and metrics collection.
/// </summary>
public class LockServiceTests
{
	/// <summary>
	/// Mock repository used to simulate lock storage operations.
	/// </summary>
	private readonly Mock<ILockRepository> _repositoryMock;

	/// <summary>
	/// Instance of the service under test.
	/// </summary>
	private readonly LockService _service;


	/// <summary>
	/// Initializes a new instance of <see cref="LockServiceTests"/> with mocked dependencies.
	/// </summary>
	public LockServiceTests()
	{
		_repositoryMock = new Mock<ILockRepository>();
		_service = new LockService(_repositoryMock.Object, NullLogger<LockService>.Instance);
	}

	// -------------------------------------------------------------------------
	// Constructor guards
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that the constructor throws <see cref="ArgumentNullException"/> when repository is null.
	/// </summary>
	[Fact]
	public void Constructor_WithNullRepository_ThrowsArgumentNullException()
	{
		// Act
		var act = () => new LockService(null!, NullLogger<LockService>.Instance);

		// Assert
		act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
	}

	/// <summary>
	/// Tests that the constructor throws <see cref="ArgumentNullException"/> when logger is null.
	/// </summary>
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

	/// <summary>
	/// Tests that <see cref="LockService.TryAcquireAsync"/> returns true with a valid lock when the repository successfully grants the lock.
	/// </summary>
	[Fact]
	public async Task TryAcquireAsync_WhenRepositoryGrantsLock_ReturnsTrueWithLock()
	{
		// Arrange
		_repositoryMock
			.Setup(r => r.AcquireAsync(It.IsAny<Lock>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(true);

		// Act
		var (success, @lock, errorMessage) =
			await _service.TryAcquireAsync("resource:checkout", "worker-1", TimeSpan.FromSeconds(30));

		// Assert
		success.Should().BeTrue();
		@lock.Should().NotBeNull();
		@lock!.Key.Should().Be("resource:checkout");
		@lock.OwnerId.Should().Be("worker-1");
		@lock.Status.Should().Be(LockStatus.Held);
		errorMessage.Should().BeNull();
	}

	/// <summary>
	/// Tests that <see cref="LockService.TryAcquireAsync"/> returns false with an error message when the repository denies the lock.
	/// </summary>
	[Fact]
	public async Task TryAcquireAsync_WhenRepositoryDeniesLock_ReturnsFalseWithErrorMessage()
	{
		// Arrange — another owner holds the lock
		_repositoryMock
			.Setup(r => r.AcquireAsync(It.IsAny<Lock>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(false);

		// Act
		var (success, @lock, errorMessage) =
			await _service.TryAcquireAsync("resource:checkout", "worker-2");

		// Assert
		success.Should().BeFalse();
		@lock.Should().BeNull();
		errorMessage.Should().NotBeNullOrWhiteSpace();
	}

	/// <summary>
	/// Tests that <see cref="LockService.TryAcquireAsync"/> returns false with an exception message when the repository throws an exception.
	/// </summary>
	[Fact]
	public async Task TryAcquireAsync_WhenRepositoryThrows_ReturnsFalseWithExceptionMessage()
	{
		// Arrange
		_repositoryMock
			.Setup(r => r.AcquireAsync(It.IsAny<Lock>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("backend unavailable"));

		// Act
		var (success, @lock, errorMessage) =
			await _service.TryAcquireAsync("resource:checkout", "worker-1");

		// Assert
		success.Should().BeFalse();
		@lock.Should().BeNull();
		errorMessage.Should().Contain("backend unavailable");
	}

	// -------------------------------------------------------------------------
	// RenewAsync
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that <see cref="LockService.RenewAsync"/> returns true when the repository successfully renews the lock.
	/// </summary>
	[Fact]
	public async Task RenewAsync_WhenRepositoryRenews_ReturnsTrue()
	{
		// Arrange
		_repositoryMock
			.Setup(r => r.RenewAsync("resource:orders", "worker-1", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(true);

		// Act
		var renewed = await _service.RenewAsync("resource:orders", "worker-1");

		// Assert
		renewed.Should().BeTrue();
	}

	/// <summary>
	/// Tests that <see cref="LockService.RenewAsync"/> returns false when the repository indicates the lock renewal failed.
	/// </summary>
	[Fact]
	public async Task RenewAsync_WhenRepositoryReturnsFalse_ReturnsFalse()
	{
		// Arrange — lock no longer owned by this worker
		_repositoryMock
			.Setup(r => r.RenewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(false);

		// Act
		var renewed = await _service.RenewAsync("resource:orders", "worker-99");

		// Assert
		renewed.Should().BeFalse();
	}

	/// <summary>
	/// Tests that <see cref="LockService.RenewAsync"/> returns false when the repository throws an exception.
	/// </summary>
	[Fact]
	public async Task RenewAsync_WhenRepositoryThrows_ReturnsFalse()
	{
		// Arrange
		_repositoryMock
			.Setup(r => r.RenewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new Exception("network timeout"));

		// Act
		var renewed = await _service.RenewAsync("resource:orders", "worker-1");

		// Assert
		renewed.Should().BeFalse();
	}

	// -------------------------------------------------------------------------
	// ReleaseAsync
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that <see cref="LockService.ReleaseAsync"/> returns false when the lock does not exist.
	/// </summary>
	[Fact]
	public async Task ReleaseAsync_WhenLockNotFound_ReturnsFalse()
	{
		// Arrange — GetByKeyAsync returns null (lock does not exist)
		_repositoryMock
			.Setup(r => r.GetByKeyAsync("resource:missing", It.IsAny<CancellationToken>()))
			.ReturnsAsync((Lock?)null);

		// Act
		var released = await _service.ReleaseAsync("resource:missing", "worker-1");

		// Assert
		released.Should().BeFalse();
		_repositoryMock.Verify(
			r => r.ReleaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
			Times.Never);
	}

	/// <summary>
	/// Tests that <see cref="LockService.ReleaseAsync"/> returns true when the lock exists and is successfully released.
	/// </summary>
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
		var released = await _service.ReleaseAsync("resource:payments", "worker-1");

		// Assert
		released.Should().BeTrue();
		_repositoryMock.Verify(
			r => r.ReleaseAsync("resource:payments", "worker-1", It.IsAny<CancellationToken>()),
			Times.Once);
	}

	// -------------------------------------------------------------------------
	// IsLockedAsync
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that <see cref="LockService.IsLockedAsync"/> returns true when the repository indicates the resource is locked.
	/// </summary>
	[Fact]
	public async Task IsLockedAsync_WhenRepositoryReturnsTrue_ReturnsTrue()
	{
		// Arrange
		_repositoryMock
			.Setup(r => r.ExistsAsync("resource:active", It.IsAny<CancellationToken>()))
			.ReturnsAsync(true);

		// Act
		var isLocked = await _service.IsLockedAsync("resource:active");

		// Assert
		isLocked.Should().BeTrue();
	}

	/// <summary>
	/// Tests that <see cref="LockService.IsLockedAsync"/> returns false when the repository throws an exception.
	/// </summary>
	[Fact]
	public async Task IsLockedAsync_WhenRepositoryThrows_ReturnsFalse()
	{
		// Arrange
		_repositoryMock
			.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new Exception("connection refused"));

		// Act
		var isLocked = await _service.IsLockedAsync("resource:flaky");

		// Assert — service should swallow the exception and default to false
		isLocked.Should().BeFalse();
	}

	// -------------------------------------------------------------------------
	// GetAllActiveLockAsync
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that <see cref="LockService.GetAllActiveLockAsync"/> returns the active locks when the repository provides them.
	/// </summary>
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
		var result = await _service.GetAllActiveLockAsync();

		// Assert
		result.Should().HaveCount(2);
	}

	/// <summary>
	/// Tests that <see cref="LockService.GetAllActiveLockAsync"/> returns an empty enumerable when the repository throws an exception.
	/// </summary>
	[Fact]
	public async Task GetAllActiveLockAsync_WhenRepositoryThrows_ReturnsEmptyEnumerable()
	{
		// Arrange
		_repositoryMock
			.Setup(r => r.GetAllActiveLockAsync(It.IsAny<CancellationToken>()))
			.ThrowsAsync(new Exception("storage error"));

		// Act
		var result = await _service.GetAllActiveLockAsync();

		// Assert
		result.Should().BeEmpty();
	}

	// -------------------------------------------------------------------------
	// GetMetrics
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that <see cref="LockService.GetMetrics"/> returns metrics with successful acquisition count after a lock has been acquired.
	/// </summary>
	[Fact]
	public async Task GetMetrics_AfterSuccessfulAcquisition_ReflectsAcquisitionCount()
	{
		// Arrange
		_repositoryMock
			.Setup(r => r.AcquireAsync(It.IsAny<Lock>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(true);

		await _service.TryAcquireAsync("resource:metrics-test", "owner-1", TimeSpan.FromSeconds(10));

		// Act
		var metrics = _service.GetMetrics();

		// Assert
		metrics.Should().NotBeNull();
		metrics.SuccessfulAcquisitions.Should().BeGreaterThan(0);
	}
}

/// <summary>
/// Unit tests for <see cref="InMemoryLockRepository"/> which provides an in-memory implementation of <see cref="ILockRepository"/>.
/// Tests cover lock acquisition, retrieval, release, renewal, existence checks, and cleanup operations.
/// </summary>
public class InMemoryLockRepositoryTests
{
	/// <summary>
	/// Instance of the in-memory repository under test.
	/// </summary>
	private readonly InMemoryLockRepository _repository = new();

	// -------------------------------------------------------------------------
	// AcquireAsync
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that <see cref="InMemoryLockRepository.AcquireAsync"/> returns true when acquiring a lock for a key that does not exist.
	/// </summary>
	[Fact]
	public async Task AcquireAsync_WhenKeyDoesNotExist_ReturnsTrue()
	{
		// Arrange
		var @lock = new Lock("resource:new", "owner-1", TimeSpan.FromSeconds(30));

		// Act
		var acquired = await _repository.AcquireAsync(@lock);

		// Assert
		acquired.Should().BeTrue();
	}

	/// <summary>
	/// Tests that <see cref="InMemoryLockRepository.AcquireAsync"/> returns false when the key is already held by another owner and the lock has not expired.
	/// </summary>
	[Fact]
	public async Task AcquireAsync_WhenKeyAlreadyHeldAndNotExpired_ReturnsFalse()
	{
		// Arrange — first acquisition succeeds
		var first = new Lock("resource:held", "owner-1", TimeSpan.FromSeconds(30));
		await _repository.AcquireAsync(first);

		// Act — second attempt by different owner
		var second = new Lock("resource:held", "owner-2", TimeSpan.FromSeconds(30));
		var acquired = await _repository.AcquireAsync(second);

		// Assert
		acquired.Should().BeFalse();
	}

	/// <summary>
	/// Tests that <see cref="InMemoryLockRepository.AcquireAsync"/> allows reacquisition when the existing lock has expired.
	/// </summary>
	[Fact]
	public async Task AcquireAsync_WhenExistingLockIsExpired_AllowsReacquisition()
	{
		// Arrange — store a lock that is already expired
		var expired = new Lock("resource:expired", "owner-1", TimeSpan.FromSeconds(30))
		{
			ExpiresAt = DateTime.UtcNow.AddSeconds(-1)
		};
		await _repository.AcquireAsync(expired);

		// Act — new owner acquires the expired slot
		var fresh = new Lock("resource:expired", "owner-2", TimeSpan.FromSeconds(30));
		var acquired = await _repository.AcquireAsync(fresh);

		// Assert
		acquired.Should().BeTrue();
	}

	// -------------------------------------------------------------------------
	// GetByKeyAsync
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that <see cref="InMemoryLockRepository.GetByKeyAsync"/> returns null when the key does not exist.
	/// </summary>
	[Fact]
	public async Task GetByKeyAsync_WhenKeyDoesNotExist_ReturnsNull()
	{
		// Act
		var result = await _repository.GetByKeyAsync("resource:ghost");

		// Assert
		result.Should().BeNull();
	}

	/// <summary>
	/// Tests that <see cref="InMemoryLockRepository.GetByKeyAsync"/> returns the lock when it exists.
	/// </summary>
	[Fact]
	public async Task GetByKeyAsync_WhenLockExists_ReturnsLock()
	{
		// Arrange
		var @lock = new Lock("resource:exists", "owner-1", TimeSpan.FromSeconds(30));
		await _repository.AcquireAsync(@lock);

		// Act
		var retrieved = await _repository.GetByKeyAsync("resource:exists");

		// Assert
		retrieved.Should().NotBeNull();
		retrieved!.OwnerId.Should().Be("owner-1");
	}

	// -------------------------------------------------------------------------
	// ReleaseAsync
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that <see cref="InMemoryLockRepository.ReleaseAsync"/> returns true and removes the lock when the owner matches.
	/// </summary>
	[Fact]
	public async Task ReleaseAsync_WhenOwnerMatches_ReturnsTrueAndRemovesLock()
	{
		// Arrange
		var @lock = new Lock("resource:release", "owner-1", TimeSpan.FromSeconds(30));
		await _repository.AcquireAsync(@lock);

		// Act
		var released = await _repository.ReleaseAsync("resource:release", "owner-1");

		// Assert
		released.Should().BeTrue();
		var check = await _repository.GetByKeyAsync("resource:release");
		check.Should().BeNull();
	}

	/// <summary>
	/// Tests that <see cref="InMemoryLockRepository.ReleaseAsync"/> returns false when the owner does not match.
	/// </summary>
	[Fact]
	public async Task ReleaseAsync_WhenOwnerMismatch_ReturnsFalse()
	{
		// Arrange
		var @lock = new Lock("resource:owned", "owner-1", TimeSpan.FromSeconds(30));
		await _repository.AcquireAsync(@lock);

		// Act — wrong owner tries to release
		var released = await _repository.ReleaseAsync("resource:owned", "impostor");

		// Assert
		released.Should().BeFalse();
	}

	// -------------------------------------------------------------------------
	// RenewAsync
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that <see cref="InMemoryLockRepository.RenewAsync"/> extends the lock duration and returns true when the owner matches.
	/// </summary>
	[Fact]
	public async Task RenewAsync_WhenOwnerMatches_ExtendsDurationAndReturnsTrue()
	{
		// Arrange
		var @lock = new Lock("resource:renew", "owner-1", TimeSpan.FromSeconds(30))
		{
			Status = LockStatus.Held
		};
		await _repository.AcquireAsync(@lock);

		// Act
		var renewed = await _repository.RenewAsync("resource:renew", "owner-1", TimeSpan.FromSeconds(60));

		// Assert
		renewed.Should().BeTrue();
	}

	/// <summary>
	/// Tests that <see cref="InMemoryLockRepository.RenewAsync"/> returns false when the owner does not match.
	/// </summary>
	[Fact]
	public async Task RenewAsync_WhenOwnerMismatch_ReturnsFalse()
	{
		// Arrange
		var @lock = new Lock("resource:no-renew", "owner-1", TimeSpan.FromSeconds(30))
		{
			Status = LockStatus.Held
		};
		await _repository.AcquireAsync(@lock);

		// Act
		var renewed = await _repository.RenewAsync("resource:no-renew", "owner-2", TimeSpan.FromSeconds(60));

		// Assert
		renewed.Should().BeFalse();
	}

	// -------------------------------------------------------------------------
	// ExistsAsync / DeleteExpiredLockAsync
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that <see cref="InMemoryLockRepository.ExistsAsync"/> returns true when an active lock is present.
	/// </summary>
	[Fact]
	public async Task ExistsAsync_WhenActiveLockPresent_ReturnsTrue()
	{
		// Arrange
		var @lock = new Lock("resource:active", "owner-1", TimeSpan.FromSeconds(30));
		await _repository.AcquireAsync(@lock);

		// Act
		var exists = await _repository.ExistsAsync("resource:active");

		// Assert
		exists.Should().BeTrue();
	}

	/// <summary>
	/// Tests that <see cref="InMemoryLockRepository.DeleteExpiredLockAsync"/> removes only expired locks.
	/// </summary>
	[Fact]
	public async Task DeleteExpiredLockAsync_RemovesOnlyExpiredLocks()
	{
		// Arrange — one valid, one expired
		var valid = new Lock("resource:valid", "owner-1", TimeSpan.FromSeconds(30));
		var expired = new Lock("resource:dead", "owner-2", TimeSpan.FromSeconds(30))
		{
			ExpiresAt = DateTime.UtcNow.AddSeconds(-1)
		};
		await _repository.AcquireAsync(valid);
		await _repository.AcquireAsync(expired);

		// Act
		var deleted = await _repository.DeleteExpiredLockAsync();

		// Assert
		deleted.Should().Be(1);
		(await _repository.ExistsAsync("resource:valid")).Should().BeTrue();
	}

	// -------------------------------------------------------------------------
	// GetAllActiveLockAsync
	// -------------------------------------------------------------------------

	/// <summary>
	/// Tests that <see cref="InMemoryLockRepository.GetAllActiveLockAsync"/> returns only non-expired locks.
	/// </summary>
	[Fact]
	public async Task GetAllActiveLockAsync_ReturnsOnlyNonExpiredLocks()
	{
		// Arrange
		await _repository.ClearAllAsync();

		var active = new Lock("res:a", "owner-1", TimeSpan.FromSeconds(30));
		var stale = new Lock("res:b", "owner-2", TimeSpan.FromSeconds(30))
		{
			ExpiresAt = DateTime.UtcNow.AddSeconds(-5)
		};
		await _repository.AcquireAsync(active);
		await _repository.AcquireAsync(stale);

		// Act
		var result = (await _repository.GetAllActiveLockAsync()).ToList();

		// Assert
		result.Should().HaveCount(1);
		result.Single().Key.Should().Be("res:a");
	}
}