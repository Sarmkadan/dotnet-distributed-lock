using System.Text.Json;
using System.Text.Json.Serialization;

namespace SarmKadan.DistributedLock.Benchmarks.Benchmarks;

/// <summary>
/// Provides System.Text.Json serialization helpers for BasicBenchmark
/// </summary>
public static class BasicBenchmarkJsonExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Serializes a BasicBenchmark instance to a JSON string
    /// </summary>
    /// <param name="value">The BasicBenchmark instance to serialize</param>
    /// <param name="indented">Whether to format the JSON with indentation</param>
    /// <returns>A JSON string representation of the BasicBenchmark</returns>
    public static string ToJson(this BasicBenchmark value, bool indented = false)
    {
        if (value is null)
        {
            return "{}";
        }

        var options = indented
            ? new JsonSerializerOptions(_jsonOptions) { WriteIndented = true }
            : _jsonOptions;

        return JsonSerializer.Serialize(value, options);
    }

    /// <summary>
    /// Deserializes a BasicBenchmark instance from a JSON string
    /// </summary>
    /// <param name="json">The JSON string to deserialize</param>
    /// <returns>The deserialized BasicBenchmark instance, or null if JSON is invalid</returns>
    public static BasicBenchmark? FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BasicBenchmark>(json, _jsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Attempts to deserialize a BasicBenchmark instance from a JSON string
    /// </summary>
    /// <param name="json">The JSON string to deserialize</param>
    /// <param name="value">Receives the deserialized BasicBenchmark instance if successful</param>
    /// <returns>True if deserialization succeeded; false otherwise</returns>
    public static bool TryFromJson(string json, out BasicBenchmark? value)
    {
        value = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            value = JsonSerializer.Deserialize<BasicBenchmark>(json, _jsonOptions);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
