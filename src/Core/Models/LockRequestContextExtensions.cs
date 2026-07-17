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
        report.AppendLine("=== Lock Request Diagnostic Report ===");
        report.AppendLine(string.Format(CultureInfo.InvariantCulture, "RequestId: {0}", context.RequestId));
        report.AppendLine(string.Format(CultureInfo.InvariantCulture, "LockKey: {0}", context.LockKey));
        report.AppendLine(string.Format(CultureInfo.InvariantCulture, "RequesterId: {0}", context.RequesterId));

        if (!string.IsNullOrEmpty(context.RequestorName))
        {
            report.AppendLine(string.Format(CultureInfo.InvariantCulture, "RequestorName: {0}", context.RequestorName));
        }

        report.AppendLine(string.Format(CultureInfo.InvariantCulture, "Mode: {0}", context.Mode));
        report.AppendLine(string.Format(CultureInfo.InvariantCulture, "RequestedDuration: {0:F2}s", context.RequestedDuration.TotalSeconds));
        report.AppendLine(string.Format(CultureInfo.InvariantCulture, "RequestedAt: {0}", context.RequestedAt.ToString("O", CultureInfo.InvariantCulture)));

        if (context.CompletedAt.HasValue)
        {
            report.AppendLine(string.Format(CultureInfo.InvariantCulture, "CompletedAt: {0}", context.CompletedAt.Value.ToString("O", CultureInfo.InvariantCulture)));
            report.AppendLine(string.Format(CultureInfo.InvariantCulture, "Duration: {0:F2}s", context.Duration.TotalSeconds));
            report.AppendLine(string.Format(CultureInfo.InvariantCulture, "Successful: {0}", context.Successful));

            if (!string.IsNullOrEmpty(context.FailureReason))
            {
                report.AppendLine(string.Format(CultureInfo.InvariantCulture, "FailureReason: {0}", context.FailureReason));
            }
        }
        else
        {
            report.AppendLine("Status: In Progress");
            report.AppendLine(string.Format(CultureInfo.InvariantCulture, "RemainingTime: {0:F2}s", context.RemainingTime().TotalSeconds));
            report.AppendLine(string.Format(CultureInfo.InvariantCulture, "HasExpired: {0}", context.HasExpired()));
        }

        report.AppendLine(string.Format(CultureInfo.InvariantCulture, "RetryCount: {0}", context.RetryCount));

        if (!string.IsNullOrEmpty(context.CorrelationId))
        {
            report.AppendLine(string.Format(CultureInfo.InvariantCulture, "CorrelationId: {0}", context.CorrelationId));
        }

        if (!string.IsNullOrEmpty(context.UserId))
        {
            report.AppendLine(string.Format(CultureInfo.InvariantCulture, "UserId: {0}", context.UserId));
            if (!string.IsNullOrEmpty(context.SessionId))
            {
                report.AppendLine(string.Format(CultureInfo.InvariantCulture, "SessionId: {0}", context.SessionId));
            }
        }

        if (includeCustomProperties && context.CustomProperties.Count > 0)
        {
            report.AppendLine("CustomProperties:");
            foreach (var kvp in context.CustomProperties)
            {
                report.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0}: {1}", kvp.Key, kvp.Value));
            }
        }

        report.AppendLine("=== End of Report ===");
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