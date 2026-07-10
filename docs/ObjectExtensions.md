# ObjectExtensions

Extension methods for `object` that provide common utility operations such as cloning, serialization, type conversion, validation, and fluent-style operations.

## API

### `DeepClone<T>(T source)`
Creates a deep copy of the given object by serializing it to JSON and deserializing it back.

- **Parameters**
  - `source`: The object to clone. Can be `null`.
- **Return value**
  - A deep clone of `source`, or `null` if `source` is `null`.
- **Exceptions**
  - Throws `JsonException` if serialization or deserialization fails.

---

### `ToJsonString<T>(T value)`
Serializes the given object to a JSON string with default formatting.

- **Parameters**
  - `value`: The object to serialize. Can be `null`.
- **Return value**
  - A JSON representation of `value`, or `null` if `value` is `null`.
- **Exceptions**
  - Throws `JsonException` if serialization fails.

---
### `ToCompactJsonString<T>(T value)`
Serializes the given object to a compact JSON string without whitespace.

- **Parameters**
  - `value`: The object to serialize. Can be `null`.
- **Return value**
  - A compact JSON representation of `value`, or `null` if `value` is `null`.
- **Exceptions**
  - Throws `JsonException` if serialization fails.

---
### `IsNullOrDefault<T>(T? value)`
Determines whether the given value is `null` or its default value.

- **Parameters**
  - `value`: The value to check.
- **Return value**
  - `true` if `value` is `null` or equal to `default(T)`; otherwise, `false`.

---
### `TryCast<T>(object? obj)`
Attempts to cast the given object to the specified type.

- **Parameters**
  - `obj`: The object to cast.
- **Return value**
  - `true` if the cast succeeds; otherwise, `false`.
- **Remarks**
  - The out parameter `result` contains the cast value if successful.

---
### `ComputeHash<T>(T value)`
Computes a stable hash code for the given object using its JSON representation.

- **Parameters**
  - `value`: The object to hash. Can be `null`.
- **Return value**
  - A stable hash code derived from the JSON serialization of `value`, or `0` if `value` is `null`.
- **Exceptions**
  - Throws `JsonException` if serialization fails.

---
### `Tap<T>(T value, Action<T> action)`
Invokes the given action on the value and returns the value itself.

- **Parameters**
  - `value`: The value to process.
  - `action`: The action to perform on `value`.
- **Return value**
  - The original `value`.
- **Exceptions**
  - Throws if `action` throws.

---
### `MapTo<T, TResult>(T value, Func<T, TResult> mapper)`
Applies the given mapper function to the value and returns the result.

- **Parameters**
  - `value`: The input value.
  - `mapper`: The function to apply.
- **Return value**
  - The result of `mapper(value)`, or `null` if `mapper` returns `null`.
- **Exceptions**
  - Throws if `mapper` throws.

---
### `GetSimpleTypeName<T>()`
Gets the simple name of the type `T` without namespace qualifiers.

- **Return value**
  - The simple type name of `T`.

---
### `Validate<T>(T value, Action<T> validator)`
Invokes the given validator action on the value and returns the value itself.

- **Parameters**
  - `value`: The value to validate.
  - `validator`: The validation action. Should throw if validation fails.
- **Return value**
  - The original `value`.
- **Exceptions**
  - Throws if `validator` throws.

## Usage
