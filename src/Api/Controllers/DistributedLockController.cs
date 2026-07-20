#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SarmKadan.DistributedLock.Core.Models;
using SarmKadan.DistributedLock.Core.Services;

/// <summary>
/// REST API controller for managing distributed locks.
/// Provides endpoints to acquire, release, renew, and query lock status.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class DistributedLockController : ControllerBase
{
    private readonly ILockService _lockService;
    private readonly ILogger<DistributedLockController> _logger;

    public DistributedLockController(ILockService lockService, ILogger<DistributedLockController> logger)
    {
        _lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Acquires a distributed lock with the specified configuration.
    /// </summary>
    /// <param name="request">Lock acquisition request containing lock name, duration, and mode</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Lock acquisition result with fencing token and lock ID</returns>
    [HttpPost("acquire")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LockAcquisitionResponse>> AcquireLock(
        [FromBody] LockAcquisitionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return BadRequest("Lock request cannot be null");

        _logger.LogInformation("Acquiring lock: {LockName}", request.LockName);

        try
        {
            var lockConfig = new LockConfiguration
            {
                LockName = request.LockName,
                Duration = TimeSpan.FromSeconds(request.DurationSeconds),
                AutoRenew = request.AutoRenew,
                RenewalInterval = request.RenewalIntervalSeconds.HasValue
                    ? TimeSpan.FromSeconds(request.RenewalIntervalSeconds.Value)
                    : null
            };

            var acquisition = await _lockService.AcquireLockAsync(lockConfig, cancellationToken);

            return Ok(new LockAcquisitionResponse
            {
                Success = true,
                LockId = acquisition.LockId,
                FencingToken = acquisition.FencingToken.Value,
                AcquiredAt = acquisition.AcquiredAt,
                ExpiresAt = acquisition.ExpiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire lock: {LockName}", request.LockName);
            return Conflict(new ErrorResponse { Message = ex.Message });
        }
    }

    /// <summary>
    /// Releases a previously acquired lock.
    /// </summary>
    /// <param name="lockId">The unique identifier of the lock</param>
    /// <param name="fencingToken">Fencing token to validate lock ownership</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Operation result</returns>
    [HttpPost("release/{lockId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OperationResponse>> ReleaseLock(
        [FromRoute] string lockId,
        [FromQuery] ulong fencingToken,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Releasing lock: {LockId}", lockId);

        try
        {
            await _lockService.ReleaseLockAsync(lockId, fencingToken, cancellationToken);
            return Ok(new OperationResponse { Success = true, Message = "Lock released successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to release lock: {LockId}", lockId);
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Message = ex.Message });
        }
    }

    /// <summary>
    /// Renews a lock's expiration time.
    /// </summary>
    /// <param name="lockId">The unique identifier of the lock</param>
    /// <param name="fencingToken">Fencing token to validate lock ownership</param>
    /// <param name="additionalSeconds">Seconds to add to lock duration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated lock information</returns>
    [HttpPost("renew/{lockId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LockRenewalResponse>> RenewLock(
        [FromRoute] string lockId,
        [FromQuery] ulong fencingToken,
        [FromQuery] int additionalSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Renewing lock: {LockId} for {Seconds} seconds", lockId, additionalSeconds);

        try
        {
            var updatedLock = await _lockService.RenewLockAsync(
                lockId,
                fencingToken,
                TimeSpan.FromSeconds(additionalSeconds),
                cancellationToken);

            return Ok(new LockRenewalResponse
            {
                Success = true,
                ExpiresAt = updatedLock.ExpiresAt,
                RemainingSeconds = (int)(updatedLock.ExpiresAt - DateTime.UtcNow).TotalSeconds
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to renew lock: {LockId}", lockId);
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Message = ex.Message });
        }
    }

    /// <summary>
    /// Extends the lease/expiration time of an existing lock.
    /// Only succeeds if the current ownerId holds the lock.
    /// </summary>
    /// <param name="lockId">The unique identifier of the lock</param>
    /// <param name="ownerId">The owner ID to validate ownership</param>
    /// <param name="extensionSeconds">Seconds to extend the lock by</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Operation result</returns>
    [HttpPost("extend/{lockId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OperationResponse>> ExtendLock(
        [FromRoute] string lockId,
        [FromQuery] string ownerId,
        [FromQuery] int extensionSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extending lock: {LockId} for {Seconds} seconds by owner {OwnerId}", lockId, extensionSeconds, ownerId);

        try
        {
            var extension = TimeSpan.FromSeconds(extensionSeconds);
            var success = await _lockService.TryExtendAsync(lockId, ownerId, extension, cancellationToken);

            if (success)
            {
                return Ok(new OperationResponse { Success = true, Message = "Lock extended successfully" });
            }

            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Message = "Failed to extend lock - either lock doesn't exist or owner mismatch" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extend lock: {LockId}", lockId);
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Message = ex.Message });
        }
    }

    /// <summary>
    /// Gets the current status of a lock.
    /// </summary>
    /// <param name="lockId">The unique identifier of the lock</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Lock status information</returns>
    [HttpGet("status/{lockId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LockStatusResponse>> GetLockStatus(
        [FromRoute] string lockId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting status for lock: {LockId}", lockId);

        try
        {
            var lockInfo = await _lockService.GetLockAsync(lockId, cancellationToken);

            return Ok(new LockStatusResponse
            {
                LockId = lockInfo.Id,
                Name = lockInfo.Name,
                IsActive = lockInfo.IsActive,
                OwnerId = lockInfo.OwnerId,
                AcquiredAt = lockInfo.AcquiredAt,
                ExpiresAt = lockInfo.ExpiresAt,
                RemainingSeconds = lockInfo.IsActive
                    ? (int)(lockInfo.ExpiresAt - DateTime.UtcNow).TotalSeconds
                    : 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get lock status: {LockId}", lockId);
            return NotFound(new ErrorResponse { Message = $"Lock not found: {lockId}" });
        }
    }
}

public record LockAcquisitionRequest
{
    public required string LockName { get; init; }
    public required int DurationSeconds { get; init; }
    public bool AutoRenew { get; init; }
    public int? RenewalIntervalSeconds { get; init; }
}

public record LockAcquisitionResponse
{
    public bool Success { get; init; }
    public required string LockId { get; init; }
    public required ulong FencingToken { get; init; }
    public DateTime AcquiredAt { get; init; }
    public DateTime ExpiresAt { get; init; }
}

public record LockRenewalResponse
{
    public bool Success { get; init; }
    public DateTime ExpiresAt { get; init; }
    public int RemainingSeconds { get; init; }
}

public record LockStatusResponse
{
    public required string LockId { get; init; }
    public required string Name { get; init; }
    public required bool IsActive { get; init; }
    public required string OwnerId { get; init; }
    public DateTime AcquiredAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public int RemainingSeconds { get; init; }
}

public record OperationResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}

public record ErrorResponse
{
    public string Message { get; init; } = string.Empty;
}
