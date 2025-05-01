// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Formatters;

using System.Text.Json;
using System.Text.Json.Serialization;
using SarmKadan.DistributedLock.Core.Models;

/// <summary>
/// JSON serializer for lock data structures.
/// Provides consistent JSON formatting for API responses and persistence.
/// Handles DateTime serialization with UTC timezone for consistency.
/// </summary>
public class JsonLockSerializer
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
    /// Serializes a lock object to JSON string.
    /// </summary>
    public static string SerializeLock(Lock @lock)
    {
        try
        {
            return JsonSerializer.Serialize(@lock, _options);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to serialize lock object", ex);
        }
    }

    /// <summary>
    /// Serializes multiple locks to a JSON array.
    /// </summary>
    public static string SerializeLocks(IEnumerable<Lock> locks)
    {
        try
        {
            return JsonSerializer.Serialize(locks.ToList(), _options);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to serialize locks collection", ex);
        }
    }

    /// <summary>
    /// Deserializes a JSON string to a lock object.
    /// </summary>
    public static Lock? DeserializeLock(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Lock>(json, _options);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to deserialize lock object", ex);
        }
    }

    /// <summary>
    /// Deserializes a JSON array string to lock objects.
    /// </summary>
    public static List<Lock> DeserializeLocks(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<Lock>();

        try
        {
            return JsonSerializer.Deserialize<List<Lock>>(json, _options) ?? new List<Lock>();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to deserialize locks collection", ex);
        }
    }

    /// <summary>
    /// Serializes lock metrics for reporting.
    /// </summary>
    public static string SerializeMetrics(LockMetrics metrics)
    {
        try
        {
            return JsonSerializer.Serialize(metrics, _options);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to serialize metrics", ex);
        }
    }

    /// <summary>
    /// Creates a pretty-printed JSON representation of a lock.
    /// Useful for debugging and logging.
    /// </summary>
    public static string SerializeLockPretty(Lock @lock)
    {
        var prettyOptions = new JsonSerializerOptions(_options)
        {
            WriteIndented = true
        };

        try
        {
            return JsonSerializer.Serialize(@lock, prettyOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to pretty-print lock object", ex);
        }
    }
}

/// <summary>
/// Custom JSON converter for UTC DateTime serialization.
/// Ensures all DateTime values are serialized in ISO 8601 format with Z suffix.
/// </summary>
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetDateTime().ToUniversalTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToUniversalTime().ToString("O"));
    }
}

/// <summary>
/// Generic serialization interface for extensibility.
/// </summary>
public interface ILockSerializer<T>
{
    string Serialize(T data);
    T? Deserialize(string data);
}

/// <summary>
/// JSON implementation of generic lock serializer.
/// </summary>
public class JsonLockSerializer<T> : ILockSerializer<T> where T : class
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string Serialize(T data)
    {
        if (data == null)
            return string.Empty;

        try
        {
            return JsonSerializer.Serialize(data, _options);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to serialize {typeof(T).Name}", ex);
        }
    }

    public T? Deserialize(string data)
    {
        if (string.IsNullOrWhiteSpace(data))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(data, _options);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name}", ex);
        }
    }
}
