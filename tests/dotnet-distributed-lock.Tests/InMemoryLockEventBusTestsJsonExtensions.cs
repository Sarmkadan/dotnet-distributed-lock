#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Provides System.Text.Json serialization and deserialization extensions for <see cref="InMemoryLockEventBusTests"/>.
/// </summary>
public static class InMemoryLockEventBusTestsJsonExtensions
{
	private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
		WriteIndented = false,
	};

	/// <summary>
	/// Serializes the specified <see cref="InMemoryLockEventBusTests"/> instance to a JSON string.
	/// </summary>
	/// <param name="value">The instance to serialize.</param>
	/// <param name="indented">Whether to format the JSON with indentation for readability.</param>
	/// <returns>A JSON string representation of the instance.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
	public static string ToJson(this InMemoryLockEventBusTests value, bool indented = false)
	{
		ArgumentNullException.ThrowIfNull(value);

		var options = indented
			? new JsonSerializerOptions(_jsonOptions) { WriteIndented = true }
			: _jsonOptions;

		return JsonSerializer.Serialize(value, options);
	}

	/// <summary>
	/// Deserializes a JSON string to an <see cref="InMemoryLockEventBusTests"/> instance.
	/// </summary>
	/// <param name="json">The JSON string to deserialize.</param>
	/// <returns>An <see cref="InMemoryLockEventBusTests"/> instance, or null if the JSON is null or empty.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> is null.</exception>
	/// <exception cref="JsonException">Thrown when the JSON is invalid or cannot be deserialized.</exception>
	public static InMemoryLockEventBusTests? FromJson(string json)
	{
		ArgumentNullException.ThrowIfNull(json);

		if (string.IsNullOrEmpty(json))
		{
			return null;
		}

		return JsonSerializer.Deserialize<InMemoryLockEventBusTests>(json, _jsonOptions);
	}

	/// <summary>
	/// Attempts to deserialize a JSON string to an <see cref="InMemoryLockEventBusTests"/> instance.
	/// </summary>
	/// <param name="json">The JSON string to deserialize.</param>
	/// <param name="value">Receives the deserialized instance if successful, otherwise null.</param>
	/// <returns>True if deserialization succeeded; otherwise, false.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> is null.</exception>
	public static bool TryFromJson(string json, out InMemoryLockEventBusTests? value)
	{
		ArgumentNullException.ThrowIfNull(json);

		value = null;

		if (string.IsNullOrEmpty(json))
		{
			return false;
		}

		try
		{
			value = JsonSerializer.Deserialize<InMemoryLockEventBusTests>(json, _jsonOptions);
			return true;
		}
		catch (JsonException)
		{
			return false;
		}
	}
}