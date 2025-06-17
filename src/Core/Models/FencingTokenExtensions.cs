#nullable enable

using System.Diagnostics.CodeAnalysis;

namespace SarmKadan.DistributedLock.Models;

/// <summary>
/// Provides extension methods for <see cref="FencingToken"/> to enable common operations
/// such as parsing, validation, conversion, and comparison.
/// </summary>
public static class FencingTokenExtensions
{
    /// <summary>
    /// Parses a fencing token from its string representation.
    /// </summary>
    /// <param name="tokenString">The string representation of the fencing token in format "{Token}:{SequenceNumber}".</param>
    /// <returns>A parsed <see cref="FencingToken"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tokenString"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="tokenString"/> is not in the correct format.</exception>
    public static FencingToken Parse(string tokenString)
    {
        ArgumentNullException.ThrowIfNull(tokenString);

        var parts = tokenString.Split(':', 2);
        if (parts.Length != 2)
        {
            throw new ArgumentException(
                "Token string must be in format '{Token}:{SequenceNumber}'.",
                nameof(tokenString));
        }

        if (!long.TryParse(parts[1], out var sequenceNumber))
        {
            throw new ArgumentException(
                "Sequence number must be a valid long integer.",
                nameof(tokenString));
        }

        return new FencingToken(parts[0], sequenceNumber);
    }

    /// <summary>
    /// Attempts to parse a fencing token from its string representation.
    /// </summary>
    /// <param name="tokenString">The string representation of the fencing token.</param>
    /// <param name="token">When this method returns, contains the parsed token if successful, or null if parsing failed.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParse(string tokenString, [NotNullWhen(true)] out FencingToken? token)
    {
        token = null;

        if (string.IsNullOrWhiteSpace(tokenString))
        {
            return false;
        }

        var parts = tokenString.Split(':', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        if (!long.TryParse(parts[1], out var sequenceNumber))
        {
            return false;
        }

        token = new FencingToken(parts[0], sequenceNumber);
        return true;
    }

    /// <summary>
    /// Converts a fencing token to its string representation.
    /// </summary>
    /// <param name="token">The fencing token to convert.</param>
    /// <returns>The string representation of the token.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="token"/> is null.</exception>
    public static string ToTokenString(this FencingToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return token.ToString();
    }

    /// <summary>
    /// Gets the age of the fencing token as a <see cref="TimeSpan"/>.
    /// </summary>
    /// <param name="token">The fencing token.</param>
    /// <returns>The age of the token since it was issued.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="token"/> is null.</exception>
    public static TimeSpan GetAge(this FencingToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return DateTime.UtcNow - token.IssuedAt;
    }

    /// <summary>
    /// Determines whether this token is less than another token.
    /// </summary>
    /// <param name="token">The current token.</param>
    /// <param name="other">The other token to compare with.</param>
    /// <returns>True if this token's sequence number is less than the other; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="token"/> or <paramref name="other"/> is null.</exception>
    public static bool IsLessThan(this FencingToken token, FencingToken other)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(other);
        return token.SequenceNumber < other.SequenceNumber;
    }

    /// <summary>
    /// Determines whether this token is greater than or equal to another token.
    /// </summary>
    /// <param name="token">The current token.</param>
    /// <param name="other">The other token to compare with.</param>
    /// <returns>True if this token's sequence number is greater than or equal to the other; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="token"/> or <paramref name="other"/> is null.</exception>
    public static bool IsGreaterThanOrEqual(this FencingToken token, FencingToken other)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(other);
        return token.SequenceNumber >= other.SequenceNumber;
    }

    /// <summary>
    /// Determines whether this token is less than or equal to another token.
    /// </summary>
    /// <param name="token">The current token.</param>
    /// <param name="other">The other token to compare with.</param>
    /// <returns>True if this token's sequence number is less than or equal to the other; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="token"/> or <paramref name="other"/> is null.</exception>
    public static bool IsLessThanOrEqual(this FencingToken token, FencingToken other)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(other);
        return token.SequenceNumber <= other.SequenceNumber;
    }

    /// <summary>
    /// Creates a new fencing token with the same token value but a new sequence number.
    /// </summary>
    /// <param name="token">The original token.</param>
    /// <param name="newSequenceNumber">The new sequence number to use.</param>
    /// <returns>A new <see cref="FencingToken"/> with the same token value but updated sequence number.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="token"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="newSequenceNumber"/> is negative.</exception>
    public static FencingToken WithSequenceNumber(this FencingToken token, long newSequenceNumber)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentOutOfRangeException.ThrowIfNegative(newSequenceNumber);

        return new FencingToken(token.Token, newSequenceNumber, token.IssuedAt);
    }

    /// <summary>
    /// Creates a new fencing token with the same sequence number but a new token value.
    /// </summary>
    /// <param name="token">The original token.</param>
    /// <returns>A new <see cref="FencingToken"/> with a new token value but same sequence number and issuance time.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="token"/> is null.</exception>
    public static FencingToken WithNewToken(this FencingToken token)
    {
        ArgumentNullException.ThrowIfNull(token);

        return new FencingToken(
            GenerateNewToken(),
            token.SequenceNumber,
            token.IssuedAt
        );
    }

    /// <summary>
    /// Determines whether two fencing tokens are adjacent in sequence (one immediately follows the other).
    /// </summary>
    /// <param name="token">The current token.</param>
    /// <param name="other">The other token to compare with.</param>
    /// <returns>True if the tokens are adjacent; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="token"/> or <paramref name="other"/> is null.</exception>
    public static bool IsAdjacentTo(this FencingToken token, FencingToken other)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(other);

        return Math.Abs(token.SequenceNumber - other.SequenceNumber) == 1;
    }

    /// <summary>
    /// Gets the difference between the sequence numbers of two tokens.
    /// </summary>
    /// <param name="token">The current token.</param>
    /// <param name="other">The other token to compare with.</param>
    /// <returns>The difference between sequence numbers (current - other).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="token"/> or <paramref name="other"/> is null.</exception>
    public static long SequenceDifference(this FencingToken token, FencingToken other)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(other);

        return token.SequenceNumber - other.SequenceNumber;
    }

    private static string GenerateNewToken() => Guid.NewGuid().ToString("N")[..16];
}