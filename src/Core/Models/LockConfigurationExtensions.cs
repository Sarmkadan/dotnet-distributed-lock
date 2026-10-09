#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Models;

/// <summary>
/// Helper extensions for <see cref="LockConfiguration"/> that create modified copies
/// without mutating the original instance. Validation helpers live in
/// <see cref="LockConfigurationValidation"/>.
/// </summary>
public static class LockConfigurationExtensions
{
    /// <summary>
    /// Creates a shallow copy of the configuration with all property values preserved.
    /// The returned instance is independent: changing its properties does not affect the source.
    /// </summary>
    /// <param name="source">The configuration to copy.</param>
    /// <returns>A new <see cref="LockConfiguration"/> with the same values as <paramref name="source"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    public static LockConfiguration Clone(this LockConfiguration source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new LockConfiguration
        {
            LockKey = source.LockKey,
            LockDuration = source.LockDuration,
            AcquisitionTimeout = source.AcquisitionTimeout,
            AcquisitionMode = source.AcquisitionMode,
            MaxRetries = source.MaxRetries,
            RetryInterval = source.RetryInterval,
            RenewalInterval = source.RenewalInterval,
            AutoRenewal = source.AutoRenewal,
            UseFencingToken = source.UseFencingToken,
            Metadata = source.Metadata
        };
    }

    /// <summary>
    /// Creates a copy of the configuration with a different lock key.
    /// </summary>
    /// <param name="source">The configuration to copy.</param>
    /// <param name="lockKey">The lock key for the new configuration. Must not be null, empty, or whitespace.</param>
    /// <returns>A new <see cref="LockConfiguration"/> with <paramref name="lockKey"/> applied.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="lockKey"/> is null, empty, or whitespace.</exception>
    public static LockConfiguration WithLockKey(this LockConfiguration source, string lockKey)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (string.IsNullOrWhiteSpace(lockKey))
            throw new ArgumentException("Lock key cannot be null or empty.", nameof(lockKey));

        var copy = source.Clone();
        copy.LockKey = lockKey;
        return copy;
    }

    /// <summary>
    /// Creates a copy of the configuration with a different lock duration.
    /// The duration is not range-checked here; use <see cref="LockConfigurationValidation.IsValid(LockConfiguration?)"/>
    /// or <see cref="LockConfigurationValidation.EnsureValid(LockConfiguration?)"/> to verify the result.
    /// </summary>
    /// <param name="source">The configuration to copy.</param>
    /// <param name="lockDuration">The lock duration for the new configuration.</param>
    /// <returns>A new <see cref="LockConfiguration"/> with <paramref name="lockDuration"/> applied.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    public static LockConfiguration WithLockDuration(this LockConfiguration source, TimeSpan lockDuration)
    {
        ArgumentNullException.ThrowIfNull(source);

        var copy = source.Clone();
        copy.LockDuration = lockDuration;
        return copy;
    }

    /// <summary>
    /// Creates a copy of the configuration with auto-renewal disabled.
    /// Useful for short-lived locks where the renewal background task is not needed.
    /// </summary>
    /// <param name="source">The configuration to copy.</param>
    /// <returns>A new <see cref="LockConfiguration"/> with <see cref="LockConfiguration.AutoRenewal"/> set to false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    public static LockConfiguration WithoutAutoRenewal(this LockConfiguration source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var copy = source.Clone();
        copy.AutoRenewal = false;
        return copy;
    }
}
