#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =====================================================================

namespace SarmKadan.DistributedLock.Events;

using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Provides validation helpers for <see cref="LockEventSubscriber"/> instances.
/// </summary>
public static class LockEventSubscriberValidation
{
    /// <summary>
    /// Validates a <see cref="LockEventSubscriber"/> instance.
    /// </summary>
    /// <param name="value">The subscriber to validate.</param>
    /// <returns>A list of validation problems; empty if valid.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    public static IReadOnlyList<string> Validate(this LockEventSubscriber? value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = new List<string>();

        // Validate the logger field (cannot be null as constructor validates it)
        // This is checked at construction time, so we don't need to re-check here

        return problems.AsReadOnly();
    }

    /// <summary>
    /// Validates a <see cref="MetricsTrackingEventSubscriber"/> instance.
    /// </summary>
    /// <param name="value">The metrics subscriber to validate.</param>
    /// <returns>A list of validation problems; empty if valid.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    public static IReadOnlyList<string> Validate(this MetricsTrackingEventSubscriber? value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = new List<string>();

        var metrics = value.GetMetrics();

        if (metrics.Acquisitions < 0)
        {
            problems.Add(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Metrics.Acquisitions must be non-negative, but was {0}.",
                    metrics.Acquisitions));
        }

        if (metrics.Releases < 0)
        {
            problems.Add(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Metrics.Releases must be non-negative, but was {0}.",
                    metrics.Releases));
        }

        if (metrics.Failures < 0)
        {
            problems.Add(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Metrics.Failures must be non-negative, but was {0}.",
                    metrics.Failures));
        }

        if (metrics.ContentionEvents < 0)
        {
            problems.Add(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Metrics.ContentionEvents must be non-negative, but was {0}.",
                    metrics.ContentionEvents));
        }

        if (metrics.Timestamp == default)
        {
            problems.Add("Metrics.Timestamp must be set to a non-default DateTime value.");
        }
        else if (metrics.Timestamp.Kind != DateTimeKind.Utc)
        {
            problems.Add("Metrics.Timestamp must be in UTC kind.");
        }

        return problems.AsReadOnly();
    }

    /// <summary>
    /// Validates a <see cref="EventMetrics"/> instance.
    /// </summary>
    /// <param name="value">The metrics to validate.</param>
    /// <returns>A list of validation problems; empty if valid.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    public static IReadOnlyList<string> Validate(this EventMetrics? value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = new List<string>();

        if (value.Acquisitions < 0)
        {
            problems.Add(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Acquisitions must be non-negative, but was {0}.",
                    value.Acquisitions));
        }

        if (value.Releases < 0)
        {
            problems.Add(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Releases must be non-negative, but was {0}.",
                    value.Releases));
        }

        if (value.Failures < 0)
        {
            problems.Add(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Failures must be non-negative, but was {0}.",
                    value.Failures));
        }

        if (value.ContentionEvents < 0)
        {
            problems.Add(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "ContentionEvents must be non-negative, but was {0}.",
                    value.ContentionEvents));
        }

        if (value.Timestamp == default)
        {
            problems.Add("Timestamp must be set to a non-default DateTime value.");
        }
        else if (value.Timestamp.Kind != DateTimeKind.Utc)
        {
            problems.Add("Timestamp must be in UTC kind.");
        }

        return problems.AsReadOnly();
    }

    /// <summary>
    /// Determines whether the specified <see cref="LockEventSubscriber"/> is valid.
    /// </summary>
    /// <param name="value">The subscriber to check.</param>
    /// <returns><see langword="true"/> if valid; otherwise, <see langword="false"/>.</returns>
    public static bool IsValid(this LockEventSubscriber? value) => Validate(value).Count == 0;

    /// <summary>
    /// Determines whether the specified <see cref="MetricsTrackingEventSubscriber"/> is valid.
    /// </summary>
    /// <param name="value">The metrics subscriber to check.</param>
    /// <returns><see langword="true"/> if valid; otherwise, <see langword="false"/>.</returns>
    public static bool IsValid(this MetricsTrackingEventSubscriber? value) => Validate(value).Count == 0;

    /// <summary>
    /// Determines whether the specified <see cref="EventMetrics"/> is valid.
    /// </summary>
    /// <param name="value">The metrics to check.</param>
    /// <returns><see langword="true"/> if valid; otherwise, <see langword="false"/>.</returns>
    public static bool IsValid(this EventMetrics? value) => Validate(value).Count == 0;

    /// <summary>
    /// Ensures that the specified <see cref="LockEventSubscriber"/> is valid, throwing an <see cref="ArgumentException"/> if not.
    /// </summary>
    /// <param name="value">The subscriber to validate.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="value"/> is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    public static void EnsureValid(this LockEventSubscriber? value)
    {
        var problems = Validate(value);
        if (problems.Count > 0)
        {
            throw new ArgumentException(
                "LockEventSubscriber is invalid. " + string.Join(" ", problems),
                nameof(value));
        }
    }

    /// <summary>
    /// Ensures that the specified <see cref="MetricsTrackingEventSubscriber"/> is valid, throwing an <see cref="ArgumentException"/> if not.
    /// </summary>
    /// <param name="value">The metrics subscriber to validate.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="value"/> is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    public static void EnsureValid(this MetricsTrackingEventSubscriber? value)
    {
        var problems = Validate(value);
        if (problems.Count > 0)
        {
            throw new ArgumentException(
                "MetricsTrackingEventSubscriber is invalid. " + string.Join(" ", problems),
                nameof(value));
        }
    }

    /// <summary>
    /// Ensures that the specified <see cref="EventMetrics"/> is valid, throwing an <see cref="ArgumentException"/> if not.
    /// </summary>
    /// <param name="value">The metrics to validate.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="value"/> is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    public static void EnsureValid(this EventMetrics? value)
    {
        var problems = Validate(value);
        if (problems.Count > 0)
        {
            throw new ArgumentException(
                "EventMetrics is invalid. " + string.Join(" ", problems),
                nameof(value));
        }
    }
}
