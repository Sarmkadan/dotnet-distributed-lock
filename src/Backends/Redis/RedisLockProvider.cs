#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SarmKadan.DistributedLock.Configuration;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Repository;
using SarmKadan.DistributedLock.Services;

namespace SarmKadan.DistributedLock.Backends.Redis;

/// <summary>
/// High-level lock provider backed by Redis, wrapping <see cref="RedisLockRepository"/>
/// with configured defaults and automatic resource lifecycle management.
/// </summary>
public sealed class RedisLockProvider : IDisposable, IAsyncDisposable
{
    private readonly RedisLockRepository _repository;
    private readonly ILockService _lockService;
    private readonly ILogger<RedisLockProvider> _logger;
    private readonly DistributedLockOptions _options;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="RedisLockProvider"/>.
    /// </summary>
    /// <param name="options">Distributed lock configuration.</param>
    /// <param name="loggerFactory">Logger factory for creating scoped loggers.</param>
    public RedisLockProvider(
        IOptions<DistributedLockOptions> options,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _options = options.Value;
        _logger = loggerFactory.CreateLogger<RedisLockProvider>();

        _repository = new RedisLockRepository(
            _options.ConnectionString,
            loggerFactory.CreateLogger<RedisLockRepository>());

        var retryPolicy = new DefaultLockRetryPolicy(
            _options.RetryPolicyMaxRetries,
            TimeSpan.FromMilliseconds(_options.RetryPolicyInitialDelayMs),
            TimeSpan.FromMilliseconds(_options.RetryPolicyMaxDelayMs),
            _options.RetryPolicyJitterFactor);

        _lockService = new LockService(
            _repository,
            loggerFactory.CreateLogger<LockService>(),
            retryPolicy);

        _logger.LogInformation("RedisLockProvider initialized for {ConnectionString}", _options.ConnectionString);
    }

    /// <summary>
    /// Acquires a distributed lock on the given resource, blocking until acquired or cancelled.
    /// </summary>
    /// <param name="resourceKey">Unique key identifying the resource to lock.</param>
    /// <param name="ownerId">Identifier of the lock owner.</param>
    /// <param name="duration">Optional lock duration; defaults to <see cref="DistributedLockOptions.DefaultLockDuration"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The acquired lock.</returns>
    public Task<Lock> AcquireAsync(
        string resourceKey,
        string ownerId,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _lockService.AcquireAsync(
            resourceKey,
            ownerId,
            duration ?? _options.DefaultLockDuration,
            cancellationToken);
    }

    /// <summary>
    /// Attempts a non-blocking lock acquisition.
    /// </summary>
    /// <param name="resourceKey">Unique key identifying the resource to lock.</param>
    /// <param name="ownerId">Identifier of the lock owner.</param>
    /// <param name="duration">Optional lock duration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple indicating success, the lock (if acquired), and an error message (if failed).</returns>
    public Task<(bool Success, Lock? Lock, string? ErrorMessage)> TryAcquireAsync(
        string resourceKey,
        string ownerId,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _lockService.TryAcquireAsync(
            resourceKey,
            ownerId,
            duration ?? _options.DefaultLockDuration,
            cancellationToken);
    }

    /// <summary>
    /// Releases a previously acquired lock.
    /// </summary>
    /// <param name="resourceKey">The resource key of the lock to release.</param>
    /// <param name="ownerId">The owner who holds the lock.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the lock was released; false if it was not held by the specified owner.</returns>
    public Task<bool> ReleaseAsync(
        string resourceKey,
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _lockService.ReleaseAsync(resourceKey, ownerId, cancellationToken);
    }

    /// <summary>
    /// Renews an existing lock, extending its expiration.
    /// </summary>
    /// <param name="resourceKey">The resource key of the lock to renew.</param>
    /// <param name="ownerId">The owner who holds the lock.</param>
    /// <param name="newDuration">Optional new duration; defaults to <see cref="DistributedLockOptions.DefaultLockDuration"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the lock was renewed successfully.</returns>
    public Task<bool> RenewAsync(
        string resourceKey,
        string ownerId,
        TimeSpan? newDuration = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _lockService.RenewAsync(
            resourceKey,
            ownerId,
            newDuration ?? _options.DefaultLockDuration,
            cancellationToken);
    }

    /// <summary>
    /// Checks whether a resource is currently locked.
    /// </summary>
    /// <param name="resourceKey">The resource key to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the resource is locked.</returns>
    public Task<bool> IsLockedAsync(string resourceKey, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _lockService.IsLockedAsync(resourceKey, cancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        (_repository as IAsyncDisposable).DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _repository.DisposeAsync();
    }
}
