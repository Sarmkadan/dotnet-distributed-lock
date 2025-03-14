// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using SarmKadan.DistributedLock.Enums;

namespace SarmKadan.DistributedLock.Models;

/// <summary>
/// Represents a lock acquisition attempt with timing and retry information.
/// </summary>
public class LockAcquisition
{
    public string Id { get; set; }
    public string LockKey { get; set; }
    public string RequesterId { get; set; }
    public AcquisitionMode Mode { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? AcquiredAt { get; set; }
    public TimeSpan Timeout { get; set; }
    public int AttemptCount { get; set; }
    public int MaxRetries { get; set; }
    public List<AcquisitionAttempt> Attempts { get; set; }
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }

    public LockAcquisition()
    {
        Id = Guid.NewGuid().ToString();
        LockKey = string.Empty;
        RequesterId = string.Empty;
        Mode = AcquisitionMode.Blocking;
        RequestedAt = DateTime.UtcNow;
        Timeout = Constants.LockConstants.DefaultAcquisitionTimeout;
        MaxRetries = Constants.LockConstants.DefaultMaxRetries;
        Attempts = new List<AcquisitionAttempt>();
        AttemptCount = 0;
        IsSuccessful = false;
    }

    public LockAcquisition(string lockKey, string requesterId, AcquisitionMode mode, TimeSpan timeout, int maxRetries = 3)
    {
        if (string.IsNullOrWhiteSpace(lockKey))
            throw new ArgumentException("Lock key cannot be null or empty.", nameof(lockKey));

        if (string.IsNullOrWhiteSpace(requesterId))
            throw new ArgumentException("Requester ID cannot be null or empty.", nameof(requesterId));

        Id = Guid.NewGuid().ToString();
        LockKey = lockKey;
        RequesterId = requesterId;
        Mode = mode;
        RequestedAt = DateTime.UtcNow;
        Timeout = timeout;
        MaxRetries = maxRetries;
        Attempts = new List<AcquisitionAttempt>();
        AttemptCount = 0;
        IsSuccessful = false;
    }

    // Records an acquisition attempt
    public void RecordAttempt(bool succeeded, string? errorMessage = null, TimeSpan? elapsedTime = null)
    {
        AttemptCount++;
        var attempt = new AcquisitionAttempt
        {
            AttemptNumber = AttemptCount,
            Succeeded = succeeded,
            ErrorMessage = errorMessage,
            AttemptedAt = DateTime.UtcNow,
            ElapsedTime = elapsedTime ?? TimeSpan.Zero
        };
        Attempts.Add(attempt);

        if (succeeded)
        {
            IsSuccessful = true;
            AcquiredAt = DateTime.UtcNow;
        }
    }

    // Checks if more retries are available
    public bool CanRetry => AttemptCount < MaxRetries && !IsSuccessful;

    // Checks if the acquisition has timed out
    public bool IsTimedOut => (DateTime.UtcNow - RequestedAt) > Timeout && !IsSuccessful;

    // Calculates the total time spent acquiring the lock
    public TimeSpan TotalElapsedTime => AcquiredAt.HasValue
        ? AcquiredAt.Value - RequestedAt
        : DateTime.UtcNow - RequestedAt;

    // Gets the average time per attempt
    public TimeSpan AverageAttemptTime =>
        Attempts.Count > 0
            ? TimeSpan.FromMilliseconds(Attempts.Average(a => a.ElapsedTime.TotalMilliseconds))
            : TimeSpan.Zero;

    public override string ToString() =>
        $"LockAcquisition(LockKey={LockKey}, Requester={RequesterId}, Mode={Mode}, " +
        $"Attempts={AttemptCount}, Successful={IsSuccessful}, Elapsed={TotalElapsedTime.TotalSeconds:F2}s)";
}

/// <summary>
/// Represents a single acquisition attempt within a lock acquisition process.
/// </summary>
public class AcquisitionAttempt
{
    public int AttemptNumber { get; set; }
    public bool Succeeded { get; set; }
    public DateTime AttemptedAt { get; set; }
    public TimeSpan ElapsedTime { get; set; }
    public string? ErrorMessage { get; set; }
}
