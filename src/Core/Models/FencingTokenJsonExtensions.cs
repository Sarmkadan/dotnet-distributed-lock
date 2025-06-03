#nullable enable

// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using System.Text.Json;
using System.Text.Json.Serialization;

namespace SarmKadan.DistributedLock.Models;

/// <summary>
/// Provides System.Text.Json serialization and deserialization extensions for <see cref="FencingToken"/>.
/// </summary>
public static class FencingTokenJsonExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters = { new FencingTokenJsonConverter() }
    };

    /// <summary>
    /// Serializes a <see cref="FencingToken"/> to a JSON string.
    /// </summary>
    /// <param name="value">The fencing token to serialize.</param>
    /// <param name="indented">Whether to format the JSON with indentation for readability.</param>
    /// <returns>A JSON string representation of the fencing token.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public static string ToJson(this FencingToken value, bool indented = false)
    {
        ArgumentNullException.ThrowIfNull(value);

        var options = indented
            ? new JsonSerializerOptions(_jsonOptions) { WriteIndented = true }
            : _jsonOptions;

        return JsonSerializer.Serialize(value, options);
    }

    /// <summary>
    /// Deserializes a <see cref="FencingToken"/> from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized fencing token, or null if the JSON is null or empty.</returns>
    /// <exception cref="JsonException">Thrown when the JSON is invalid or cannot be deserialized.</exception>
    public static FencingToken? FromJson(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        return JsonSerializer.Deserialize<FencingToken>(json, _jsonOptions);
    }

    /// <summary>
    /// Attempts to deserialize a <see cref="FencingToken"/> from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="value">Receives the deserialized fencing token if successful; otherwise, null.</param>
    /// <returns>True if deserialization succeeded; otherwise, false.</returns>
    public static bool TryFromJson(string? json, out FencingToken? value)
    {
        value = null;

        if (string.IsNullOrEmpty(json))
            return true;

        try
        {
            value = JsonSerializer.Deserialize<FencingToken>(json, _jsonOptions);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Custom JSON converter for <see cref="FencingToken"/> that handles serialization and deserialization.
    /// </summary>
    private sealed class FencingTokenJsonConverter : JsonConverter<FencingToken>
    {
        public override FencingToken? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Null)
                return null;

            var token = root.GetProperty("token").GetString()
                ?? throw new JsonException("Missing required 'token' property.");

            var sequenceNumber = root.GetProperty("sequenceNumber").GetInt64();
            var issuedAt = root.GetProperty("issuedAt").GetDateTime();

            return new FencingToken(token, sequenceNumber, issuedAt);
        }

        public override void Write(
            Utf8JsonWriter writer,
            FencingToken value,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString("token", value.Token);
            writer.WriteNumber("sequenceNumber", value.SequenceNumber);
            writer.WriteString("issuedAt", value.IssuedAt);

            writer.WriteEndObject();
        }
    }
}