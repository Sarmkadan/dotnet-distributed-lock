#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Core.Exceptions;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Provides System.Text.Json serialization and deserialization extensions for <see cref="LockRenewalFailedException"/>.
/// </summary>
public static class LockRenewalFailedExceptionJsonExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Serializes the <see cref="LockRenewalFailedException"/> to a JSON string.
    /// </summary>
    /// <param name="value">The exception to serialize.</param>
    /// <param name="indented">Whether to format the JSON with indentation.</param>
    /// <returns>A JSON string representation of the exception.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static string ToJson(this LockRenewalFailedException value, bool indented = false)
    {
        ArgumentNullException.ThrowIfNull(value);

        var options = indented
            ? new JsonSerializerOptions(_jsonOptions)
            {
                WriteIndented = true
            }
            : _jsonOptions;

        return JsonSerializer.Serialize(value, options);
    }

    /// <summary>
    /// Deserializes a <see cref="LockRenewalFailedException"/> from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized exception, or <see langword="null"/> if the JSON is <see langword="null"/>, empty, or whitespace.</returns>
    /// <exception cref="JsonException">The JSON is invalid or cannot be deserialized.</exception>
    public static LockRenewalFailedException? FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<LockRenewalFailedException>(json, _jsonOptions);
    }

    /// <summary>
    /// Attempts to deserialize a <see cref="LockRenewalFailedException"/> from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="value">The deserialized exception, or <see langword="null"/> if deserialization fails.</param>
    /// <returns><see langword="true"/> if deserialization succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryFromJson(string json, out LockRenewalFailedException? value)
    {
        value = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            value = JsonSerializer.Deserialize<LockRenewalFailedException>(json, _jsonOptions);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Custom JSON converter for <see cref="LockRenewalFailedException"/>.
    /// </summary>
    private sealed class LockRenewalFailedExceptionConverter : JsonConverter<LockRenewalFailedException>
    {
        public override LockRenewalFailedException Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            using JsonDocument doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            var lockId = root.GetProperty("lockId").GetString()
                ?? throw new JsonException("Missing required property 'lockId'.");
            var message = root.GetProperty("message").GetString()
                ?? throw new JsonException("Missing required property 'message'.");

            var exception = root.TryGetProperty("innerException", out var innerExceptionElement)
                ? new LockRenewalFailedException(
                    lockId,
                    message,
                    innerExceptionElement.Deserialize<Exception>(options))
                : new LockRenewalFailedException(lockId, message);

            return exception;
        }

        public override void Write(
            Utf8JsonWriter writer,
            LockRenewalFailedException value,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString("lockId", value.LockId);
            writer.WriteString("message", value.Message);

            if (value.InnerException is not null)
            {
                writer.WritePropertyName("innerException");
                JsonSerializer.Serialize(writer, value.InnerException, options);
            }

            writer.WriteEndObject();
        }
    }
}
