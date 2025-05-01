// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Utilities.Extensions;

/// <summary>
/// Extension methods for DateTime operations.
/// Provides utilities for lock expiration calculations, time comparisons, and formatting.
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// Determines if a lock has expired based on its expiration time.
    /// </summary>
    public static bool IsExpired(this DateTime expiresAt)
    {
        return DateTime.UtcNow >= expiresAt;
    }

    /// <summary>
    /// Determines if a lock is still valid (not expired).
    /// </summary>
    public static bool IsValid(this DateTime expiresAt)
    {
        return DateTime.UtcNow < expiresAt;
    }

    /// <summary>
    /// Calculates the remaining time until a lock expires.
    /// Returns TimeSpan.Zero if already expired.
    /// </summary>
    public static TimeSpan GetRemainingTime(this DateTime expiresAt)
    {
        var remaining = expiresAt - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>
    /// Calculates the remaining seconds until a lock expires.
    /// Returns 0 if already expired.
    /// </summary>
    public static long GetRemainingSeconds(this DateTime expiresAt)
    {
        return (long)expiresAt.GetRemainingTime().TotalSeconds;
    }

    /// <summary>
    /// Calculates the remaining milliseconds until a lock expires.
    /// Returns 0 if already expired.
    /// </summary>
    public static long GetRemainingMilliseconds(this DateTime expiresAt)
    {
        return (long)expiresAt.GetRemainingTime().TotalMilliseconds;
    }

    /// <summary>
    /// Determines if a lock will expire within a specified grace period.
    /// Useful for proactive renewal logic.
    /// </summary>
    public static bool ExpiresWithin(this DateTime expiresAt, TimeSpan gracePeriod)
    {
        var remaining = expiresAt.GetRemainingTime();
        return remaining > TimeSpan.Zero && remaining <= gracePeriod;
    }

    /// <summary>
    /// Formats a DateTime as an ISO 8601 string with UTC timezone.
    /// </summary>
    public static string ToIso8601String(this DateTime dateTime)
    {
        return dateTime.ToUniversalTime().ToString("O");
    }

    /// <summary>
    /// Formats a DateTime in a human-readable format.
    /// Example: "2 hours 30 minutes ago"
    /// </summary>
    public static string ToHumanReadableFormat(this DateTime dateTime)
    {
        var timeSpan = DateTime.UtcNow - dateTime.ToUniversalTime();

        if (timeSpan.TotalSeconds < 60)
            return "Just now";

        if (timeSpan.TotalMinutes < 60)
            return $"{(int)timeSpan.TotalMinutes} minute{(timeSpan.TotalMinutes >= 2 ? "s" : "")} ago";

        if (timeSpan.TotalHours < 24)
            return $"{(int)timeSpan.TotalHours} hour{(timeSpan.TotalHours >= 2 ? "s" : "")} ago";

        if (timeSpan.TotalDays < 30)
            return $"{(int)timeSpan.TotalDays} day{(timeSpan.TotalDays >= 2 ? "s" : "")} ago";

        return dateTime.ToString("yyyy-MM-dd");
    }

    /// <summary>
    /// Rounds a DateTime to the nearest specified interval.
    /// Useful for grouping lock operations by time buckets.
    /// </summary>
    public static DateTime RoundToNearest(this DateTime dateTime, TimeSpan interval)
    {
        long ticks = (long)Math.Round(dateTime.Ticks / (double)interval.Ticks);
        return new DateTime(ticks * interval.Ticks, dateTime.Kind);
    }

    /// <summary>
    /// Adds a random jitter to a TimeSpan.
    /// Useful for staggering lock renewals to avoid thundering herd problems.
    /// </summary>
    public static TimeSpan AddRandomJitter(this TimeSpan timeSpan, double maxJitterPercentage = 10)
    {
        var jitterMs = (long)(timeSpan.TotalMilliseconds * (maxJitterPercentage / 100));
        var random = new Random();
        var jitter = random.Next((int)-jitterMs, (int)jitterMs);
        return timeSpan.Add(TimeSpan.FromMilliseconds(jitter));
    }

    /// <summary>
    /// Converts Unix timestamp (seconds since epoch) to DateTime.
    /// </summary>
    public static DateTime FromUnixTimestamp(long seconds)
    {
        return DateTime.UnixEpoch.AddSeconds(seconds);
    }

    /// <summary>
    /// Converts DateTime to Unix timestamp (seconds since epoch).
    /// </summary>
    public static long ToUnixTimestamp(this DateTime dateTime)
    {
        return (long)dateTime.ToUniversalTime().Subtract(DateTime.UnixEpoch).TotalSeconds;
    }
}
