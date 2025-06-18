# LockRenewalFailedExceptionJsonExtensions

Provides JSON serialization and deserialization support for `LockRenewalFailedException` objects. This class extends `JsonConverter<LockRenewalFailedException>` and exposes static convenience methods for converting instances to and from JSON strings, as well as the standard converter read/write overrides required by `System.Text.Json`.

## API

### `public static string ToJson(LockRenewalFailedException value, JsonSerializerOptions? options = null)`

Serializes a `LockRenewalFailedException` instance to its JSON string representation.

**Parameters**
- `value` — The exception instance to serialize. Must not be `null`.
- `options` — Optional `JsonSerializerOptions` to control serialization behavior. When `null`, default options are used.

**Return Value**
A JSON string representing the exception.

**Throws**
- `ArgumentNullException` if `value` is `null`.
- `JsonException` or `NotSupportedException` if the object graph cannot be serialized.

---

### `public static LockRenewalFailedException? FromJson(string json, JsonSerializerOptions? options = null)`

Deserializes a JSON string into a `LockRenewalFailedException` instance.

**Parameters**
- `json` — The JSON string to deserialize. Must not be `null`.
- `options` — Optional `JsonSerializerOptions` to control deserialization behavior. When `null`, default options are used.

**Return Value**
A `LockRenewalFailedException` instance, or `null` if the JSON string represents a JSON null literal.

**Throws**
- `ArgumentNullException` if `json` is `null`.
- `JsonException` if the JSON is malformed or cannot be mapped to the target type.

---

### `public static bool TryFromJson(string json, out LockRenewalFailedException? result, JsonSerializerOptions? options = null)`

Attempts to deserialize a JSON string into a `LockRenewalFailedException` instance without throwing on failure.

**Parameters**
- `json` — The JSON string to deserialize. Must not be `null`.
- `result` — When this method returns `true`, contains the deserialized exception; when `false`, contains `null`.
- `options` — Optional `JsonSerializerOptions`. When `null`, default options are used.

**Return Value**
`true` if deserialization succeeded; `false` if the JSON was invalid or could not be mapped.

**Throws**
- `ArgumentNullException` if `json` is `null`.

---

### `public override LockRenewalFailedException? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)`

Reads and deserializes a `LockRenewalFailedException` from a `Utf8JsonReader`. This is the standard `JsonConverter<T>` override invoked by the serializer during deserialization.

**Parameters**
- `reader` — The UTF-8 JSON reader positioned at the start of the object to deserialize.
- `typeToConvert` — The type to convert (expected to be `LockRenewalFailedException`).
- `options` — The `JsonSerializerOptions` in use.

**Return Value**
A `LockRenewalFailedException` instance, or `null` if the JSON token is null.

**Throws**
- `JsonException` if the JSON structure is invalid for the target type.

---

### `public override void Write(Utf8JsonWriter writer, LockRenewalFailedException value, JsonSerializerOptions options)`

Writes a `LockRenewalFailedException` instance to a `Utf8JsonWriter`. This is the standard `JsonConverter<T>` override invoked by the serializer during serialization.

**Parameters**
- `writer` — The UTF-8 JSON writer to which the exception will be written.
- `value` — The exception instance to serialize. Must not be `null`.
- `options` — The `JsonSerializerOptions` in use.

**Throws**
- `ArgumentNullException` if `writer` or `value` is `null`.

## Usage

### Example 1: Round-tripping an exception to JSON and back

```csharp
using System.Text.Json;

var original = new LockRenewalFailedException("Lock renewal timed out", "my-lock-id");

// Serialize to JSON
string json = LockRenewalFailedExceptionJsonExtensions.ToJson(original);
Console.WriteLine(json);

// Deserialize back
LockRenewalFailedException? deserialized =
    LockRenewalFailedExceptionJsonExtensions.FromJson(json);

Console.WriteLine(deserialized?.Message);
```

### Example 2: Safe deserialization with TryFromJson

```csharp
string incomingJson = GetJsonFromExternalSource(); // may be malformed

if (LockRenewalFailedExceptionJsonExtensions.TryFromJson(
        incomingJson, out LockRenewalFailedException? ex))
{
    Console.WriteLine($"Deserialized: {ex.Message}");
}
else
{
    Console.WriteLine("Failed to parse LockRenewalFailedException JSON.");
}
```

## Notes

- All static methods delegate to `System.Text.Json.JsonSerializer` internally, using the converter defined by this class. The `Read` and `Write` overrides are invoked automatically when this converter is registered.
- The `TryFromJson` method catches `JsonException` internally and returns `false`; it does not catch argument-validation exceptions such as `ArgumentNullException`.
- The `FromJson` method returns `null` only when the JSON string is literally `null` (the JSON null token). An empty string or whitespace will cause a `JsonException`.
- This converter is not thread-safe by default, but the static methods are safe to call concurrently as long as the provided `JsonSerializerOptions` instance is not mutated during the call. The `Read` and `Write` overrides receive their state through parameters and do not rely on shared mutable fields.
- Serialization behavior (casing, indentation, inclusion of null values) is governed entirely by the `JsonSerializerOptions` passed in. If no options are supplied, `JsonSerializerDefaults.General` defaults apply.
