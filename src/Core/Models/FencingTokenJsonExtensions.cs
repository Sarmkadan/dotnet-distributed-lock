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
    public static string ToJson(this FencingToken value, bool indented = false) =>
        ToJsonInternal(value, indented);

    private static string ToJsonInternal(FencingToken value, bool indented)
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
    /// <param name="json">The JSON string to deserialize. Null or empty strings return null.</param>
    /// <returns>The deserialized fencing token, or null if the JSON is null, empty, or whitespace.</returns>
    /// <exception cref="JsonException">Thrown when the JSON is invalid, malformed, or cannot be deserialized into a <see cref="FencingToken"/>.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> is null.</exception>
    public static FencingToken? FromJson(string? json)
    {
        ArgumentNullException.ThrowIfNull(json);

        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JsonSerializer.Deserialize<FencingToken>(json, _jsonOptions);
    }

    /// <summary>
    /// Attempts to deserialize a <see cref="FencingToken"/> from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="value">Receives the deserialized fencing token if successful; otherwise, null.</param>
    /// <returns>True if deserialization succeeded; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> is null.</exception>
    public static bool TryFromJson(string? json, out FencingToken? value)
    {
        if (json is null)
        {
            value = null;
            return false;
        }

        if (string.IsNullOrEmpty(json))
        {
            value = null;
            return true;
        }

        try
        {
            value = JsonSerializer.Deserialize<FencingToken>(json, _jsonOptions);
            return true;
        }
        catch (JsonException)
        {
            value = null;
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

            if (!root.TryGetProperty("token", out var tokenProperty) || tokenProperty.ValueKind == JsonValueKind.Null)
                throw new JsonException("Missing required 'token' property.");

            var token = tokenProperty.GetString()
                    ?? throw new JsonException("Token property cannot be null.");

            if (!root.TryGetProperty("sequenceNumber", out var sequenceNumberProperty))
                throw new JsonException("Missing required 'sequenceNumber' property.");

            if (!root.TryGetProperty("issuedAt", out var issuedAtProperty))
                throw new JsonException("Missing required 'issuedAt' property.");

            var sequenceNumber = sequenceNumberProperty.GetInt64();
            var issuedAt = issuedAtProperty.GetDateTime();

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