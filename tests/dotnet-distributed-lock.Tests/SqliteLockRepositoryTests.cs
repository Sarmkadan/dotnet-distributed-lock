using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock.Backends.SQLite;
using SarmKadan.DistributedLock.Models;
using System;
using System.Threading.Tasks;
using System.IO;

namespace dotnet_distributed_lock.Tests
{
    public class SqliteLockRepositoryTests : IDisposable
    {
        private readonly string _dbPath = $"test_{Guid.NewGuid()}.db";
        private readonly string _connectionString;
        private readonly Mock<ILogger<SqliteLockRepository>> _loggerMock = new();

        public SqliteLockRepositoryTests()
        {
            _connectionString = $"Data Source={_dbPath}";
        }

        public void Dispose()
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }

        [Fact]
        public async Task Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new SqliteLockRepository(_connectionString, null!));
        }

        [Fact]
        public async Task AcquireAsync_AddsLock_WhenValid()
        {
            var repo = new SqliteLockRepository(_connectionString, _loggerMock.Object);
            var lockObj = new Lock("key1", "owner1", TimeSpan.FromSeconds(30));

            var result = await repo.AcquireAsync(lockObj);

            Assert.True(result);
            var retrieved = await repo.GetByKeyAsync("key1");
            Assert.NotNull(retrieved);
            Assert.Equal("owner1", retrieved!.OwnerId);
        }

        [Fact]
        public async Task GetByKeyAsync_ReturnsNull_WhenNotExists()
        {
            var repo = new SqliteLockRepository(_connectionString, _loggerMock.Object);

            var result = await repo.GetByKeyAsync("nonexistent");

            Assert.Null(result);
        }

        [Fact]
        public async Task UpdateAsync_UpdatesLock_WhenExists()
        {
            var repo = new SqliteLockRepository(_connectionString, _loggerMock.Object);
            var lockObj = new Lock("key1", "owner1", TimeSpan.FromSeconds(30));
            await repo.AcquireAsync(lockObj);

            lockObj.Status = SarmKadan.DistributedLock.Enums.LockStatus.Released;
            var result = await repo.UpdateAsync(lockObj);

            Assert.True(result);
            var updated = await repo.GetByKeyAsync("key1");
            Assert.Equal(SarmKadan.DistributedLock.Enums.LockStatus.Released, updated!.Status);
        }

        [Fact]
        public async Task ReleaseAsync_RemovesLock_WhenExists()
        {
            var repo = new SqliteLockRepository(_connectionString, _loggerMock.Object);
            var lockObj = new Lock("key1", "owner1", TimeSpan.FromSeconds(30));
            await repo.AcquireAsync(lockObj);

            var result = await repo.ReleaseAsync("key1", "owner1");

            Assert.True(result);
            var exists = await repo.ExistsAsync("key1");
            Assert.False(exists);
        }

        [Fact]
        public async Task GetAllActiveLockAsync_ReturnsLocks_WhenExist()
        {
            var repo = new SqliteLockRepository(_connectionString, _loggerMock.Object);
            await repo.AcquireAsync(new Lock("key1", "owner1", TimeSpan.FromSeconds(30)));
            await repo.AcquireAsync(new Lock("key2", "owner1", TimeSpan.FromSeconds(30)));

            var locks = await repo.GetAllActiveLockAsync();

            Assert.NotEmpty(locks);
        }
    }
}
