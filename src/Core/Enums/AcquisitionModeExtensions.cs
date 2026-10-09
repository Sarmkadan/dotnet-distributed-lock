#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Enums;

/// <summary>
/// Extension methods for <see cref="AcquisitionMode"/>.
/// </summary>
public static class AcquisitionModeExtensions
{
    /// <summary>
    /// Returns a human-readable display name for the specified acquisition mode.
    /// </summary>
    /// <param name="mode">The acquisition mode to describe.</param>
    /// <returns>A display name suitable for logs, dashboards, or UI labels.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="mode"/> is not a defined <see cref="AcquisitionMode"/> value.
    /// </exception>
    public static string GetDisplayName(this AcquisitionMode mode) => mode switch
    {
        AcquisitionMode.NonBlocking => "Non-Blocking",
        AcquisitionMode.Blocking => "Blocking",
        AcquisitionMode.ExponentialBackoff => "Exponential Backoff",
        AcquisitionMode.LinearBackoff => "Linear Backoff",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown acquisition mode.")
    };
}
