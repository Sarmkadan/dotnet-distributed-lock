#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SarmKadan.DistributedLock.Configuration;
using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Exceptions;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Repository;
using System.Collections.Concurrent;

namespace SarmKadan.DistributedLock.Services;

/// <summary>
/// Orchestrates lock lifecycle across multiple resources, providing scoped lock execution,
/// multi-resource locking, and integration with <see cref="FencingTokenService"/>.
/// </summary>
public sealed class LockManager : IDisposable
{
    private readonly ILockService _lockService;
    private readonly FencingTokenService _fencingTokenService;
    private readonly ILogger<LockManager> _logger;
    private readonly DistributedLockOptions _options;
    private readonly ConcurrentDictionary<string, string> _heldLocks = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="LockManager"/>.
    /// </summary>
    /// <param name="lockService">The underlying lock service.</param>
    /// <param name="fencingTokenService">Fencing token service for token management.</param>
    /// <param name="options">Distributed lock configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public LockManager(
        ILockService lockService,
        FencingTokenService fencingTokenService,
        IOptions<DistributedLockOptions> options,
        ILogger<LockManager> logger)
    {
        _lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));
        _fencingTokenService = fencingTokenService ?? throw new ArgumentNullException(nameof(fencingTokenService));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Acquires a lock, executes the provided action, and releases the lock automatically.
    /// </summary>
    /// <param name="resourceKey">The resource to lock.</param>
    /// <param name="ownerId">The lock owner identifier.</param>
    /// <param name="action">The async action to execute while holding the lock.</param>
    /// <param name="duration">Optional lock duration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RunWithLockAsync(
        string resourceKey,
        string ownerId,
        Func<Lock, CancellationToken, Task> action,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(action);

        var lockInstance = await _lockService.AcquireAsync(
            resourceKey, ownerId, duration ?? _options.DefaultLockDuration, cancellationToken);

        _heldLocks[resourceKey] = ownerId;

        if (_options.UseFencingTokens)
        {
            _fencingTokenService.IssueToken(resourceKey);
        }

        try
        {
            await action(lockInstance, cancellationToken);
        }
        finally
        {
            _heldLocks.TryRemove(resourceKey, out _);

            if (_options.UseFencingTokens)
            {
                _fencingTokenService.RevokeToken(resourceKey);
            }

            await _lockService.ReleaseAsync(resourceKey, ownerId, cancellationToken);
            _logger.LogDebug("Lock released after scoped execution: {ResourceKey}", resourceKey);
        }
    }

    /// <summary>
    /// Acquires a lock, executes the provided function, releases the lock, and returns the result.
    /// </summary>
    /// <typeparam name="TResult">The return type of the function.</typeparam>
    /// <param name="resourceKey">The resource to lock.</param>
    /// <param name="ownerId">The lock owner identifier.</param>
    /// <param name="func">The async function to execute while holding the lock.</param>
    /// <param name="duration">Optional lock duration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the function.</returns>
    public async Task<TResult> RunWithLockAsync<TResult>(
        string resourceKey,
        string ownerId,
        Func<Lock, CancellationToken, Task<TResult>> func,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(func);

        var lockInstance = await _lockService.AcquireAsync(
            resourceKey, ownerId, duration ?? _options.DefaultLockDuration, cancellationToken);

        _heldLocks[resourceKey] = ownerId;

        if (_options.UseFencingTokens)
        {
            _fencingTokenService.IssueToken(resourceKey);
        }

        try
        {
            return await func(lockInstance, cancellationToken);
        }
        finally
        {
            _heldLocks.TryRemove(resourceKey, out _);

            if (_options.UseFencingTokens)
            {
                _fencingTokenService.RevokeToken(resourceKey);
            }

            await _lockService.ReleaseAsync(resourceKey, ownerId, cancellationToken);
        }
    }

    /// <summary>
    /// Acquires locks on multiple resources atomically (all-or-nothing with ordered acquisition to prevent deadlocks).
    /// </summary>
    /// <param name="resourceKeys">The resource keys to lock.</param>
    /// <param name="ownerId">The lock owner identifier.</param>
    /// <param name="duration">Optional lock duration applied to each resource.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of acquired locks in acquisition order.</returns>
    public async Task<IReadOnlyList<Lock>> AcquireMultipleAsync(
        IEnumerable<string> resourceKeys,
        string ownerId,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(resourceKeys);

        // Sort keys to establish a consistent acquisition order and prevent deadlocks
        var sortedKeys = resourceKeys.Distinct().OrderBy(k => k, StringComparer.Ordinal).ToList();
        var acquired = new List<Lock>(sortedKeys.Count);

        try
        {
            foreach (var key in sortedKeys)
            {
                var lockInstance = await _lockService.AcquireAsync(
                    key, ownerId, duration ?? _options.DefaultLockDuration, cancellationToken);
                acquired.Add(lockInstance);
                _heldLocks[key] = ownerId;
            }

            return acquired;
        }
        catch
        {
            // Rollback: release all already-acquired locks in reverse order
            for (int i = acquired.Count - 1; i >= 0; i--)
            {
                var key = acquired[i].Key;
                _heldLocks.TryRemove(key, out _);
                try
                {
                    await _lockService.ReleaseAsync(key, ownerId, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to release lock during rollback: {LockKey}", key);
                }
            }

            throw;
        }
    }

    /// <summary>
    /// Releases all locks currently held by this manager instance.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of locks successfully released.</returns>
    public async Task<int> ReleaseAllAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        int released = 0;
        var snapshot = _heldLocks.ToArray();

        foreach (var (key, ownerId) in snapshot)
        {
            try
            {
                if (await _lockService.ReleaseAsync(key, ownerId, cancellationToken))
                {
                    released++;
                }

                _heldLocks.TryRemove(key, out _);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to release lock: {LockKey}", key);
            }
        }

        _logger.LogInformation("Released {Count} locks", released);
        return released;
    }

    /// <summary>
    /// Gets the number of locks currently tracked by this manager instance.
    /// </summary>
    public int HeldLockCount => _heldLocks.Count;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _heldLocks.Clear();
    }
}
