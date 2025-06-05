# StringExtensions

The `StringExtensions` class provides a set of static utility methods designed to support string manipulation and validation tasks specific to the `dotnet-distributed-lock` library. These extensions handle critical operations such as sanitizing lock identifiers, validating naming conventions, converting strings to specific byte encodings required for hashing or network transmission, and parsing hexadecimal representations. By centralizing these common patterns, the class ensures consistency in how lock names and related identifiers are processed throughout the distributed locking mechanism.

## API

### `IsValidLockName`
Determines whether a given string adheres to the naming constraints required for a distributed lock identifier.
*   **Parameters**: `string name` – The candidate lock name to validate.
*   **Returns**: `bool` – `true` if the name is valid according to library rules; otherwise, `false`.
*   **Throws**: None. Returns `false` for null or empty inputs depending on implementation logic.

### `SanitizeForLockName`
Transforms an arbitrary string into a format suitable for use as a lock name by removing or replacing invalid characters.
*   **Parameters**: `string name` – The raw string to sanitize.
*   **Returns**: `string` – A sanitized version of the input string safe for use as a lock identifier.
*   **Throws**: None. Typically returns an empty string or a default placeholder if the input yields no valid characters.

### `ToUtf8Bytes`
Converts a string into its binary representation using UTF-8 encoding.
*   **Parameters**: `string value` – The string to convert.
*   **Returns**: `byte[]` – A byte array containing the UTF-8 encoded data.
*   **Throws**: `ArgumentNullException` if `value` is `null`.

### `ToAsciiBytes`
Converts a string into its binary representation using ASCII encoding.
*   **Parameters**: `string value` – The string to convert.
*   **Returns**: `byte[]` – A byte array containing the ASCII encoded data. Non-ASCII characters may be replaced with a fallback character (usually `?`).
*   **Throws**: `ArgumentNullException` if `value` is `null`.

### `FromHexString`
Parses a hexadecimal string representation into a corresponding byte array.
*   **Parameters**: `string hex` – The hexadecimal string to parse (case-insensitive).
*   **Returns**: `byte[]` – The resulting byte array.
*   **Throws**: `FormatException` if the string contains invalid hexadecimal characters or has an odd length. `ArgumentNullException` if `hex` is `null`.

### `IsValidGuid`
Validates whether a string represents a correctly formatted Globally Unique Identifier (GUID).
*   **Parameters**: `string value` – The string to validate.
*   **Returns**: `bool` – `true` if the string is a valid GUID format; otherwise, `false`.
*   **Throws**: None. Returns `false` for null or malformed strings.

### `TruncateWithEllipsis`
Shortens a string to a specified maximum length, appending an ellipsis if truncation occurs.
*   **Parameters**: 
    *   `string value` – The source string.
    *   `int maxLength` – The maximum allowed length of the returned string (including the ellipsis).
*   **Returns**: `string` – The original string if it fits within `maxLength`, or a truncated version ending with "..." otherwise.
*   **Throws**: `ArgumentOutOfRangeException` if `maxLength` is less than the length of the ellipsis string.

### `ToTrimmedList`
Splits a delimited string into a list of substrings, trimming whitespace from each entry and excluding empty results.
*   **Parameters**: 
    *   `string value` – The source string.
    *   `char separator` – The character used to delimit items.
*   **Returns**: `List<string>` – A list of non-empty, trimmed substrings.
*   **Throws**: `ArgumentNullException` if `value` is `null`.

## Usage

### Validating and Sanitizing Lock Names
The following example demonstrates how to accept a user-provided resource name, sanitize it to ensure it meets locking requirements, and verify its validity before attempting to acquire a lock.

```csharp
using DistributedLock.Extensions;

public void AcquireLockForResource(string userInput)
{
    if (string.IsNullOrWhiteSpace(userInput))
    {
        throw new ArgumentException("Resource name cannot be empty.", nameof(userInput));
    }

    // Sanitize the input to remove invalid characters
    string safeLockName = userInput.SanitizeForLockName();

    // Validate the sanitized name
    if (!safeLockName.IsValidLockName())
    {
        throw new InvalidOperationException("The sanitized name does not meet lock naming requirements.");
    }

    // Proceed with acquiring the lock using safeLockName
    // var lockHandle = distributedLock.Acquire(safeLockName);
}
```

### Processing Hexadecimal Identifiers and Byte Conversion
This example illustrates converting a lock metadata string to ASCII bytes for hashing and parsing a hex-encoded session ID into a byte array for binary comparison.

```csharp
using System;
using System.Linq;
using DistributedLock.Extensions;

public void ProcessLockMetadata(string metadata, string hexSessionId)
{
    // Convert metadata to ASCII bytes for consistent hashing
    byte[] metadataBytes = metadata.ToAsciiBytes();
    
    // Parse the hexadecimal session ID
    byte[] sessionBytes;
    try
    {
        sessionBytes = hexSessionId.FromHexString();
    }
    catch (FormatException)
    {
        Console.WriteLine("Invalid session ID format.");
        return;
    }

    // Example: Truncate metadata for logging if it exceeds 50 characters
    string loggableMetadata = metadata.TruncateWithEllipsis(50);
    Console.WriteLine($"Processing session for: {loggableMetadata}");
}
```

## Notes

*   **Thread Safety**: As this class consists entirely of static methods that operate on immutable string inputs and return new instances (strings or byte arrays) without modifying shared state, all members are inherently thread-safe.
*   **Null Handling**: Most conversion methods (`ToUtf8Bytes`, `ToAsciiBytes`, `FromHexString`, `ToTrimmedList`) explicitly throw `ArgumentNullException` when passed `null`. Validation methods (`IsValidLockName`, `IsValidGuid`) generally return `false` for null inputs rather than throwing, allowing for safer inline checks.
*   **Encoding Implications**: When using `ToAsciiBytes`, be aware that any characters outside the 7-bit ASCII range will be lost or replaced by a fallback character. For internationalized lock names, `ToUtf8Bytes` should be preferred to preserve data integrity.
*   **Hex Parsing Constraints**: The `FromHexString` method expects a string with an even number of characters. Passing a string with an odd length will result in a `FormatException`, as a single hexadecimal character does not constitute a full byte.
*   **Truncation Logic**: The `TruncateWithEllipsis` method includes the ellipsis characters in the `maxLength` calculation. If `maxLength` is smaller than the ellipsis string itself, the method cannot construct a valid output and will throw an exception.
