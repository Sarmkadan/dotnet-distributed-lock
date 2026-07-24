#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =====================================================================

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock.Backends.PostgreSQL;
using SarmKadan.DistributedLock.Backends.Redis;
using SarmKadan.DistributedLock.Backends.SQLite;
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
    /// Uses <see cref="DefaultLockRetryPolicy"/> with exponential backoff and jitter configured via
    /// <see cref="DistributedLockOptions"/> retry policy properties.
    /// </summary>
    /// <remarks>
    /// To supply a fully custom retry policy (e.g. Polly-based), use the overload that accepts
    /// <see cref="ILockRetryPolicy"/>:
    /// <code>
    /// services.AddDistributedLocking(myCustomPolicy, options => { ... });
    /// </code>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the configuration is invalid.</exception>
    public static IServiceCollection AddDistributedLocking(
        this IServiceCollection services,
        Action<DistributedLockOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new DistributedLockOptions();
        configureOptions?.Invoke(options);

        var validationErrors = options.Validate().ToList();
        if (validationErrors.Any())
        {
            throw new InvalidOperationException(
                $"Invalid distributed lock configuration: {string.Join(", ", validationErrors)}"
            );
        }

        var retryPolicy = new DefaultLockRetryPolicy(
            options.RetryPolicyMaxRetries,
            TimeSpan.FromMilliseconds(options.RetryPolicyInitialDelayMs),
            TimeSpan.FromMilliseconds(options.RetryPolicyMaxDelayMs),
            options.RetryPolicyJitterFactor
        );

        return services.AddDistributedLocking(retryPolicy, options);
    }

    /// <summary>
    /// Adds distributed lock services with a caller-supplied retry policy.
    /// Use this overload to plug in Polly or any other custom <see cref="ILockRetryPolicy"/>
    /// implementation instead of the built-in exponential-backoff default.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="retryPolicy">
    /// The retry policy to use for lock acquisition attempts. Must not be null.
    /// </param>
    /// <param name="configureOptions">Optional callback to configure additional options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="retryPolicy"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the configuration is invalid.</exception>
    /// <example>
    /// <code>
    /// // Plug in a custom Polly-based retry policy:
    /// services.AddDistributedLocking(new PollyLockRetryPolicy(), options => {
    ///     options.BackendType = BackendType.Redis;
    ///     options.ConnectionString = "localhost:6379";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddDistributedLocking(
        this IServiceCollection services,
        ILockRetryPolicy retryPolicy,
        Action<DistributedLockOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(retryPolicy);

        var options = new DistributedLockOptions();
        configureOptions?.Invoke(options);

        var validationErrors = options.Validate().ToList();
        if (validationErrors.Any())
        {
            throw new InvalidOperationException(
                $"Invalid distributed lock configuration: {string.Join(", ", validationErrors)}"
            );
        }

        return services.AddDistributedLocking(retryPolicy, options);
    }

    private static IServiceCollection AddDistributedLocking(
        this IServiceCollection services,
        ILockRetryPolicy retryPolicy,
        DistributedLockOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton(retryPolicy);
        services.AddRepository(options.BackendType, options.ConnectionString);
        services.AddScoped<ILockService, LockService>();
        services.AddSingleton<FencingTokenService>();
        services.AddSingleton<LockMonitor>();
        services.AddSingleton<Events.ILockEventBus, Events.InMemoryLockEventBus>();
        services.AddSingleton<IDeadlockDetector, DeadlockDetector>();
        services.AddSingleton<IMetricsStore, InMemoryMetricsStore>();

        return services;
    }

    /// <summary>
    /// Registers the appropriate lock repository based on the backend type.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="backendType">The backend type to use for lock storage.</param>
    /// <param name="connectionString">The connection string for the backend storage.</param>
    /// <returns>The configured service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="connectionString"/> is null or empty.</exception>
    /// <exception cref="NotSupportedException">Thrown when the backend type is not supported.</exception>
    private static IServiceCollection AddRepository(
        this IServiceCollection services,
        BackendType backendType,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(connectionString, nameof(connectionString));

        return backendType switch
        {
            BackendType.InMemory => services.AddSingleton<ILockRepository, InMemoryLockRepository>(),
            BackendType.Redis => services.AddRedisRepository(connectionString),
            BackendType.SQLite => services.AddSqliteRepository(connectionString),
            BackendType.PostgreSQL => services.AddPostgresRepository(connectionString),
            _ => throw new NotSupportedException($"Backend type {backendType} is not supported")
        };
    }

    private static IServiceCollection AddRedisRepository(this IServiceCollection services, string connectionString)
    {
        return services.AddSingleton<ILockRepository>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<RedisLockRepository>>();
            return new RedisLockRepository(connectionString, logger);
        });
    }

    private static IServiceCollection AddSqliteRepository(this IServiceCollection services, string connectionString)
    {
        return services.AddSingleton<ILockRepository>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<SqliteLockRepository>>();
            return new SqliteLockRepository(connectionString, logger);
        });
    }

    private static IServiceCollection AddPostgresRepository(this IServiceCollection services, string connectionString)
    {
        return services.AddSingleton<ILockRepository>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<PostgresLockRepository>>();
            return new PostgresLockRepository(connectionString, logger);
        });
    }
}
