using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using SarmKadan.DistributedLock.Backends.PostgreSQL;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Xunit;

namespace dotnet_distributed_lock.Tests;

public class PostgresLockRepositoryTests
{
    private readonly Mock<ILogger<PostgresLockRepository>> _loggerMock = new();

    // Reserves a loopback port and releases it again, so nothing is listening there and connections are refused fast.
    private static string UnreachableConnectionString()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        return $"Host=127.0.0.1;Port={port};Database=locks;Username=test;Password={Guid.NewGuid():N};Timeout=1";
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new PostgresLockRepository("Host=localhost", null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankConnectionString_ThrowsArgumentException(string? connectionString)
    {
        var act = () => new PostgresLockRepository(connectionString!, _loggerMock.Object);

        act.Should().Throw<ArgumentException>().WithParameterName("connectionString");
    }

    [Fact]
    public void Constructor_ValidArguments_DoesNotConnectAndLogsInitialization()
    {
        // The port is unreachable, so this only succeeds if the constructor never opens a connection.
        var act = () => new PostgresLockRepository(UnreachableConnectionString(), _loggerMock.Object);

        act.Should().NotThrow();
        _loggerMock.Verify(l => l.Log(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_NoDatabaseWork_CompletesWithoutError()
    {
        var repository = new PostgresLockRepository(UnreachableConnectionString(), _loggerMock.Object);

        var act = async () => await repository.DisposeAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AcquireAsync_ServerUnreachable_LogsErrorAndRethrowsNpgsqlException()
    {
        var repository = new PostgresLockRepository(UnreachableConnectionString(), _loggerMock.Object);
        var @lock = new Lock("orders:42", "owner-a", TimeSpan.FromMinutes(1));

        var act = async () => await repository.AcquireAsync(@lock);

        await act.Should().ThrowAsync<NpgsqlException>();
        _loggerMock.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task DataOperations_ServerUnreachable_PropagateNpgsqlException()
    {
        var repository = new PostgresLockRepository(UnreachableConnectionString(), _loggerMock.Object);
        var operations = new Func<Task>[]
        {
            () => repository.GetByKeyAsync("orders:42"),
            () => repository.GetByKeyAndOwnerAsync("orders:42", "owner-a"),
            () => repository.UpdateAsync(new Lock("orders:42", "owner-a", TimeSpan.FromMinutes(1))),
            () => repository.RenewAsync("orders:42", "owner-a", TimeSpan.FromMinutes(1)),
            () => repository.ReleaseAsync("orders:42", "owner-a"),
            () => repository.ExistsAsync("orders:42"),
            () => repository.GetAllActiveLockAsync(),
            () => repository.GetByOwnerAsync("owner-a"),
        };

        foreach (var operation in operations)
        {
            await operation.Should().ThrowAsync<NpgsqlException>();
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task DeleteLockIfExpirationMatchesAsync_NullOrEmptyKey_ThrowsArgumentException(string? key)
    {
        var repository = new PostgresLockRepository(UnreachableConnectionString(), _loggerMock.Object);

        var act = async () => await repository.DeleteLockIfExpirationMatchesAsync(key!, DateTime.UtcNow);

        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("key");
    }
}
