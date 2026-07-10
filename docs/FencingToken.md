# FencingToken

`FencingToken` is a value type that represents a fencing token used in distributed locking protocols. It combines a unique token identifier, a monotonically increasing sequence number, and an issuance timestamp to provide a total order among lock acquisitions. The type supports comparison, equality, and validation operations, enabling consumers to determine which token is more recent or valid in a distributed system.

## API

### `public FencingToken()`
Initializes a new instance of the `FencingToken` type.  
*Parameters:* None.  
*Returns:* A new `FencingToken` with default values (empty token, zero sequence number, and the minimum `DateTime` value).  
*Throws:* None.

### `public string Token`
Gets the unique token identifier.  
*Type:* `string`  
*Remarks:* This value is typically a GUID or a similar unique string assigned at creation.

### `public long SequenceNumber`
Gets the monotonically increasing sequence number.  
*Type:* `long`  
*Remarks:* Higher values indicate more recent tokens.

### `public DateTime IssuedAt`
Gets the timestamp when the token was issued.  
*Type:* `DateTime`  
*Remarks:* The value is expected to be in UTC for consistent ordering across nodes.

### `public FencingToken IncrementSequence()`
Creates a new `FencingToken` with the same `Token` and `IssuedAt` values, but with an incremented `SequenceNumber`.  
*Parameters:* None.  
*Returns:* A new `FencingToken` whose `SequenceNumber` is one greater than the current instance.  
*Throws:* None.

### `public bool IsGreaterThan(FencingToken other)`
Determines whether the current token is strictly greater than another token according to the fencing order (sequence number, then timestamp, then token).  
*Parameters:*  
- `other` – The `FencingToken` to compare against.  
*Returns:* `true` if the current token is greater; otherwise `false`.  
*Throws:* None.

### `public bool IsValid()`
Checks whether the token is considered valid. A token is valid if it has a non‑null, non‑empty `Token`, a non‑negative `SequenceNumber`, and a `IssuedAt` value that is not the default (`DateTime.MinValue`).  
*Parameters:* None.  
*Returns:* `true` if the token is valid; otherwise `false`.  
*Throws:* None.

### `public override bool Equals(object obj)`
Determines whether the current token equals another object.  
*Parameters:*  
- `obj` – The object to compare with the current token.  
*Returns:* `true` if `obj` is a `FencingToken` and all fields are equal; otherwise `false`.  
*Throws:* None.

### `public bool Equals(FencingToken other)`
Determines whether the current token equals another `FencingToken` value.  
*Parameters:*  
- `other` – The `FencingToken` to compare with the current instance.  
*Returns:* `true` if all fields are equal; otherwise `false`.  
*Throws:* None.

### `public override int GetHashCode()`
Returns the hash code for this token.  
*Parameters:* None.  
*Returns:* A 32‑bit signed integer hash code derived from the token’s fields.  
*Throws:* None.

### `public int CompareTo(FencingToken other)`
Compares the current token with another token and returns an integer that indicates whether the current instance precedes, follows, or occurs in the same position in the sort order.  
*Parameters:*  
- `other` – The `FencingToken` to compare with.  
*Returns:* A value less than zero if the current token is less than `other`; zero if they are equal; greater than zero if the current token is greater than `other`.  
*Throws:* None.

### `public override string ToString()`
Returns a string representation of the token, including its `Token`, `SequenceNumber`, and `IssuedAt` values.  
*Parameters:* None.  
*Returns:* A `string` in the format `"Token: {Token}, SequenceNumber: {SequenceNumber}, IssuedAt: {IssuedAt}"`.  
*Throws:* None.

## Usage

### Example 1: Creating and comparing fencing tokens

```csharp
using System;
using DistributedLock;

var token1 = new FencingToken
{
    Token = Guid.NewGuid().ToString(),
    SequenceNumber = 1,
    IssuedAt = DateTime.UtcNow
};

var token2 = token1.IncrementSequence();

Console.WriteLine($"Token1: {token1}");
Console.WriteLine($"Token2: {token2}");
Console.WriteLine($"Token2 is greater than Token1: {token2.IsGreaterThan(token1)}");
Console.WriteLine($"Token1 is valid: {token1.IsValid()}");
```

### Example 2: Using fencing tokens in a lock acquisition check

```csharp
using System;
using DistributedLock;

FencingToken currentToken = GetCurrentLockToken(); // hypothetical method
FencingToken newToken = new FencingToken
{
    Token = Guid.NewGuid().ToString(),
    SequenceNumber = currentToken.SequenceNumber + 1,
    IssuedAt = DateTime.UtcNow
};

if (newToken.IsGreaterThan(currentToken) && newToken.IsValid())
{
    // Acquire or renew the lock with newToken
    Console.WriteLine("Lock acquired with token: " + newToken);
}
else
{
    Console.WriteLine("Cannot acquire lock – token is not greater or invalid.");
}
```

## Notes

- **Default instance:** A `FencingToken` created with the parameterless constructor has an empty `Token`, a `SequenceNumber` of `0`, and `IssuedAt` equal to `DateTime.MinValue`. Such an instance is considered invalid by `IsValid()` and should not be used in comparisons or lock operations.
- **Comparison order:** `IsGreaterThan` and `CompareTo` use the following precedence: `SequenceNumber` (higher is greater), then `IssuedAt` (later is greater), then `Token` (lexicographic order). This ensures a deterministic total order even when sequence numbers or timestamps are equal.
- **Thread safety:** `FencingToken` is an immutable value type. All public fields are read‑only after construction (assuming the type is implemented as a struct with readonly fields). Therefore, instances are inherently thread‑safe. No synchronization is required when reading or comparing tokens from multiple threads.
- **Null handling:** The `Equals(object)` overload returns `false` when `obj` is `null`. The `CompareTo` method does not accept `null`; passing `null` will result in a `NullReferenceException` (or a compiler error if the type is a struct and the method is called on a nullable value). Always ensure the argument is a valid `FencingToken` instance.
- **Timestamp consistency:** For reliable ordering across distributed nodes, `IssuedAt` should always be set to `DateTime.UtcNow`. Using local time or unspecified kind may lead to incorrect comparisons.
