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
    public string Token { get; }
    public long SequenceNumber { get; }
    public DateTime IssuedAt { get; }

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

    // Creates a new fencing token with an incremented sequence number
    public FencingToken IncrementSequence()
    {
        return new FencingToken(
            GenerateNewToken(),
            SequenceNumber + 1,
            DateTime.UtcNow
        );
    }

    // Validates that this token is greater than another token
    public bool IsGreaterThan(FencingToken other)
    {
        return other == null || SequenceNumber > other.SequenceNumber;
    }

    // Validates that this token is valid and not expired
    public bool IsValid(TimeSpan tokenLifetime)
    {
        var age = DateTime.UtcNow - IssuedAt;
        return age < tokenLifetime;
    }

    public override bool Equals(object? obj) => Equals(obj as FencingToken);

    public bool Equals(FencingToken? other)
    {
        if (other is null) return false;
        return Token == other.Token && SequenceNumber == other.SequenceNumber;
    }

    public override int GetHashCode() => HashCode.Combine(Token, SequenceNumber);

    public int CompareTo(FencingToken? other)
    {
        if (other is null) return 1;
        return SequenceNumber.CompareTo(other.SequenceNumber);
    }

    public override string ToString() => $"{Token}:{SequenceNumber}";

    private static string GenerateNewToken()
    {
        return Guid.NewGuid().ToString("N")[..16];
    }
}
