#nullable enable

using System.Globalization;

namespace SarmKadan.DistributedLock.Models;

/// <summary>
/// Provides extension methods for <see cref="LockRequestContext"/> to enhance functionality
/// for audit trails, diagnostics, and distributed tracing scenarios.
/// </summary>
public static class LockRequestContextExtensions
{
    /// <summary>
    /// Determines whether the lock request has expired based on the requested duration.
    /// </summary>
    /// <param name="context">The lock request context.</param>
    /// <returns>True if the request has expired; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public static bool HasExpired(this LockRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return DateTime.UtcNow > context.RequestedAt.Add(context.RequestedDuration);
    }

    /// <summary>
    /// Gets the remaining time for the lock request before expiration.
    /// </summary>
    /// <param name="context">The lock request context.</param>
    /// <returns>The remaining time span before expiration, or TimeSpan.Zero if already expired.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public static TimeSpan RemainingTime(this LockRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var expirationTime = context.RequestedAt.Add(context.RequestedDuration);
        var remaining = expirationTime - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>
    /// Creates a diagnostic report string containing detailed information about the lock request.
    /// Useful for logging and debugging purposes.
    /// </summary>
    /// <param name="context">The lock request context.</param>
    /// <param name="includeCustomProperties">Whether to include custom properties in the report.</param>
    /// <returns>A formatted diagnostic string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public static string ToDiagnosticString(this LockRequestContext context, bool includeCustomProperties = true)
    {
        ArgumentNullException.ThrowIfNull(context);

        var report = new System.Text.StringBuilder();
        report.AppendLine(CultureInfo.InvariantCulture, $"=== Lock Request Diagnostic Report ===");
        report.AppendLine(CultureInfo.InvariantCulture, $"RequestId: {context.RequestId}");
        report.AppendLine(CultureInfo.InvariantCulture, $"LockKey: {context.LockKey}");
        report.AppendLine(CultureInfo.InvariantCulture, $"RequesterId: {context.RequesterId}");

        if (!string.IsNullOrEmpty(context.RequestorName))
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"RequestorName: {context.RequestorName}");
        }

        report.AppendLine(CultureInfo.InvariantCulture, $"Mode: {context.Mode}");
        report.AppendLine(CultureInfo.InvariantCulture, $"RequestedDuration: {context.RequestedDuration.TotalSeconds:F2}s");
        report.AppendLine(CultureInfo.InvariantCulture, $"RequestedAt: {context.RequestedAt:O}");

        if (context.CompletedAt.HasValue)
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"CompletedAt: {context.CompletedAt.Value:O}");
            report.AppendLine(CultureInfo.InvariantCulture, $"Duration: {context.Duration.TotalSeconds:F2}s");
            report.AppendLine(CultureInfo.InvariantCulture, $"Successful: {context.Successful}");

            if (!string.IsNullOrEmpty(context.FailureReason))
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"FailureReason: {context.FailureReason}");
            }
        }
        else
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"Status: In Progress");
            report.AppendLine(CultureInfo.InvariantCulture, $"RemainingTime: {context.RemainingTime().TotalSeconds:F2}s");
            report.AppendLine(CultureInfo.InvariantCulture, $"HasExpired: {context.HasExpired()}");
        }

        report.AppendLine(CultureInfo.InvariantCulture, $"RetryCount: {context.RetryCount}");

        if (!string.IsNullOrEmpty(context.CorrelationId))
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"CorrelationId: {context.CorrelationId}");
        }

        if (!string.IsNullOrEmpty(context.UserId))
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"UserId: {context.UserId}");
            if (!string.IsNullOrEmpty(context.SessionId))
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"SessionId: {context.SessionId}");
            }
        }

        if (includeCustomProperties && context.CustomProperties.Count > 0)
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"CustomProperties:");
            foreach (var kvp in context.CustomProperties)
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"  {kvp.Key}: {kvp.Value}");
            }
        }

        report.AppendLine(CultureInfo.InvariantCulture, $"=== End of Report ===");
        return report.ToString();
    }

    /// <summary>
    /// Determines whether the lock request was completed successfully within the requested duration.
    /// </summary>
    /// <param name="context">The lock request context.</param>
    /// <returns>True if the request was successful and completed within the requested duration; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public static bool IsSuccessfulWithinDuration(this LockRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Successful && context.CompletedAt.HasValue &&
               context.CompletedAt.Value <= context.RequestedAt.Add(context.RequestedDuration);
    }

    /// <summary>
    /// Gets a dictionary of standard metrics that can be used for monitoring and alerting.
    /// </summary>
    /// <param name="context">The lock request context.</param>
    /// <returns>A dictionary containing standard metrics like duration, retry count, and success status.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public static Dictionary<string, object> GetStandardMetrics(this LockRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var metrics = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["request_id"] = context.RequestId,
            ["lock_key"] = context.LockKey,
            ["requester_id"] = context.RequesterId,
            ["mode"] = context.Mode.ToString(),
            ["requested_duration_seconds"] = context.RequestedDuration.TotalSeconds,
            ["retry_count"] = context.RetryCount,
            ["successful"] = context.Successful,
            ["has_expired"] = context.HasExpired()
        };

        if (context.CompletedAt.HasValue)
        {
            metrics["completed_at"] = context.CompletedAt.Value;
            metrics["actual_duration_seconds"] = context.Duration.TotalSeconds;
        }
        else
        {
            metrics["remaining_time_seconds"] = context.RemainingTime().TotalSeconds;
        }

        return metrics;
    }
}