#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =====================================================================

namespace SarmKadan.DistributedLock.Formatters;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using SarmKadan.DistributedLock.Models;

/// <summary>
/// Provides System.Text.Json serialization extensions for <see cref="CsvLockExporter"/>.
/// Enables round-trip serialization of lock data to/from JSON format.
/// </summary>
public static class CsvLockExporterJsonExtensions
{
	/// <summary>
	/// JSON serialization options with camelCase naming policy and proper type handling.
	/// </summary>
	private static readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web)
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		WriteIndented = false,
		TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
		ReferenceHandler = ReferenceHandler.IgnoreCycles,
	};

	/// <summary>
	/// Serializes a <see cref="CsvLockExporter"/> instance to JSON string.
	/// </summary>
	/// <param name="value">The exporter instance to serialize.</param>
	/// <param name="indented">Whether to format the JSON with indentation for readability.</param>
	/// <returns>A JSON string representation of the exporter.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
	public static string ToJson(this CsvLockExporter value, bool indented = false)
	{
		ArgumentNullException.ThrowIfNull(value);

		var options = indented
			? new JsonSerializerOptions(_jsonSerializerOptions)
			{
				WriteIndented = true,
			}
			: _jsonSerializerOptions;

		return JsonSerializer.Serialize(value, options);
	}

	/// <summary>
	/// Deserializes a JSON string to a <see cref="CsvLockExporter"/> instance.
	/// </summary>
	/// <param name="json">The JSON string to deserialize.</param>
	/// <returns>A deserialized <see cref="CsvLockExporter"/> instance, or null if the JSON is empty.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="json"/> is null.</exception>
	/// <exception cref="JsonException">Thrown if the JSON is invalid or cannot be deserialized.</exception>
	public static CsvLockExporter? FromJson(string json)
	{
		ArgumentNullException.ThrowIfNull(json);

		if (string.IsNullOrWhiteSpace(json))
		{
			return null;
		}

		return JsonSerializer.Deserialize<CsvLockExporter>(json, _jsonSerializerOptions);
	}

	/// <summary>
	/// Attempts to deserialize a JSON string to a <see cref="CsvLockExporter"/> instance.
	/// </summary>
	/// <param name="json">The JSON string to deserialize.</param>
	/// <param name="value">Receives the deserialized instance if successful.</param>
	/// <returns>True if deserialization succeeded; otherwise, false.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="json"/> is null.</exception>
	public static bool TryFromJson(string json, out CsvLockExporter? value)
	{
		ArgumentNullException.ThrowIfNull(json);

		value = null;

		if (string.IsNullOrWhiteSpace(json))
		{
			return false;
		}

		try
		{
			value = JsonSerializer.Deserialize<CsvLockExporter>(json, _jsonSerializerOptions);
			return value is not null;
		}
		catch (JsonException)
		{
			return false;
		}
	}
}