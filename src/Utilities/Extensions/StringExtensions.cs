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
    public static bool IsValidLockName(this string? lockName)
    {
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
    public static string SanitizeForLockName(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

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
    public static byte[] ToUtf8Bytes(this string? input)
    {
        return string.IsNullOrEmpty(input)
            ? []<byte>()
            : System.Text.Encoding.UTF8.GetBytes(input);
    }

    /// <summary>
    /// Converts a string to a byte array using ASCII encoding.
    /// </summary>
    public static byte[] ToAsciiBytes(this string? input)
    {
        return string.IsNullOrEmpty(input)
            ? []<byte>()
            : System.Text.Encoding.ASCII.GetBytes(input);
    }

    /// <summary>
    /// Converts a hex string to a byte array.
    /// Useful for deserializing fencing tokens and other binary data.
    /// </summary>
    public static byte[] FromHexString(this string? hexString)
    {
        if (string.IsNullOrEmpty(hexString) || hexString.Length % 2 != 0)
            return []<byte>();

        var bytes = new byte[hexString.Length / 2];
        for (int i = 0; i < hexString.Length; i += 2)
        {
            if (!byte.TryParse(hexString.Substring(i, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
                return []<byte>();
            bytes[i / 2] = b;
        }

        return bytes;
    }

    /// <summary>
    /// Checks if a string is a valid UUID/GUID format.
    /// </summary>
    public static bool IsValidGuid(this string? input)
    {
        return !string.IsNullOrEmpty(input) && Guid.TryParse(input, out _);
    }

    /// <summary>
    /// Truncates a string to a maximum length with ellipsis.
    /// </summary>
    public static string TruncateWithEllipsis(this string input, int maxLength)
    {
        if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
            return input;

        return input.Substring(0, Math.Max(0, maxLength - 3)) + "...";
    }

    /// <summary>
    /// Converts a delimited string to a list of trimmed strings.
    /// </summary>
    public static List<string> ToTrimmedList(this string? input, char delimiter = ',')
    {
        if (string.IsNullOrEmpty(input))
            return new List<string>();

        return input
            .Split(delimiter)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
    }
}
