# FencingTokenExtensions

Provides extension methods for the `FencingToken` struct, enabling parsing, comparison, and manipulation of fencing tokens used in distributed locking scenarios.

## API

### `Parse`

```csharp
public static FencingToken Parse(string tokenString)
```

Parses a fencing token from its string representation.

- **Parameters**:
  - `tokenString`: The string representation of the fencing token, typically in the format `"<sequenceNumber>-<token>"`.
- **Returns**: A `FencingToken` instance parsed from the input string.
- **Throws**:
  - `ArgumentNullException` if `tokenString` is `null`.
  - `FormatException` if the string is not in the expected format.

---

### `TryParse`

```csharp
public static bool TryParse([NotNullWhen(true)] string? tokenString, out FencingToken token)
```

Attempts to parse a fencing token from its string representation.

- **Parameters**:
  - `tokenString`: The string representation of the fencing token. May be `null`.
  - `token`: Output parameter for the parsed `FencingToken` if successful.
- **Returns**: `true` if parsing succeeds; otherwise, `false`.
- **Remarks**: Does not throw exceptions for invalid input.

---

### `ToTokenString`

```csharp
public static string ToTokenString(this FencingToken token)
```

Converts a fencing token to its string representation.

- **Parameters**:
  - `token`: The fencing token to convert.
- **Returns**: A string in the format `"<sequenceNumber>-<token>"`.

---

### `GetAge`

```csharp
public static TimeSpan GetAge(this FencingToken token)
```

Calculates the age of the fencing token based on its creation timestamp.

- **Parameters**:
  - `token`: The fencing token whose age is to be calculated.
- **Returns**: A `TimeSpan` representing the duration since the token was created.
- **Remarks**: The age is calculated using the token's internal timestamp, which is assumed to be in UTC.

---

### `IsLessThan`

```csharp
public static bool IsLessThan(this FencingToken left, FencingToken right)
```

Determines whether one fencing token is less than another.

- **Parameters**:
  - `left`: The left-hand side token for comparison.
  - `right`: The right-hand side token for comparison.
- **Returns**: `true` if `left` is less than `right`; otherwise, `false`.
- **Remarks**: Comparison is based on the sequence number of the tokens.

---

### `IsGreaterThanOrEqual`

```csharp
public static bool IsGreaterThanOrEqual(this FencingToken left, FencingToken right)
```

Determines whether one fencing token is greater than or equal to another.

- **Parameters**:
  - `left`: The left-hand side token for comparison.
  - `right`: The right-hand side token for comparison.
- **Returns**: `true` if `left` is greater than or equal to `right`; otherwise, `false`.
- **Remarks**: Comparison is based on the sequence number of the tokens.

---
### `IsLessThanOrEqual`

```csharp
public static bool IsLessThanOrEqual(this FencingToken left, FencingToken right)
```

Determines whether one fencing token is less than or equal to another.

- **Parameters**:
  - `left`: The left-hand side token for comparison.
  - `right`: The right-hand side token for comparison.
- **Returns**: `true` if `left` is less than or equal to `right`; otherwise, `false`.
- **Remarks**: Comparison is based on the sequence number of the tokens.

---
### `WithSequenceNumber`

```csharp
public static FencingToken WithSequenceNumber(this FencingToken token, long newSequenceNumber)
```

Creates a new fencing token with the specified sequence number while preserving the token value.

- **Parameters**:
  - `token`: The original fencing token.
  - `newSequenceNumber`: The new sequence number to assign.
- **Returns**: A new `FencingToken` with the updated sequence number and the same token value.

---
### `WithNewToken`

```csharp
public static FencingToken WithNewToken(this FencingToken token)
```

Creates a new fencing token with the same sequence number but a new token value.

- **Parameters**:
  - `token`: The original fencing token.
- **Returns**: A new `FencingToken` with the same sequence number and a new token value.

---
### `IsAdjacentTo`

```csharp
public static bool IsAdjacentTo(this FencingToken token, FencingToken other)
```

Determines whether two fencing tokens are adjacent in sequence.

- **Parameters**:
  - `token`: The first fencing token.
  - `other`: The second fencing token.
- **Returns**: `true` if the absolute difference between the sequence numbers of `token` and `other` is exactly 1; otherwise, `false`.

---
### `SequenceDifference`

```csharp
public static long SequenceDifference(this FencingToken left, FencingToken right)
```

Calculates the difference between the sequence numbers of two fencing tokens.

- **Parameters**:
  - `left`: The left-hand side token.
  - `right`: The right-hand side token.
- **Returns**: The difference `left.SequenceNumber - right.SequenceNumber`.

## Usage

### Parsing and Comparing Tokens

```csharp
using var distributedLock = await DistributedLock.CreateAsync("resource-key");
FencingToken token = await distributedLock.AcquireAsync();

// Serialize the token for storage or transmission
string tokenString = token.ToTokenString();

// Parse the token back when needed
if (FencingTokenExtensions.TryParse(tokenString, out var parsedToken))
{
    Console.WriteLine($"Parsed token: {parsedToken}");

    // Compare tokens
    if (parsedToken.IsLessThan(token))
    {
        Console.WriteLine("Parsed token is older than the current token.");
    }
}
```

### Generating New Tokens

```csharp
// Create a new fencing token with an incremented sequence number
FencingToken newToken = token.WithSequenceNumber(token.SequenceNumber + 1);

// Create a new token with the same sequence but a new value
FencingToken refreshedToken = token.WithNewToken();

// Check if tokens are adjacent
if (token.IsAdjacentTo(newToken))
{
    Console.WriteLine("Tokens are adjacent in sequence.");
}
```

## Notes

- **Thread Safety**: All methods are thread-safe as they operate on immutable `FencingToken` values or perform atomic comparisons.
- **Sequence Number Wrapping**: No special handling is provided for sequence number overflow or underflow. Consumers should ensure sequence numbers remain within valid ranges for their use case.
- **Timestamp Accuracy**: The `GetAge` method relies on the internal timestamp of the token, which is assumed to be in UTC. Clock drift or incorrect timestamp initialization may lead to inaccurate age calculations.
- **Adjacency Check**: The `IsAdjacentTo` method only checks for an absolute difference of 1 in sequence numbers. It does not account for gaps larger than 1 or wrap-around scenarios.
