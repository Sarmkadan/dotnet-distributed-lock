#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock.Models;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace SarmKadan.DistributedLock.Services;

/// <summary>
/// Generates cryptographically strong fencing tokens with monotonically increasing
/// sequence numbers, suitable for distributed environments where token uniqueness
/// and ordering guarantees are critical.
/// </summary>
public sealed class FencingTokenGenerator
{
    private readonly ConcurrentDictionary<string, long> _sequences = new();
    private readonly ILogger<FencingTokenGenerator> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="FencingTokenGenerator"/>.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public FencingTokenGenerator(ILogger<FencingTokenGenerator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates a new fencing token for the specified resource, incrementing its
    /// sequence counter atomically.
    /// </summary>
    /// <param name="resourceKey">The resource identifier to generate a token for.</param>
    /// <returns>A new <see cref="FencingToken"/> with an incremented sequence number.</returns>
    public FencingToken Generate(string resourceKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceKey);

        var sequence = _sequences.AddOrUpdate(resourceKey, _ => 1L, (_, current) => current + 1);
        var tokenString = GenerateCryptoToken();
        var token = new FencingToken(tokenString, sequence);

        _logger.LogDebug(
            "Generated fencing token for {ResourceKey}: sequence={Sequence}",
            resourceKey, sequence);

        return token;
    }

    /// <summary>
    /// Generates a batch of sequential fencing tokens for a resource.
    /// Useful for pre-allocating tokens in high-throughput scenarios.
    /// </summary>
    /// <param name="resourceKey">The resource identifier.</param>
    /// <param name="count">Number of tokens to generate.</param>
    /// <returns>A list of sequentially ordered fencing tokens.</returns>
    public IReadOnlyList<FencingToken> GenerateBatch(string resourceKey, int count)
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var tokens = new List<FencingToken>(count);

        for (int i = 0; i < count; i++)
        {
            tokens.Add(Generate(resourceKey));
        }

        _logger.LogDebug("Generated {Count} fencing tokens for {ResourceKey}", count, resourceKey);
        return tokens;
    }

    /// <summary>
    /// Returns the current sequence number for a resource without incrementing it.
    /// </summary>
    /// <param name="resourceKey">The resource identifier.</param>
    /// <returns>The current sequence number, or 0 if no token has been generated yet.</returns>
    public long GetCurrentSequence(string resourceKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceKey);
        return _sequences.GetValueOrDefault(resourceKey, 0L);
    }

    /// <summary>
    /// Validates that a token's sequence number is at least as recent as the current
    /// sequence for its resource.
    /// </summary>
    /// <param name="resourceKey">The resource identifier.</param>
    /// <param name="token">The fencing token to validate.</param>
    /// <returns>True if the token is current or newer; false if it is stale.</returns>
    public bool IsTokenCurrent(string resourceKey, FencingToken token)
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceKey);
        ArgumentNullException.ThrowIfNull(token);

        var currentSequence = GetCurrentSequence(resourceKey);
        return token.SequenceNumber >= currentSequence;
    }

    /// <summary>
    /// Resets the sequence counter for a resource back to zero. Typically used in testing.
    /// </summary>
    /// <param name="resourceKey">The resource identifier to reset.</param>
    public void ResetSequence(string resourceKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceKey);
        _sequences.TryRemove(resourceKey, out _);
        _logger.LogDebug("Reset fencing token sequence for {ResourceKey}", resourceKey);
    }

    private static string GenerateCryptoToken()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
