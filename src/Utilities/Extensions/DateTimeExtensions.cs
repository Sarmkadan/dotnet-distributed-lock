#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using System;

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
    /// <param name="expiresAt">The expiration DateTime to check.</param>
    /// <returns><see langword="true"/> if the lock has expired; otherwise, <see langword="false"/>.</returns>
    public static bool IsExpired(this DateTime expiresAt)
    {
        return DateTime.UtcNow >= expiresAt;
    }

    /// <summary>
    /// Determines if a lock is still valid (not expired).
    /// </summary>
    /// <param name="expiresAt">The expiration DateTime to check.</param>
    /// <returns><see langword="true"/> if the lock is still valid; otherwise, <see langword="false"/>.</returns>
    public static bool IsValid(this DateTime expiresAt)
    {
        return DateTime.UtcNow < expiresAt;
    }

    /// <summary>
    /// Calculates the remaining time until a lock expires.
    /// Returns TimeSpan.Zero if already expired.
    /// </summary>
    /// <param name="expiresAt">The expiration DateTime to check.</param>
    /// <returns>The remaining time until expiration, or TimeSpan.Zero if already expired.</returns>
    public static TimeSpan GetRemainingTime(this DateTime expiresAt)
    {
        var remaining = expiresAt - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>
    /// Calculates the remaining seconds until a lock expires.
    /// Returns 0 if already expired.
    /// </summary>
    /// <param name="expiresAt">The expiration DateTime to check.</param>
    /// <returns>The remaining seconds until expiration, or 0 if already expired.</returns>
    public static long GetRemainingSeconds(this DateTime expiresAt)
    {
        return (long)expiresAt.GetRemainingTime().TotalSeconds;
    }

    /// <summary>
    /// Calculates the remaining milliseconds until a lock expires.
    /// Returns 0 if already expired.
    /// </summary>
    /// <param name="expiresAt">The expiration DateTime to check.</param>
    /// <returns>The remaining milliseconds until expiration, or 0 if already expired.</returns>
    public static long GetRemainingMilliseconds(this DateTime expiresAt)
    {
        return (long)expiresAt.GetRemainingTime().TotalMilliseconds;
    }

    /// <summary>
    /// Determines if a lock will expire within a specified grace period.
    /// Useful for proactive renewal logic.
    /// </summary>
    /// <param name="expiresAt">The expiration DateTime to check.</param>
    /// <param name="gracePeriod">The grace period TimeSpan to check within.</param>
    /// <returns><see langword="true"/> if the lock will expire within the grace period; otherwise, <see langword="false"/>.</returns>
    public static bool ExpiresWithin(this DateTime expiresAt, TimeSpan gracePeriod)
    {
        var remaining = expiresAt.GetRemainingTime();
        return remaining > TimeSpan.Zero && remaining <= gracePeriod;
    }

    /// <summary>
    /// Formats a DateTime as an ISO 8601 string with UTC timezone.
    /// </summary>
    /// <param name="dateTime">The DateTime to format.</param>
    /// <returns>An ISO 8601 formatted string.</returns>
    public static string ToIso8601String(this DateTime dateTime)
    {
        return dateTime.ToUniversalTime().ToString("O");
    }

    /// <summary>
    /// Formats a DateTime in a human-readable format.
    /// Example: "2 hours 30 minutes ago"
    /// </summary>
    /// <param name="dateTime">The DateTime to format.</param>
    /// <returns>A human-readable string representation of the DateTime.</returns>
    public static string ToHumanReadableFormat(this DateTime dateTime)
    {
        var utcDateTime = dateTime.ToUniversalTime();
        var timeSpan = DateTime.UtcNow - utcDateTime;

        if (timeSpan.TotalSeconds < 60)
            return "Just now";

        if (timeSpan.TotalMinutes < 60)
            return $"{(int)timeSpan.TotalMinutes} minute{(timeSpan.TotalMinutes >= 2 ? "s" : "")} ago";

        if (timeSpan.TotalHours < 24)
            return $"{(int)timeSpan.TotalHours} hour{(timeSpan.TotalHours >= 2 ? "s" : "")} ago";

        if (timeSpan.TotalDays < 30)
            return $"{(int)timeSpan.TotalDays} day{(timeSpan.TotalDays >= 2 ? "s" : "")} ago";

        return dateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Rounds a DateTime to the nearest specified interval.
    /// Useful for grouping lock operations by time buckets.
    /// </summary>
    /// <param name="dateTime">The DateTime to round.</param>
    /// <param name="interval">The TimeSpan interval to round to.</param>
    /// <returns>A DateTime rounded to the nearest interval.</returns>
    public static DateTime RoundToNearest(this DateTime dateTime, TimeSpan interval)
    {
        long ticks = (long)Math.Round(dateTime.Ticks / (double)interval.Ticks);
        return new DateTime(ticks * interval.Ticks, dateTime.Kind);
    }

    /// <summary>
    /// Adds a random jitter to a TimeSpan.
    /// Useful for staggering lock renewals to avoid thundering herd problems.
    /// </summary>
    /// <param name="timeSpan">The TimeSpan to add jitter to.</param>
    /// <param name="maxJitterPercentage">Maximum jitter as percentage of the TimeSpan (default: 10%).</param>
    /// <returns>A new TimeSpan with added random jitter.</returns>
    public static TimeSpan AddRandomJitter(this TimeSpan timeSpan, double maxJitterPercentage = 10)
    {
        if (maxJitterPercentage < 0 || maxJitterPercentage > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maxJitterPercentage), "Jitter percentage must be between 0 and 100.");
        }

        var jitterMs = (long)(timeSpan.TotalMilliseconds * (maxJitterPercentage / 100));
        var random = new Random();
        var jitter = random.Next((int)Math.Max(int.MinValue, -jitterMs), (int)Math.Min(int.MaxValue, jitterMs));
        return timeSpan.Add(TimeSpan.FromMilliseconds(jitter));
    }

    /// <summary>
    /// Converts Unix timestamp (seconds since epoch) to DateTime.
    /// </summary>
    /// <param name="seconds">Unix timestamp in seconds.</param>
    /// <returns>A DateTime representing the Unix timestamp.</returns>
    public static DateTime FromUnixTimestamp(long seconds)
    {
        return DateTime.UnixEpoch.AddSeconds(seconds);
    }

    /// <summary>
    /// Converts DateTime to Unix timestamp (seconds since epoch).
    /// </summary>
    /// <param name="dateTime">The DateTime to convert.</param>
    /// <returns>Unix timestamp in seconds.</returns>
    public static long ToUnixTimestamp(this DateTime dateTime)
    {
        return (long)dateTime.ToUniversalTime().Subtract(DateTime.UnixEpoch).TotalSeconds;
    }
}