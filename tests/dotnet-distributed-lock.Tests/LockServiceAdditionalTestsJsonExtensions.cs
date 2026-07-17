#nullable enable

// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =====================================================================

namespace SarmKadan.DistributedLock.Tests;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Provides extension methods for <see cref="LockServiceAdditionalTests"/> to enable fluent JSON serialization/deserialization.
/// </summary>
public static class LockServiceAdditionalTestsJsonExtensions
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    /// <summary>
    /// Serializes the <see cref="LockServiceAdditionalTests"/> instance to a JSON string.
    /// </summary>
    /// <param name="value">The lock service additional tests instance to serialize.</param>
    /// <param name="indented">Whether to format the JSON with indentation for readability.</param>
    /// <returns>A JSON string representation of the instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public static string ToJson(this LockServiceAdditionalTests value, bool indented = false) =>
        JsonSerializer.Serialize(value, indented ? new JsonSerializerOptions(_options) { WriteIndented = true } : _options);

    /// <summary>
    /// Deserializes a JSON string to a <see cref="LockServiceAdditionalTests"/> instance.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>A deserialized <see cref="LockServiceAdditionalTests"/> instance, or null if the JSON is null or whitespace.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> is null.</exception>
    /// <exception cref="JsonException">Thrown when the JSON is malformed or cannot be deserialized to <see cref="LockServiceAdditionalTests"/>.</exception>
    public static LockServiceAdditionalTests? FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<LockServiceAdditionalTests>(json, _options);
    }

    /// <summary>
    /// Attempts to deserialize a JSON string to a <see cref="LockServiceAdditionalTests"/> instance.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="value">Receives the deserialized instance if successful, otherwise null.</param>
    /// <returns>True if deserialization succeeded; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> is null.</exception>
    public static bool TryFromJson(string json, out LockServiceAdditionalTests? value)
    {
        ArgumentNullException.ThrowIfNull(json);

        value = null;

        return !string.IsNullOrWhiteSpace(json) &&
               JsonSerializer.Deserialize<LockServiceAdditionalTests>(json, _options) is { } result &&
               (value = result) is not null;
    }
}
