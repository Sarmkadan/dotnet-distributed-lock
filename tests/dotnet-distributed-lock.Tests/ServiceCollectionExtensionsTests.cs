using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SarmKadan.DistributedLock.Backends.PostgreSQL;
using SarmKadan.DistributedLock.Backends.Redis;
using SarmKadan.DistributedLock.Backends.SQLite;
using SarmKadan.DistributedLock.Configuration;
using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Repository;
using SarmKadan.DistributedLock.Services;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

public class ServiceCollectionExtensionsTests
{
    // Port 1 is never open locally. abortConnect=false lets the multiplexer be constructed without a live server,
    // so the Redis case verifies wiring only and never talks to a real Redis instance.
    private const string UnreachableRedis = "127.0.0.1:1,abortConnect=false,connectTimeout=1000";

    // No credentials: the PostgreSQL and SQLite repositories do not open connections in their constructors.
    private const string PostgresConnection = "Host=localhost;Database=locks";
    private const string SqliteConnection = "Data Source=:memory:";

    private static IServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        return services;
    }

    [Theory]
    [InlineData(BackendType.InMemory, "", typeof(InMemoryLockRepository))]
    [InlineData(BackendType.Redis, UnreachableRedis, typeof(RedisLockRepository))]
    [InlineData(BackendType.SQLite, SqliteConnection, typeof(SqliteLockRepository))]
    [InlineData(BackendType.PostgreSQL, PostgresConnection, typeof(PostgresLockRepository))]
    public async Task AddDistributedLocking_ResolvesRepositoryForBackend(
        BackendType backendType,
        string connectionString,
        Type expectedRepositoryType)
    {
        // Arrange
        var services = CreateServices();
        services.AddDistributedLocking(options =>
        {
            options.BackendType = backendType;
            options.ConnectionString = connectionString;
        });

        // Act
        await using var provider = services.BuildServiceProvider();
        var repository = provider.GetRequiredService<ILockRepository>();

        // Assert
        repository.Should().BeOfType(expectedRepositoryType);
    }

    [Fact]
    public async Task AddDistributedLocking_ResolvesLockServiceWithRegisteredRepository()
    {
        // Arrange
        var services = CreateServices();
        services.AddDistributedLocking(options => options.BackendType = BackendType.InMemory);

        // Act
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var lockService = scope.ServiceProvider.GetRequiredService<ILockService>();
        var acquired = await lockService.TryAcquireAsync("orders", "owner-1");

        // Assert
        lockService.Should().BeOfType<LockService>();
        acquired.Success.Should().BeTrue();

        // The lock service must share the singleton repository the container exposes.
        var repository = provider.GetRequiredService<ILockRepository>();
        (await repository.ExistsAsync("orders")).Should().BeTrue();
    }

    [Fact]
    public async Task AddDistributedLocking_WithCustomRetryPolicy_ResolvesSuppliedPolicy()
    {
        // Arrange
        var services = CreateServices();
        var retryPolicy = new DefaultLockRetryPolicy();
        services.AddDistributedLocking(retryPolicy, options => options.BackendType = BackendType.InMemory);

        // Act
        await using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<ILockRetryPolicy>();

        // Assert
        resolved.Should().BeSameAs(retryPolicy);
    }

    [Theory]
    [InlineData(BackendType.Redis)]
    [InlineData(BackendType.SQLite)]
    [InlineData(BackendType.PostgreSQL)]
    [InlineData(BackendType.Redis, "   ")]
    [InlineData(BackendType.SQLite, "")]
    [InlineData(BackendType.PostgreSQL, " ")]
    public void AddDistributedLocking_MissingConnectionString_ThrowsActionableException(
        BackendType backendType,
        string? connectionString = null)
    {
        // Arrange
        var services = CreateServices();

        // Act
        var act = () => services.AddDistributedLocking(options =>
        {
            options.BackendType = backendType;
            options.ConnectionString = connectionString ?? string.Empty;
        });

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Invalid distributed lock configuration*Connection string is required*");
    }

    [Fact]
    public void AddDistributedLocking_MissingConnectionString_RegistersNothing()
    {
        // Arrange
        var services = CreateServices();
        var initialCount = services.Count;

        // Act
        var act = () => services.AddDistributedLocking(options => options.BackendType = BackendType.Redis);

        // Assert
        act.Should().Throw<InvalidOperationException>();
        services.Count.Should().Be(initialCount);
    }

    [Fact]
    public void AddDistributedLocking_CalledTwice_ThrowsInsteadOfOverwriting()
    {
        // Arrange
        var services = CreateServices();
        services.AddDistributedLocking(options => options.BackendType = BackendType.InMemory);

        // Act
        var act = () => services.AddDistributedLocking(options =>
        {
            options.BackendType = BackendType.SQLite;
            options.ConnectionString = SqliteConnection;
        });

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already registered*");
        services.Count(d => d.ServiceType == typeof(ILockRepository)).Should().Be(1);
        services.Count(d => d.ServiceType == typeof(DistributedLockOptions)).Should().Be(1);
    }

    [Fact]
    public void AddDistributedLocking_CalledTwiceWithDifferentOverloads_Throws()
    {
        // Arrange
        var services = CreateServices();
        services.AddDistributedLocking(new DefaultLockRetryPolicy(), options => options.BackendType = BackendType.InMemory);

        // Act
        var act = () => services.AddDistributedLocking(options => options.BackendType = BackendType.InMemory);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already registered*");
    }

    [Fact]
    public void AddDistributedLocking_NullServices_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection? services = null;

        // Act
        var act = () => services!.AddDistributedLocking(options => options.BackendType = BackendType.InMemory);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddDistributedLocking_NullRetryPolicy_ThrowsArgumentNullException()
    {
        // Arrange
        var services = CreateServices();

        // Act
        var act = () => services.AddDistributedLocking((ILockRetryPolicy)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("retryPolicy");
    }
}
