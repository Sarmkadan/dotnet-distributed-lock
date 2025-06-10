#nullable enable

// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Formatters;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Provides extension methods for <see cref="JsonLockSerializer"/> to enable fluent JSON serialization/deserialization.
/// </summary>
public static class JsonLockSerializerJsonExtensions
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            new UtcDateTimeConverter()
        }
    };

    /// <summary>
    /// Serializes the <see cref="JsonLockSerializer"/> instance to a JSON string.
    /// </summary>
    /// <param name="value">The serializer instance to serialize.</param>
    /// <param name="indented">Whether to format the JSON with indentation for readability.</param>
    /// <returns>A JSON string representation of the serializer.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public static string ToJson(this JsonLockSerializer value, bool indented = false)
    {
        ArgumentNullException.ThrowIfNull(value);

        var options = indented
            ? new JsonSerializerOptions(_options) { WriteIndented = true }
            : _options;

        return JsonSerializer.Serialize(value, options);
    }

    /// <summary>
    /// Deserializes a JSON string to a <see cref="JsonLockSerializer"/> instance.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>A deserialized <see cref="JsonLockSerializer"/> instance, or null if the JSON is null or whitespace.</returns>
    /// <exception cref="JsonException">Thrown when the JSON is malformed.</exception>
    public static JsonLockSerializer? FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<JsonLockSerializer>(json, _options);
        }
        catch (JsonException ex)
        {
            throw new JsonException("Failed to deserialize JsonLockSerializer from JSON", ex);
        }
    }

    /// <summary>
    /// Attempts to deserialize a JSON string to a <see cref="JsonLockSerializer"/> instance.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="value">Receives the deserialized instance if successful, otherwise null.</param>
    /// <returns>True if deserialization succeeded; otherwise, false.</returns>
    public static bool TryFromJson(string json, out JsonLockSerializer? value)
    {
        value = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            value = JsonSerializer.Deserialize<JsonLockSerializer>(json, _options);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}