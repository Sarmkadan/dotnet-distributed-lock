#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Models;

/// <summary>
/// Represents a fencing token that prevents zombie processes from writing to shared resources.
/// Fencing tokens are monotonically increasing and ensure that only the current lock holder can proceed.
/// </summary>
public class FencingToken : IEquatable<FencingToken>, IComparable<FencingToken>
{
    /// <summary>
    /// The string representation of the fencing token.
    /// </summary>
    public string Token { get; }
    
    /// <summary>
    /// The sequence number of the fencing token.
    /// </summary>
    public long SequenceNumber { get; }
    
    /// <summary>
    /// The timestamp when the fencing token was issued.
    /// </summary>
    public DateTime IssuedAt { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FencingToken"/> class.
    /// </summary>
    /// <param name="token">The token string.</param>
    /// <param name="sequenceNumber">The sequence number.</param>
    /// <param name="issuedAt">The issuance timestamp.</param>
    public FencingToken(string token, long sequenceNumber, DateTime? issuedAt = null)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token cannot be null or empty.", nameof(token));

        if (sequenceNumber < 0)
            throw new ArgumentException("Sequence number cannot be negative.", nameof(sequenceNumber));

        Token = token;
        SequenceNumber = sequenceNumber;
        IssuedAt = issuedAt ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a new fencing token with an incremented sequence number.
    /// </summary>
    /// <returns>A new <see cref="FencingToken"/> with an incremented sequence number.</returns>
    public FencingToken IncrementSequence()
    {
        return new FencingToken(
            GenerateNewToken(),
            SequenceNumber + 1,
            DateTime.UtcNow
        );
    }

    /// <summary>
    /// Validates that this token is greater than another token.
    /// </summary>
    /// <param name="other">The other token to compare.</param>
    /// <returns>True if this token's sequence number is greater than the other; otherwise, false.</returns>
    public bool IsGreaterThan(FencingToken other)
    {
        return other is null || SequenceNumber > other.SequenceNumber;
    }

    /// <summary>
    /// Validates that this token is valid and not expired.
    /// </summary>
    /// <param name="tokenLifetime">The maximum lifetime of the token.</param>
    /// <returns>True if the token is valid; otherwise, false.</returns>
    public bool IsValid(TimeSpan tokenLifetime)
    {
        var age = DateTime.UtcNow - IssuedAt;
        return age < tokenLifetime;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as FencingToken);

    /// <inheritdoc/>
    public bool Equals(FencingToken? other)
    {
        if (other is null) return false;
        return Token == other.Token && SequenceNumber == other.SequenceNumber;
    }

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Token, SequenceNumber);

    /// <inheritdoc/>
    public int CompareTo(FencingToken? other)
    {
        if (other is null) return 1;
        return SequenceNumber.CompareTo(other.SequenceNumber);
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Token}:{SequenceNumber}";

    private static string GenerateNewToken()
    {
        return Guid.NewGuid().ToString("N")[..16];
    }
}
