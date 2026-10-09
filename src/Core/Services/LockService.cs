#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =====================================================================

using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Exceptions;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Repository;

namespace SarmKadan.DistributedLock.Services;

/// <summary>
/// Core service for managing distributed locks with retry logic and auto-renewal support.
/// </summary>
public sealed class LockService : ILockService
{
    private readonly ILockRepository _repository;
    private readonly ILogger<LockService> _logger;
    private readonly LockMetrics _metrics;
    private readonly ILockRetryPolicy _retryPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="LockService"/> class using the default retry policy.
    /// </summary>
    /// <param name="repository">The repository used to persist and query lock state.</param>
    /// <param name="logger">The logger for lock operations.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="repository"/> or <paramref name="logger"/> is null.</exception>
    public LockService(ILockRepository repository, ILogger<LockService> logger)
        : this(repository, logger, new DefaultLockRetryPolicy())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LockService"/> class with a custom retry policy.
    /// </summary>
    /// <param name="repository">The repository used to persist and query lock state.</param>
    /// <param name="logger">The logger for lock operations.</param>
    /// <param name="retryPolicy">The policy that controls retry count and delay between acquisition attempts.</param>
    /// <exception cref="ArgumentNullException">Thrown if any argument is null.</exception>
    public LockService(ILockRepository repository, ILogger<LockService> logger, ILockRetryPolicy retryPolicy)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        _metrics = new LockMetrics();
    }

    /// <summary>
    /// Makes a single non-blocking attempt to acquire a lock.
    /// </summary>
    /// <param name="lockKey">The unique identifier for the resource.</param>
    /// <param name="ownerId">The unique identifier for the lock owner.</param>
    /// <param name="duration">The optional duration to hold the lock. Defaults to <c>LockConstants.DefaultLockTimeout</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A tuple of success flag, the acquired lock (if successful), and an error message (if failed).
    /// Repository exceptions are caught and reported through the error message rather than thrown.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="lockKey"/> or <paramref name="ownerId"/> is null, empty, or whitespace.</exception>
    public async Task<(bool Success, Lock? Lock, string? ErrorMessage)> TryAcquireAsync(
        string lockKey,
        string ownerId,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        try
        {
            var lockDuration = duration ?? Constants.LockConstants.DefaultLockTimeout;
            var @lock = new Lock(lockKey, ownerId, lockDuration);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var acquired = await _repository.AcquireAsync(@lock, cancellationToken);
            stopwatch.Stop();

            if (acquired)
            {
                @lock.Status = LockStatus.Held;
                _metrics.RecordSuccessfulAcquisition(stopwatch.Elapsed.TotalMilliseconds);
                _logger.LogInformation("Lock acquired: {LockKey} by {OwnerId} in {ElapsedMs}ms",
                    lockKey, ownerId, stopwatch.Elapsed.TotalMilliseconds);
                return (true, @lock, null);
            }

            _metrics.RecordFailedAcquisition();
            _logger.LogWarning("Failed to acquire lock: {LockKey} by {OwnerId}", lockKey, ownerId);
            return (false, null, "Lock is currently held by another owner");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acquiring lock {LockKey} by {OwnerId}", lockKey, ownerId);
            return (false, null, ex.Message);
        }
    }

    /// <summary>
    /// Acquires a lock, retrying with the configured retry policy until it succeeds or attempts are exhausted.
    /// </summary>
    /// <param name="lockKey">The unique identifier for the resource.</param>
    /// <param name="ownerId">The unique identifier for the lock owner.</param>
    /// <param name="duration">The optional duration to hold the lock. Defaults to <c>LockConstants.DefaultLockTimeout</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The acquired lock.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="lockKey"/> or <paramref name="ownerId"/> is null, empty, or whitespace.</exception>
    /// <exception cref="LockAcquisitionException">Thrown if the lock could not be acquired within the retry limit or the acquisition timeout.</exception>
    public async Task<Lock> AcquireAsync(
        string lockKey,
        string ownerId,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        var maxRetries = _retryPolicy.MaxRetries;
        var acquisitionTimeout = Constants.LockConstants.DefaultAcquisitionTimeout;

        var acquisition = new LockAcquisition(lockKey, ownerId, AcquisitionMode.Blocking, acquisitionTimeout, maxRetries);
        var startTime = DateTime.UtcNow;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            if (acquisitionTimeout.TotalSeconds > 0 && (DateTime.UtcNow - startTime) > acquisitionTimeout)
            {
                _logger.LogError("Lock acquisition timeout for {LockKey} by {OwnerId}", lockKey, ownerId);
                throw new LockAcquisitionException(lockKey, acquisitionTimeout, attempt);
            }

            var (success, @lock, errorMessage) = await TryAcquireAsync(lockKey, ownerId, duration, cancellationToken);
            acquisition.RecordAttempt(success, errorMessage);

            if (success && @lock is not null)
            {
                _logger.LogInformation("Lock acquired successfully on attempt {Attempt} for {LockKey}", attempt + 1, lockKey);
                return @lock;
            }

            if (attempt < maxRetries - 1)
            {
                var delay = _retryPolicy.GetDelay(attempt);
                _logger.LogDebug(
                    "Lock acquisition attempt {Attempt} of {MaxRetries} failed for {LockKey} by {OwnerId}; retrying after {Delay}",
                    attempt + 1, maxRetries, lockKey, ownerId, delay);
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new LockAcquisitionException(lockKey, acquisitionTimeout, maxRetries);
    }

    /// <summary>
    /// Renews a lock held by the specified owner.
    /// </summary>
    /// <param name="lockKey">The unique identifier for the resource.</param>
    /// <param name="ownerId">The unique identifier for the lock owner.</param>
    /// <param name="newDuration">The new duration to hold the lock. Defaults to <c>LockConstants.DefaultLockTimeout</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the lock was renewed; false if it does not exist, the owner does not match, or an error occurred.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="lockKey"/> or <paramref name="ownerId"/> is null, empty, or whitespace.</exception>
    public async Task<bool> RenewAsync(
        string lockKey,
        string ownerId,
        TimeSpan? newDuration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        try
        {
            var duration = newDuration ?? Constants.LockConstants.DefaultLockTimeout;
            var renewed = await _repository.RenewAsync(lockKey, ownerId, duration, cancellationToken);

            if (renewed)
            {
                _metrics.RecordSuccessfulRenewal();
                _logger.LogInformation("Lock renewed: {LockKey} by {OwnerId}", lockKey, ownerId);
            }
            else
            {
                _metrics.RecordFailedRenewal();
                _logger.LogWarning("Failed to renew lock: {LockKey} by {OwnerId}", lockKey, ownerId);
            }

            return renewed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renewing lock {LockKey} by {OwnerId}", lockKey, ownerId);
            _metrics.RecordFailedRenewal();
            return false;
        }
    }

    /// <summary>
    /// Extends a lock held by the specified owner by the given duration.
    /// </summary>
    /// <param name="lockKey">The unique identifier for the resource.</param>
    /// <param name="ownerId">The unique identifier for the lock owner.</param>
    /// <param name="extension">The duration to extend the lock by. Must be positive.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the lock was extended; false if it does not exist, the owner does not match, or an error occurred.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="lockKey"/> or <paramref name="ownerId"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="extension"/> is zero or negative.</exception>
    public async Task<bool> TryExtendAsync(
        string lockKey,
        string ownerId,
        TimeSpan extension,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (extension <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(extension), "Extension must be positive.");
        }

        try
        {
            var extended = await _repository.RenewAsync(lockKey, ownerId, extension, cancellationToken);

            if (extended)
            {
                _metrics.RecordSuccessfulRenewal();
                _logger.LogInformation("Lock extended: {LockKey} by {OwnerId} for {Extension}", lockKey, ownerId, extension);
            }
            else
            {
                _metrics.RecordFailedRenewal();
                _logger.LogWarning(
                    "Failed to extend lock: {LockKey} by {OwnerId} - either lock doesn't exist or owner mismatch",
                    lockKey, ownerId);
            }

            return extended;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extending lock {LockKey} by {OwnerId}", lockKey, ownerId);
            _metrics.RecordFailedRenewal();
            return false;
        }
    }

    /// <summary>
    /// Renews a lock after validating the caller's fencing token, and returns the refreshed lock.
    /// </summary>
    /// <param name="lockKey">The unique identifier for the resource.</param>
    /// <param name="fencingToken">The fencing token previously issued for this lock.</param>
    /// <param name="newDuration">The new duration to hold the lock.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The renewed lock.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="lockKey"/> is null, empty, or whitespace.</exception>
    /// <exception cref="InvalidFencingTokenException">Thrown if the fencing token is no longer valid for this lock.</exception>
    /// <exception cref="Core.Exceptions.LockRenewalFailedException">Thrown if the lock is not found or the renewal could not be applied.</exception>
    public async Task<Lock> RenewLockAsync(
        string lockKey,
        ulong fencingToken,
        TimeSpan newDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockKey);
        var isValid = await _repository.ValidateFencingTokenAsync(lockKey, fencingToken, cancellationToken);
        if (!isValid)
        {
            _logger.LogWarning("Invalid fencing token for lock: {LockKey}", lockKey);
            throw new InvalidFencingTokenException(fencingToken.ToString(), lockKey);
        }

        var @lock = await _repository.GetByKeyAsync(lockKey, cancellationToken)
            ?? throw new Core.Exceptions.LockRenewalFailedException(lockKey, $"Lock '{lockKey}' not found.");

        var renewed = await _repository.RenewAsync(lockKey, @lock.OwnerId, newDuration, cancellationToken);
        if (!renewed)
        {
            _metrics.RecordFailedRenewal();
            throw new Core.Exceptions.LockRenewalFailedException(lockKey, $"Failed to renew lock '{lockKey}'.");
        }

        _metrics.RecordSuccessfulRenewal();
        _logger.LogInformation("Lock renewed via fencing token: {LockKey}", lockKey);

        return await _repository.GetByKeyAsync(lockKey, cancellationToken)
            ?? throw new Core.Exceptions.LockRenewalFailedException(lockKey, $"Lock '{lockKey}' not found after renewal.");
    }

    /// <summary>
    /// Releases a lock held by the specified owner.
    /// </summary>
    /// <param name="lockKey">The unique identifier for the resource.</param>
    /// <param name="ownerId">The unique identifier for the lock owner.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the lock was released; false if it does not exist, the owner does not match, or an error occurred.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="lockKey"/> or <paramref name="ownerId"/> is null, empty, or whitespace.</exception>
    public async Task<bool> ReleaseAsync(
        string lockKey,
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        try
        {
            var @lock = await _repository.GetByKeyAsync(lockKey, cancellationToken);
            if (@lock is null)
            {
                _logger.LogWarning("Lock not found for release: {LockKey} by {OwnerId}", lockKey, ownerId);
                return false;
            }

            var holdTime = DateTime.UtcNow - @lock.AcquiredAt;
            var released = await _repository.ReleaseAsync(lockKey, ownerId, cancellationToken);

            if (released)
            {
                _metrics.RecordRelease(holdTime.TotalMilliseconds);
                _logger.LogInformation("Lock released: {LockKey} by {OwnerId} (held for {HoldTime}ms)",
                    lockKey, ownerId, holdTime.TotalMilliseconds);
            }

            return released;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing lock {LockKey} by {OwnerId}", lockKey, ownerId);
            return false;
        }
    }

    /// <summary>
    /// Gets the current lock for the specified key.
    /// </summary>
    /// <param name="lockKey">The unique identifier for the resource.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The lock if one exists; otherwise, null. Repository errors are logged and reported as null.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="lockKey"/> is null, empty, or whitespace.</exception>
    public async Task<Lock?> GetLockAsync(string lockKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockKey);
        try
        {
            return await _repository.GetByKeyAsync(lockKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving lock {LockKey}", lockKey);
            return null;
        }
    }

    /// <summary>
    /// Checks whether a lock currently exists for the specified key.
    /// </summary>
    /// <param name="lockKey">The unique identifier for the resource.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the lock exists; otherwise, false. Repository errors are logged and reported as false.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="lockKey"/> is null, empty, or whitespace.</exception>
    public async Task<bool> IsLockedAsync(string lockKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockKey);
        try
        {
            return await _repository.ExistsAsync(lockKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking lock existence for {LockKey}", lockKey);
            return false;
        }
    }

    /// <summary>
    /// Gets all currently active locks.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The active locks, or an empty sequence if the repository query fails (the error is logged).</returns>
    public async Task<IEnumerable<Lock>> GetAllActiveLockAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _repository.GetAllActiveLockAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all active locks");
            return Enumerable.Empty<Lock>();
        }
    }

    /// <summary>
    /// Acquires a lock and creates a handle that automatically renews it.
    /// </summary>
    /// <remarks>
    /// The renewal interval is calculated from the lock duration and renewal fraction.
    /// If that calculation produces a non-positive interval, a one-second interval is used.
    /// </remarks>
    public async Task<LockHandle> AcquireWithRenewalAsync(
        string lockKey,
        string ownerId,
        LockAcquisitionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveOptions = options ?? new LockAcquisitionOptions();
        effectiveOptions.Validate();

        // Acquire the lock first
        var @lock = await AcquireAsync(lockKey, ownerId, null, cancellationToken);

        // Calculate renewal interval based on lock duration
        var renewalTicks = (long)(@lock.Duration.Ticks * effectiveOptions.RenewalFraction);
        var renewalInterval = renewalTicks > 0
            ? TimeSpan.FromTicks(renewalTicks)
            : TimeSpan.FromSeconds(1);

        // Create the handle with auto-renewal
        return new LockHandle(this, @lock, renewalInterval, @lock.Duration);
    }

    /// <summary>
    /// Gets the metrics collected by this service instance.
    /// </summary>
    /// <returns>The <see cref="LockMetrics"/> instance tracking acquisitions, renewals, and releases.</returns>
    public LockMetrics GetMetrics() => _metrics;
}
