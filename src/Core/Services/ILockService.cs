#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using SarmKadan.DistributedLock.Models;

namespace SarmKadan.DistributedLock.Services;

/// <summary>
/// Defines the contract for distributed lock operations. Implementations must guarantee
/// mutual exclusion across processes/machines for a given lock key.
/// </summary>
/// <remarks>
/// <para>
/// The typical usage pattern is:
/// <list type="number">
///   <item>Acquire a lock via <see cref="TryAcquireAsync"/> or <see cref="AcquireAsync"/></item>
///   <item>Perform the protected operation</item>
///   <item>Release the lock via <see cref="ReleaseAsync"/></item>
/// </list>
/// Locks have an optional TTL (<paramref name="duration"/>) and are automatically released
/// when the duration expires, preventing deadlocks from crashed owners.
/// </para>
/// <para>
/// Available backends: in-memory (<see cref="InMemoryLockRepository"/>), Redis, PostgreSQL, SQLite.
/// </para>
/// </remarks>
public interface ILockService
{
    /// <summary>
    /// Attempts to acquire a lock without blocking. Returns immediately with the result.
    /// </summary>
    /// <param name="lockKey">Unique key identifying the resource to lock.</param>
    /// <param name="ownerId">Identifier of the lock owner (process/instance ID).</param>
    /// <param name="duration">Optional lock TTL. If null, uses the configured default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A tuple containing: whether acquisition succeeded, the lock object if acquired,
    /// and an error message if acquisition failed.
    /// </returns>
    Task<(bool Success, Lock? Lock, string? ErrorMessage)> TryAcquireAsync(
        string lockKey,
        string ownerId,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Acquires a lock, retrying with backoff until successful or the cancellation token fires.
    /// </summary>
    /// <param name="lockKey">Unique key identifying the resource to lock.</param>
    /// <param name="ownerId">Identifier of the lock owner.</param>
    /// <param name="duration">Optional lock TTL.</param>
    /// <param name="cancellationToken">Cancellation token to abort the wait.</param>
    /// <returns>The acquired <see cref="Lock"/> object.</returns>
    /// <exception cref="OperationCanceledException">Thrown when acquisition is cancelled.</exception>
    Task<Lock> AcquireAsync(
        string lockKey,
        string ownerId,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Extends the TTL of an existing lock held by the specified owner.
    /// </summary>
    /// <param name="lockKey">The lock key to renew.</param>
    /// <param name="ownerId">Must match the current lock owner.</param>
    /// <param name="newDuration">New TTL from now. If null, uses the original duration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the lock was renewed; <c>false</c> if not held by this owner.</returns>
    Task<bool> RenewAsync(
        string lockKey,
        string ownerId,
        TimeSpan? newDuration = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Releases a lock. Only the current owner can release it.
    /// </summary>
    /// <param name="lockKey">The lock key to release.</param>
    /// <param name="ownerId">Must match the current lock owner.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the lock was released; <c>false</c> if not held by this owner.</returns>
    Task<bool> ReleaseAsync(
        string lockKey,
        string ownerId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Retrieves the current lock state for the given key, or <c>null</c> if not locked.
    /// </summary>
    Task<Lock?> GetLockAsync(string lockKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <c>true</c> if the specified key is currently locked by any owner.
    /// </summary>
    Task<bool> IsLockedAsync(string lockKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all locks that are currently held (not expired and not released).
    /// </summary>
    Task<IEnumerable<Lock>> GetAllActiveLockAsync(CancellationToken cancellationToken = default);
}
