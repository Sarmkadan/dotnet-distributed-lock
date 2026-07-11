#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Utilities.Extensions;

/// <summary>
/// Extension methods for string manipulation and validation.
/// Provides common operations for lock name validation, formatting, and encoding.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Validates if a string is a valid lock name.
    /// Lock names must be non-empty, alphanumeric with hyphens/underscores, and max 256 chars.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lockName"/> is <see langword="null"/>.</exception>
    public static bool IsValidLockName(this string? lockName)
    {
        ArgumentNullException.ThrowIfNull(lockName);

        if (string.IsNullOrWhiteSpace(lockName))
            return false;

        if (lockName.Length > 256)
            return false;

        return lockName.All(c =>
            char.IsLetterOrDigit(c) ||
            c == '-' || c == '_' || c == '.' || c == ':');
    }

    /// <summary>
    /// Sanitizes a string for use as a lock name by removing invalid characters.
    /// Replaces spaces with underscores and removes special characters.
    /// </summary>
    /// <param name="input">The string to sanitize.</param>
    /// <returns>The sanitized string, truncated to 256 characters if necessary.</returns>
    public static string SanitizeForLockName(this string input)
    {
        ArgumentException.ThrowIfNullOrEmpty(input);

        var sanitized = System.Text.RegularExpressions.Regex.Replace(
            input.Trim(),
            @"[^a-zA-Z0-9\-_\.\:]",
            "_");

        return sanitized.Length > 256
            ? sanitized.Substring(0, 256)
            : sanitized;
    }

    /// <summary>
    /// Converts a string to a byte array using UTF-8 encoding.
    /// Useful for hashing and cryptographic operations.
    /// </summary>
    /// <param name="input">The string to convert. Can be null or empty.</param>
    /// <returns>A byte array containing the UTF-8 encoded bytes of the string, or empty array if input is null or empty.</returns>
    public static byte[] ToUtf8Bytes(this string? input)
    {
        return string.IsNullOrEmpty(input)
            ? Array.Empty<byte>()
            : System.Text.Encoding.UTF8.GetBytes(input);
    }

    /// <summary>
    /// Converts a string to a byte array using ASCII encoding.
    /// </summary>
    /// <param name="input">The string to convert. Can be null or empty.</param>
    /// <returns>A byte array containing the ASCII encoded bytes of the string, or empty array if input is null or empty.</returns>
    public static byte[] ToAsciiBytes(this string? input)
    {
        return string.IsNullOrEmpty(input)
            ? Array.Empty<byte>()
            : System.Text.Encoding.ASCII.GetBytes(input);
    }

    /// <summary>
    /// Converts a hex string to a byte array.
    /// Useful for deserializing fencing tokens and other binary data.
    /// </summary>
    /// <param name="hexString">The hexadecimal string to convert. Can be null or empty, or have odd length.</param>
    /// <returns>A byte array containing the parsed bytes, or empty array if input is invalid.</returns>
    public static byte[] FromHexString(this string? hexString)
    {
        if (string.IsNullOrEmpty(hexString) || hexString.Length % 2 != 0)
            return Array.Empty<byte>();

        var bytes = new byte[hexString.Length / 2];
        for (int i = 0; i < hexString.Length; i += 2)
        {
            if (!byte.TryParse(hexString.AsSpan(i, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
                return Array.Empty<byte>();
            bytes[i / 2] = b;
        }

        return bytes;
    }

    /// <summary>
    /// Checks if a string is a valid UUID/GUID format.
    /// </summary>
    /// <param name="input">The string to check. Can be null or empty.</param>
    /// <returns><see langword="true"/> if the string is a valid GUID; otherwise, <see langword="false"/>.</returns>
    public static bool IsValidGuid(this string? input)
    {
        return !string.IsNullOrEmpty(input) && Guid.TryParse(input, out _);
    }

    /// <summary>
    /// Truncates a string to a maximum length with ellipsis.
    /// </summary>
    /// <param name="input">The string to truncate.</param>
    /// <param name="maxLength">The maximum length including ellipsis. Must be positive.</param>
    /// <returns>The truncated string with ellipsis, or the original string if it fits within maxLength.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxLength"/> is less than 0.</exception>
    public static string TruncateWithEllipsis(this string input, int maxLength)
    {
        ArgumentException.ThrowIfNullOrEmpty(input);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        if (input.Length <= maxLength)
            return input;

        return input[..Math.Max(0, maxLength - 3)] + "...";
    }

    /// <summary>
    /// Converts a delimited string to a list of trimmed strings.
    /// </summary>
    /// <param name="input">The delimited string. Can be null or empty.</param>
    /// <param name="delimiter">The delimiter character. Defaults to comma.</param>
    /// <returns>A list of non-empty, trimmed strings from the input.</returns>
    public static List<string> ToTrimmedList(this string? input, char delimiter = ',')
    {
        if (string.IsNullOrEmpty(input))
            return [];

        return input
            .Split(delimiter)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
    }
}