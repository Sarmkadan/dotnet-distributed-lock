// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Repository;
using SarmKadan.DistributedLock.Services;

namespace SarmKadan.DistributedLock.Configuration;

/// <summary>
/// Extension methods for configuring distributed lock services in the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds distributed lock services to the dependency injection container.
    /// </summary>
    public static IServiceCollection AddDistributedLocking(
        this IServiceCollection services,
        Action<DistributedLockOptions>? configureOptions = null)
    {
        var options = new DistributedLockOptions();
        configureOptions?.Invoke(options);

        var validationErrors = options.Validate().ToList();
        if (validationErrors.Any())
        {
            throw new InvalidOperationException(
                $"Invalid distributed lock configuration: {string.Join(", ", validationErrors)}"
            );
        }

        services.AddSingleton(options);
        services.AddRepository(options.BackendType, options.ConnectionString);
        services.AddScoped<ILockService, LockService>();
        services.AddSingleton<FencingTokenService>();
        services.AddSingleton<LockMonitor>();

        return services;
    }

    /// <summary>
    /// Registers the appropriate lock repository based on the backend type.
    /// </summary>
    private static IServiceCollection AddRepository(
        this IServiceCollection services,
        BackendType backendType,
        string connectionString)
    {
        return backendType switch
        {
            BackendType.InMemory => services.AddSingleton<ILockRepository, InMemoryLockRepository>(),
            BackendType.Redis => services.AddRedisRepository(connectionString),
            BackendType.SQLite => services.AddSqliteRepository(connectionString),
            BackendType.PostgreSQL => services.AddPostgresRepository(connectionString),
            _ => throw new NotSupportedException($"Backend type {backendType} is not supported")
        };
    }

    // Placeholder methods for backend-specific repositories
    private static IServiceCollection AddRedisRepository(this IServiceCollection services, string connectionString)
    {
        // Implementation for Redis repository registration
        throw new NotImplementedException("Redis repository not yet implemented");
    }

    private static IServiceCollection AddSqliteRepository(this IServiceCollection services, string connectionString)
    {
        // Implementation for SQLite repository registration
        throw new NotImplementedException("SQLite repository not yet implemented");
    }

    private static IServiceCollection AddPostgresRepository(this IServiceCollection services, string connectionString)
    {
        // Implementation for PostgreSQL repository registration
        throw new NotImplementedException("PostgreSQL repository not yet implemented");
    }
}
