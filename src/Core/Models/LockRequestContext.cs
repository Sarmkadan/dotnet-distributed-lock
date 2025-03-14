// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using SarmKadan.DistributedLock.Enums;

namespace SarmKadan.DistributedLock.Models;

/// <summary>
/// Represents the context and metadata for a lock request, useful for audit trails and debugging.
/// </summary>
public class LockRequestContext
{
    public string RequestId { get; set; }
    public string LockKey { get; set; }
    public string RequesterId { get; set; }
    public string? RequestorName { get; set; }
    public AcquisitionMode Mode { get; set; }
    public TimeSpan RequestedDuration { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool Successful { get; set; }
    public string? FailureReason { get; set; }
    public int RetryCount { get; set; }
    public Dictionary<string, object> CustomProperties { get; set; }
    public string? CorrelationId { get; set; }
    public string? UserId { get; set; }
    public string? SessionId { get; set; }

    public LockRequestContext()
    {
        RequestId = Guid.NewGuid().ToString();
        LockKey = string.Empty;
        RequesterId = string.Empty;
        Mode = AcquisitionMode.Blocking;
        RequestedDuration = Constants.LockConstants.DefaultLockTimeout;
        RequestedAt = DateTime.UtcNow;
        CustomProperties = new Dictionary<string, object>();
        RetryCount = 0;
        Successful = false;
    }

    public LockRequestContext(string lockKey, string requesterId, AcquisitionMode mode = AcquisitionMode.Blocking)
        : this()
    {
        if (string.IsNullOrWhiteSpace(lockKey))
            throw new ArgumentException("Lock key cannot be null or empty.", nameof(lockKey));

        if (string.IsNullOrWhiteSpace(requesterId))
            throw new ArgumentException("Requester ID cannot be null or empty.", nameof(requesterId));

        LockKey = lockKey;
        RequesterId = requesterId;
        Mode = mode;
    }

    // Marks the request as completed
    public void MarkCompleted(bool successful, string? failureReason = null)
    {
        CompletedAt = DateTime.UtcNow;
        Successful = successful;
        FailureReason = failureReason;
    }

    // Calculates the total time taken for the request
    public TimeSpan Duration =>
        CompletedAt.HasValue
            ? CompletedAt.Value - RequestedAt
            : DateTime.UtcNow - RequestedAt;

    // Adds custom property for tracking
    public void AddProperty(string key, object value)
    {
        CustomProperties[key] = value;
    }

    // Retrieves a custom property
    public object? GetProperty(string key)
    {
        return CustomProperties.TryGetValue(key, out var value) ? value : null;
    }

    // Sets correlation ID for distributed tracing
    public void SetCorrelationId(string correlationId)
    {
        CorrelationId = correlationId;
    }

    // Sets user context information
    public void SetUserContext(string userId, string? sessionId = null)
    {
        UserId = userId;
        SessionId = sessionId;
    }

    // Increments retry count
    public void IncrementRetryCount()
    {
        RetryCount++;
    }

    public override string ToString() =>
        $"LockRequestContext(RequestId={RequestId}, LockKey={LockKey}, " +
        $"Requester={RequesterId}, Mode={Mode}, Successful={Successful}, " +
        $"Duration={Duration.TotalSeconds:F2}s, Retries={RetryCount})";
}
