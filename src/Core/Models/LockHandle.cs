#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =====================================================================

using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Exceptions;
using SarmKadan.DistributedLock.Services;

using System.Threading;

namespace SarmKadan.DistributedLock.Models;

/// <summary>
/// Represents a handle to a distributed lock that supports automatic renewal and provides
/// a cancellation token that fires when the lock is lost or renewal fails.
/// </summary>
/// <remarks>
/// Implements <see cref="IAsyncDisposable"/> to ensure proper cleanup of renewal timers.
/// </remarks>
public sealed class LockHandle : IAsyncDisposable
{
    private readonly CancellationTokenSource _renewalFailedCts = new();
    private readonly CancellationTokenSource _disposalCts = new();
    private readonly ILockService _lockService;
    private readonly string _lockKey;
    private readonly string _ownerId;
    private readonly TimeSpan _renewalInterval;
    private readonly TimeSpan _lockDuration;
    private readonly System.Threading.Timer? _renewalTimer;
    private int _disposed;

    /// <summary>
    /// Gets the lock associated with this handle.
    /// </summary>
    public Lock Lock { get; }

    /// <summary>
    /// Gets a <see cref="CancellationToken"/> that fires when the lock renewal fails or the lock is lost.
    /// </summary>
    /// <remarks>
    /// This token should be passed to long-running operations to allow them to abort
    /// when they can no longer maintain the lock.
    /// </remarks>
    public CancellationToken RenewalFailedToken => _renewalFailedCts.Token;

    /// <summary>
    /// Gets a <see cref="CancellationToken"/> that fires when the handle is disposed.
    /// </summary>
    public CancellationToken DisposalToken => _disposalCts.Token;

    /// <summary>
    /// Gets whether the lock is still valid and held.
    /// </summary>
    public bool IsValid => Lock.IsValid && !RenewalFailedToken.IsCancellationRequested;

    /// <summary>
    /// Gets the current status of the lock.
    /// </summary>
    public LockStatus Status => Lock.Status;

    /// <summary>
    /// Gets the time remaining until the lock expires.
    /// </summary>
    public TimeSpan TimeRemaining => Lock.ExpiresAt - DateTime.UtcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="LockHandle"/> class.
    /// </summary>
    /// <param name="lockService">The lock service for performing renewals.</param>
    /// <param name="lock">The acquired lock.</param>
    /// <param name="renewalInterval">The interval at which to renew the lock.</param>
    /// <param name="lockDuration">The duration of each renewal.</param>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
    public LockHandle(ILockService lockService, Lock @lock, TimeSpan renewalInterval, TimeSpan lockDuration)
    {
        ArgumentNullException.ThrowIfNull(lockService);
        ArgumentNullException.ThrowIfNull(@lock);

        _lockService = lockService;
        Lock = @lock;
        _lockKey = @lock.Key;
        _ownerId = @lock.OwnerId;
        _renewalInterval = renewalInterval;
        _lockDuration = lockDuration;

        // Start auto-renewal timer (fire first renewal after 2/3 of interval to be safe)
        var initialDelay = renewalInterval.Multiply(0.66);
        _renewalTimer = new System.Threading.Timer(
            ExecuteRenewal,
            null,
            initialDelay,
            renewalInterval
        );
    }

    /// <summary>
    /// Manually renews the lock immediately.
    /// </summary>
    /// <returns>True if renewal was successful; otherwise, false.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the handle has been disposed.</exception>
    public async Task<bool> RenewAsync()
    {
        if (_disposed == 1)
            throw new ObjectDisposedException(nameof(LockHandle), "Handle has been disposed.");

        try
        {
            var renewed = await _lockService.RenewAsync(_lockKey, _ownerId, _lockDuration);
            if (renewed)
            {
                Lock.Renew(_lockDuration);
                return true;
            }

            // Renewal failed - trigger cancellation
            _renewalFailedCts.Cancel();
            return false;
        }
        catch (LockExpiredException)
        {
            // Lock has already expired - trigger cancellation
            _renewalFailedCts.Cancel();
            throw;
        }
        catch (LockNotOwnedException)
        {
            // We no longer own the lock - trigger cancellation
            _renewalFailedCts.Cancel();
            throw;
        }
        catch (InvalidFencingTokenException)
        {
            // Fencing token is invalid - we lost the lock
            _renewalFailedCts.Cancel();
            throw;
        }
        catch (Exception)
        {
            // Any other error means renewal failed
            _renewalFailedCts.Cancel();
            throw;
        }
    }

    /// <summary>
    /// Releases the lock manually.
    /// </summary>
    /// <returns>True if the lock was released; otherwise, false.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the handle has been disposed.</exception>
    public async Task<bool> ReleaseAsync()
    {
        if (_disposed == 1)
            throw new ObjectDisposedException(nameof(LockHandle), "Handle has been disposed.");

        try
        {
            var released = await _lockService.ReleaseAsync(_lockKey, _ownerId);
            if (released)
            {
                Lock.Release();
                // Trigger cancellation on successful release
                _renewalFailedCts.Cancel();
            }
            return released;
        }
        finally
        {
            await DisposeAsync();
        }
    }

    private async void ExecuteRenewal(object? state)
    {
        if (_disposed == 1 || _disposalCts.IsCancellationRequested)
            return;

        try
        {
            var renewed = await _lockService.RenewAsync(_lockKey, _ownerId, _lockDuration);
            if (!renewed)
            {
                // Renewal failed - trigger cancellation
                _renewalFailedCts.Cancel();
            }
        }
        catch (LockExpiredException)
        {
            // Lock has already expired - trigger cancellation
            _renewalFailedCts.Cancel();
        }
        catch (LockNotOwnedException)
        {
            // We no longer own the lock - trigger cancellation
            _renewalFailedCts.Cancel();
        }
        catch (InvalidFencingTokenException)
        {
            // Fencing token is invalid - we lost the lock
            _renewalFailedCts.Cancel();
        }
        catch (Exception)
        {
            // Any other error means renewal failed
            _renewalFailedCts.Cancel();
        }
    }

    /// <summary>
    /// Disposes the handle and stops auto-renewal.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        // Signal disposal
        _disposalCts.Cancel();

        // Stop the renewal timer
        _renewalTimer?.Dispose();

        // Attempt to release the lock if it's still held
        try
        {
            if (Lock.Status == LockStatus.Held)
            {
                await _lockService.ReleaseAsync(_lockKey, _ownerId);
            }
        }
        catch
        {
            // Ignore errors during disposal cleanup
        }

        // Trigger cancellation for renewal failures
        _renewalFailedCts.Cancel();

        // Dispose cancellation tokens
        _renewalFailedCts.Dispose();
        _disposalCts.Dispose();
    }
}
