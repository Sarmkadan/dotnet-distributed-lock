using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Repository;
using SarmKadan.DistributedLock.Services;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Provides additional tests for <see cref="LockService"/> focusing on edge cases and error handling.
/// </summary>
public class LockServiceAdditionalTests
{
    private readonly Mock<ILockRepository> _repositoryMock;
    private readonly LockService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="LockServiceAdditionalTests"/> class with mocked dependencies.
    /// </summary>
    public LockServiceAdditionalTests()
    {
        _repositoryMock = new Mock<ILockRepository>();
        _service = new LockService(_repositoryMock.Object, NullLogger<LockService>.Instance);
    }

    /// <summary>
    /// Verifies that <see cref="LockService.ReleaseAsync(string, string, CancellationToken)"/> returns false when the lock does not exist in the repository.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task ReleaseAsync_WhenLockDoesNotExist_ReturnsFalse()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lock?)null);

        // Act
        var released = await _service.ReleaseAsync("non-existent-lock", "owner-1");

        // Assert
        released.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that <see cref="LockService.ReleaseAsync(string, string, CancellationToken)"/> returns false when the repository throws an exception.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task ReleaseAsync_WhenRepositoryThrows_ReturnsFalse()
    {
        // Arrange
        var lockObj = new Lock("resource:1", "owner-1", TimeSpan.FromSeconds(10));
        _repositoryMock
            .Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockObj);
        _repositoryMock
            .Setup(r => r.ReleaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var released = await _service.ReleaseAsync("resource:1", "owner-1");

        // Assert
        released.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that <see cref="LockService.RenewAsync(string, string, TimeSpan, CancellationToken)"/> returns false when the lock does not exist in the repository.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task RenewAsync_WhenLockDoesNotExist_ReturnsFalse()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.RenewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var renewed = await _service.RenewAsync("non-existent-lock", "owner-1");

        // Assert
        renewed.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that <see cref="LockService.GetLockAsync(string, CancellationToken)"/> returns null when the lock does not exist in the repository.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task GetLockAsync_WhenLockDoesNotExist_ReturnsNull()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lock?)null);

        // Act
        var @lock = await _service.GetLockAsync("non-existent-lock");

        // Assert
        @lock.Should().BeNull();
    }

    /// <summary>
    /// Verifies that <see cref="LockService.IsLockedAsync(string, CancellationToken)"/> returns false when the repository throws an exception.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task IsLockedAsync_WhenRepositoryThrows_ReturnsFalse()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var isLocked = await _service.IsLockedAsync("resource:1");

        // Assert
        isLocked.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that <see cref="LockService.GetAllActiveLockAsync(CancellationToken)"/> returns an empty enumerable when the repository throws an exception.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task GetAllActiveLockAsync_WhenRepositoryThrows_ReturnsEmptyEnumerable()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetAllActiveLockAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var locks = await _service.GetAllActiveLockAsync();

        // Assert
        locks.Should().BeEmpty();
    }
}
