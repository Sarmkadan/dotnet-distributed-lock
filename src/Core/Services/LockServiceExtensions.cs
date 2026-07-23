#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =====================================================================

using System.Threading;
using SarmKadan.DistributedLock.Exceptions;
using SarmKadan.DistributedLock.Models;

namespace SarmKadan.DistributedLock.Services;

/// <summary>
/// Provides extension methods for <see cref="ILockService"/> to enable scoped lock execution with automatic cleanup.
/// </summary>
public static class LockServiceExtensions
{
    /// <summary>
    /// Executes a function while holding a distributed lock, with automatic renewal and guaranteed cleanup.
    /// </summary>
    /// <typeparam name="T">The return type of the function.</typeparam>
    /// <param name="lockService">The lock service.</param>
    /// <param name="lockKey">The unique identifier for the resource to lock.</param>
    /// <param name="ownerId">The unique identifier for the lock owner.</param>
    /// <param name="body">The function to execute while holding the lock.</param>
    /// <param name="options">Optional lock acquisition options including renewal settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="LockResult{T}"/> indicating the result of the operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lockService"/> or <paramref name="body"/> is null.</exception>
    public static async Task<LockResult<T>> RunWithLockAsync<T>(
        this ILockService lockService,
        string lockKey,
        string ownerId,
        Func<CancellationToken, Task<T>> body,
        LockAcquisitionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lockService);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentException.ThrowIfNullOrEmpty(lockKey);
        ArgumentException.ThrowIfNullOrEmpty(ownerId);

        var effectiveOptions = options ?? new LockAcquisitionOptions();
        effectiveOptions.Validate();

        LockHandle? lockHandle = null;
        CancellationTokenSource? linkedCts = null;

        try
        {
            // Acquire the lock with automatic renewal
            lockHandle = await lockService.AcquireWithRenewalAsync(lockKey, ownerId, effectiveOptions, cancellationToken);

            // Create a cancellation token that links to lock loss
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lockHandle.RenewalFailedToken);

            // Execute the user's function with the linked cancellation token
            var result = await body(linkedCts.Token);

            return LockResult<T>.Acquired(result);
        }
        catch (LockAcquisitionException ex) when (ex is not null)
        {
            // Lock acquisition failed - return contended result
            return LockResult<T>.Contended(ex.Message);
        }
        catch (Exception ex) when (ex is not null)
        {
            // Any other error - return faulted result
            return LockResult<T>.Faulted(ex);
        }
        finally
        {
            // Clean up: dispose the linked token source and release the lock
            linkedCts?.Dispose();

            if (lockHandle is not null)
            {
                try
                {
                    await lockHandle.DisposeAsync();
                }
                catch
                {
                    // Ignore errors during cleanup
                }
            }
        }
    }

    /// <summary>
    /// Executes a function while holding a distributed lock, with automatic renewal and guaranteed cleanup.
    /// This overload returns void and throws exceptions on failure.
    /// </summary>
    /// <param name="lockService">The lock service.</param>
    /// <param name="lockKey">The unique identifier for the resource to lock.</param>
    /// <param name="ownerId">The unique identifier for the lock owner.</param>
    /// <param name="body">The action to execute while holding the lock.</param>
    /// <param name="options">Optional lock acquisition options including renewal settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="LockResult"/> indicating the result of the operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lockService"/> or <paramref name="body"/> is null.</exception>
    public static async Task<LockResult> RunWithLockAsync(
        this ILockService lockService,
        string lockKey,
        string ownerId,
        Func<CancellationToken, Task> body,
        LockAcquisitionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lockService);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentException.ThrowIfNullOrEmpty(lockKey);
        ArgumentException.ThrowIfNullOrEmpty(ownerId);

        var effectiveOptions = options ?? new LockAcquisitionOptions();
        effectiveOptions.Validate();

        LockHandle? lockHandle = null;
        CancellationTokenSource? linkedCts = null;

        try
        {
            // Acquire the lock with automatic renewal
            lockHandle = await lockService.AcquireWithRenewalAsync(lockKey, ownerId, effectiveOptions, cancellationToken);

            // Create a cancellation token that links to lock loss
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lockHandle.RenewalFailedToken);

            // Execute the user's function with the linked cancellation token
            await body(linkedCts.Token);

            return LockResult.Acquired();
        }
        catch (LockAcquisitionException ex) when (ex is not null)
        {
            // Lock acquisition failed - return contended result
            return LockResult.Contended(ex.Message);
        }
        catch (Exception ex) when (ex is not null)
        {
            // Any other error - return faulted result
            return LockResult.Faulted(ex);
        }
        finally
        {
            // Clean up: dispose the linked token source and release the lock
            linkedCts?.Dispose();

            if (lockHandle is not null)
            {
                try
                {
                    await lockHandle.DisposeAsync();
                }
                catch
                {
                    // Ignore errors during cleanup
                }
            }
        }
    }

    /// <summary>
    /// Executes a function while holding a distributed lock, with automatic renewal and guaranteed cleanup.
    /// This overload uses the default lock duration and no renewal.
    /// </summary>
    /// <typeparam name="T">The return type of the function.</typeparam>
    /// <param name="lockService">The lock service.</param>
    /// <param name="lockKey">The unique identifier for the resource to lock.</param>
    /// <param name="ownerId">The unique identifier for the lock owner.</param>
    /// <param name="body">The function to execute while holding the lock.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="LockResult{T}"/> indicating the result of the operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lockService"/> or <paramref name="body"/> is null.</exception>
    public static async Task<LockResult<T>> RunWithLockAsync<T>(
        this ILockService lockService,
        string lockKey,
        string ownerId,
        Func<CancellationToken, Task<T>> body,
        CancellationToken cancellationToken = default)
    {
        return await RunWithLockAsync(lockService, lockKey, ownerId, body, null, cancellationToken);
    }

    /// <summary>
    /// Executes an action while holding a distributed lock, with automatic renewal and guaranteed cleanup.
    /// This overload uses the default lock duration and no renewal.
    /// </summary>
    /// <param name="lockService">The lock service.</param>
    /// <param name="lockKey">The unique identifier for the resource to lock.</param>
    /// <param name="ownerId">The unique identifier for the lock owner.</param>
    /// <param name="body">The action to execute while holding the lock.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="LockResult"/> indicating the result of the operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lockService"/> or <paramref name="body"/> is null.</exception>
    public static async Task<LockResult> RunWithLockAsync(
        this ILockService lockService,
        string lockKey,
        string ownerId,
        Func<CancellationToken, Task> body,
        CancellationToken cancellationToken = default)
    {
        return await RunWithLockAsync(lockService, lockKey, ownerId, body, null, cancellationToken);
    }
}