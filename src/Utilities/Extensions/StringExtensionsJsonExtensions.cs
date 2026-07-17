#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using System.Text.Json;

namespace SarmKadan.DistributedLock.Utilities.Extensions;

/// <summary>
/// Provides System.Text.Json serialization and deserialization extensions for strings
/// that are processed by <see cref="StringExtensions"/> methods.
/// Includes utilities for serializing lock names, validation results, and other
/// string-based data structures used with distributed locks.
/// </summary>
public static class StringExtensionsJsonExtensions
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static JsonSerializerOptions GetOptions(bool indented)
    {
        var options = new JsonSerializerOptions(_jsonSerializerOptions)
        {
            WriteIndented = indented
        };
        return options;
    }

    /// <summary>
    /// Serializes a lock name string to JSON format.
    /// </summary>
    /// <param name="lockName">The lock name to serialize.</param>
    /// <param name="indented">Whether to format the JSON with indentation for readability.</param>
    /// <returns>A JSON string representation of the lock name.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="lockName"/> is null or empty.</exception>
    public static string ToJson(this string lockName, bool indented = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(lockName);

        var data = new { LockName = lockName, IsValid = lockName.IsValidLockName(), Sanitized = lockName.SanitizeForLockName() };
        return JsonSerializer.Serialize(data, GetOptions(indented));
    }

    /// <summary>
    /// Serializes a collection of lock names to JSON format.
    /// </summary>
    /// <param name="lockNames">The collection of lock names to serialize.</param>
    /// <param name="indented">Whether to format the JSON with indentation for readability.</param>
    /// <returns>A JSON string representation of the lock names collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lockNames"/> is null.</exception>
    public static string ToJson(this IEnumerable<string> lockNames, bool indented = false)
    {
        ArgumentNullException.ThrowIfNull(lockNames);

        var data = lockNames.Select(name => new { LockName = name, IsValid = name.IsValidLockName(), Sanitized = name.SanitizeForLockName() }).ToList();
        return JsonSerializer.Serialize(data, GetOptions(indented));
    }

    /// <summary>
    /// Deserializes a JSON string to a lock name.
    /// Returns null if the JSON is null, empty, or whitespace.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized lock name string, or null if JSON is null, empty, whitespace, or invalid.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> is null.</exception>
    /// <exception cref="JsonException">Thrown when the JSON is malformed.</exception>
    public static string? FromLockNameJson(string? json)
    {
        ArgumentNullException.ThrowIfNull(json);

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var data = JsonSerializer.Deserialize<LockNameData>(json, _jsonSerializerOptions);
            return data?.LockName;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Attempts to deserialize a JSON string to a lock name.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="lockName">Receives the deserialized lock name if successful.</param>
    /// <returns>True if deserialization succeeded; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> is null.</exception>
    public static bool TryFromLockNameJson(string? json, out string? lockName)
    {
        lockName = null;

        ArgumentNullException.ThrowIfNull(json);

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            var data = JsonSerializer.Deserialize<LockNameData>(json, _jsonSerializerOptions);
            if (data?.LockName is not null)
            {
                lockName = data.LockName;
                return true;
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record LockNameData(string LockName, bool IsValid, string Sanitized);
}