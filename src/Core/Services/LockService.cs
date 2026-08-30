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

    public LockService(ILockRepository repository, ILogger<LockService> logger)
        : this(repository, logger, new DefaultLockRetryPolicy())
    {
    }

    public LockService(ILockRepository repository, ILogger<LockService> logger, ILockRetryPolicy retryPolicy)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        _metrics = new LockMetrics();
    }

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
        var renewalInterval = TimeSpan.FromTicks(@lock.Duration.Ticks * (long)effectiveOptions.RenewalFraction);

        // Create the handle with auto-renewal
        return new LockHandle(this, @lock, renewalInterval, @lock.Duration);
    }

    public LockMetrics GetMetrics() => _metrics;
}
