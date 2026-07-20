#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =====================================================================

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Repository;
using SarmKadan.DistributedLock.Services;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Unit tests for <see cref="LockService.TryExtendAsync"/> which provides distributed lock lease extension functionality.
/// Tests cover successful extensions, failed extensions due to owner mismatch or missing lock, and error handling.
/// </summary>
public class LockServiceTryExtendTests : IDisposable
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
    /// In-memory repository for integration-style tests.
    /// </summary>
    private readonly InMemoryLockRepository _repository;

    /// <summary>
    /// Initializes a new instance of <see cref="LockServiceTryExtendTests"/> with mocked dependencies.
    /// </summary>
    public LockServiceTryExtendTests()
    {
        _repositoryMock = new Mock<ILockRepository>();
        _service = new LockService(_repositoryMock.Object, NullLogger<LockService>.Instance);
        _repository = new InMemoryLockRepository();
    }

    /// <summary>
    /// Disposes of resources.
    /// </summary>
    public void Dispose()
    {
        _repository.ClearAllAsync().GetAwaiter().GetResult();
    }

    // ---------------------------------------------------------------------
    // TryExtendAsync - Mock Repository Tests
    // ---------------------------------------------------------------------

    /// <summary>
    /// Tests that <see cref="LockService.TryExtendAsync"/> returns true when the repository successfully extends the lock.
    /// </summary>
    [Fact]
    public async Task TryExtendAsync_WhenRepositoryExtendsLock_ReturnsTrue()
    {
        // Arrange
        var lockKey = "resource:checkout";
        var ownerId = "worker-1";

        _repositoryMock
            .Setup(r => r.RenewAsync(lockKey, ownerId, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.TryExtendAsync(lockKey, ownerId, TimeSpan.FromSeconds(60));

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// Tests that <see cref="LockService.TryExtendAsync"/> returns false when the lock doesn't exist.
    /// </summary>
    [Fact]
    public async Task TryExtendAsync_WhenLockDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var lockKey = "nonexistent:lock";
        var ownerId = "worker-1";

        _repositoryMock
            .Setup(r => r.RenewAsync(lockKey, ownerId, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.TryExtendAsync(lockKey, ownerId, TimeSpan.FromSeconds(30));

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Tests that <see cref="LockService.TryExtendAsync"/> returns false when the owner doesn't match.
    /// </summary>
    [Fact]
    public async Task TryExtendAsync_WhenOwnerDoesNotMatch_ReturnsFalse()
    {
        // Arrange
        var lockKey = "resource:checkout";
        var ownerId = "worker-1";
        var otherOwnerId = "worker-2";

        _repositoryMock
            .Setup(r => r.RenewAsync(lockKey, otherOwnerId, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.TryExtendAsync(lockKey, otherOwnerId, TimeSpan.FromSeconds(30));

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Tests that <see cref="LockService.TryExtendAsync"/> returns false when the repository throws an exception.
    /// </summary>
    [Fact]
    public async Task TryExtendAsync_WhenRepositoryThrows_ReturnsFalse()
    {
        // Arrange
        var lockKey = "resource:checkout";
        var ownerId = "worker-1";

        _repositoryMock
            .Setup(r => r.RenewAsync(lockKey, ownerId, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("backend unavailable"));

        // Act
        var result = await _service.TryExtendAsync(lockKey, ownerId, TimeSpan.FromSeconds(30));

        // Assert
        result.Should().BeFalse();
    }

    // ---------------------------------------------------------------------
    // TryExtendAsync - In-Memory Repository Integration Tests
    // ---------------------------------------------------------------------

    /// <summary>
    /// Tests that <see cref="LockService.TryExtendAsync"/> successfully extends an existing lock.
    /// </summary>
    [Fact]
    public async Task TryExtendAsync_WithValidLock_ExtendsSuccessfully()
    {
        // Arrange
        var lockKey = "integration:test";
        var ownerId = "test-owner";
        var initialDuration = TimeSpan.FromSeconds(30);
        var extension = TimeSpan.FromSeconds(60);

        var initialLock = new Lock(lockKey, ownerId, initialDuration);
        await _repository.AcquireAsync(initialLock);

        var service = new LockService(_repository, NullLogger<LockService>.Instance);

        // Act
        var result = await service.TryExtendAsync(lockKey, ownerId, extension);

        // Assert
        result.Should().BeTrue();

        // Verify the lock was actually extended
        var updatedLock = await _repository.GetByKeyAsync(lockKey);
        updatedLock.Should().NotBeNull();
        updatedLock!.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.Add(extension), TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Tests that <see cref="LockService.TryExtendAsync"/> fails when the owner doesn't match.
    /// </summary>
    [Fact]
    public async Task TryExtendAsync_WithWrongOwner_Fails()
    {
        // Arrange
        var lockKey = "integration:wrongowner";
        var ownerId = "owner-1";
        var wrongOwnerId = "owner-2";
        var initialDuration = TimeSpan.FromSeconds(30);

        var initialLock = new Lock(lockKey, ownerId, initialDuration);
        await _repository.AcquireAsync(initialLock);

        var service = new LockService(_repository, NullLogger<LockService>.Instance);

        // Act
        var result = await service.TryExtendAsync(lockKey, wrongOwnerId, TimeSpan.FromSeconds(60));

        // Assert
        result.Should().BeFalse();

        // Verify the lock was not extended (expires at original time)
        var existingLock = await _repository.GetByKeyAsync(lockKey);
        existingLock.Should().NotBeNull();
        existingLock!.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.Add(initialDuration), TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Tests that <see cref="LockService.TryExtendAsync"/> fails when the lock doesn't exist.
    /// </summary>
    [Fact]
    public async Task TryExtendAsync_WithNonExistentLock_Fails()
    {
        // Arrange
        var lockKey = "integration:nonexistent";
        var ownerId = "test-owner";

        var service = new LockService(_repository, NullLogger<LockService>.Instance);

        // Act
        var result = await service.TryExtendAsync(lockKey, ownerId, TimeSpan.FromSeconds(30));

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Tests that <see cref="LockService.TryExtendAsync"/> fails when the lock is expired.
    /// </summary>
    [Fact]
    public async Task TryExtendAsync_WithExpiredLock_Fails()
    {
        // Arrange
        var lockKey = "integration:expired";
        var ownerId = "test-owner";
        var initialDuration = TimeSpan.FromSeconds(1);

        var initialLock = new Lock(lockKey, ownerId, initialDuration);
        await _repository.AcquireAsync(initialLock);

        // Wait for lock to expire
        await Task.Delay(TimeSpan.FromSeconds(2));

        var service = new LockService(_repository, NullLogger<LockService>.Instance);

        // Act
        var result = await service.TryExtendAsync(lockKey, ownerId, TimeSpan.FromSeconds(30));

        // Assert
        result.Should().BeFalse();
    }
}
