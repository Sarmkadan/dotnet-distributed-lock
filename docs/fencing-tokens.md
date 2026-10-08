# Fencing Tokens

This guide describes the fencing-token mechanism: the concept, the `FencingTokenService` API, the `FencingToken` model, and usage examples.

## Concept

A distributed lock alone does not protect a shared resource from a stale holder. A process can acquire a lock, pause (GC, network partition, VM freeze), lose the lock when it expires, and then resume and write anyway. This is a "zombie write".

A fencing token fixes this. Each lock grant is associated with a number that only ever goes up. The protected resource (or the code in front of it) remembers the highest token it has seen, and rejects writes that carry a lower or stale token. The lock says who may try; the token says whose write is still current.

In this library:

- `FencingTokenService` keeps, per lock key, a monotonically increasing sequence counter and the currently active token.
- `FencingToken` is an immutable value: a random string part plus a `long` sequence number, plus the UTC issue time.
- Sequence numbers are per key and are never reused for that key, even after a revoke.

## Model: `FencingToken`

Namespace: `SarmKadan.DistributedLock.Models`

`FencingToken` is an immutable class that implements `IEquatable<FencingToken>` and `IComparable<FencingToken>`.

### Constructor

```csharp
new FencingToken(string token, long sequenceNumber, DateTime? issuedAt = null)
```

- `token` must not be null, empty, or whitespace. Otherwise `ArgumentException`.
- `sequenceNumber` must not be negative. Otherwise `ArgumentException`.
- `issuedAt` defaults to `DateTime.UtcNow`.

### Properties

| Property         | Type       | Description                                  |
|------------------|------------|----------------------------------------------|
| `Token`          | `string`   | Opaque token string (16 hex characters when issued by the service). |
| `SequenceNumber` | `long`     | Ordering value. Higher means newer.          |
| `IssuedAt`       | `DateTime` | Issue time, UTC.                             |

### Members

- `FencingToken IncrementSequence()` - returns a new token with a new random string and `SequenceNumber + 1`.
- `bool IsGreaterThan(FencingToken other)` - compares `SequenceNumber` only. Throws `ArgumentNullException` for null.
- `bool IsValid(TimeSpan tokenLifetime)` - `true` while `DateTime.UtcNow - IssuedAt < tokenLifetime`.
- `bool Equals(FencingToken other)` - equal when both `Token` and `SequenceNumber` match. `GetHashCode` is consistent with this.
- `int CompareTo(FencingToken other)` - orders by `SequenceNumber` only. A null argument sorts before the instance.
- `string ToString()` - returns `"{Token}:{SequenceNumber}"`. This is the wire format used by the extension methods.

### Extension methods (`FencingTokenExtensions`)

Namespace: `SarmKadan.DistributedLock.Models`

Parsing:

- `FencingToken Parse(string tokenString)` - parses `"{Token}:{SequenceNumber}"`. Splits on the first `:`, so the token part may contain colons. Throws `ArgumentNullException` for null, `ArgumentException` for a bad format or a non-numeric sequence.
- `bool TryParse(string tokenString, out FencingToken? token)` - same rules, no exceptions. Returns `false` for null, empty, or whitespace input.
- `string ToTokenString(this FencingToken token)` - same as `ToString()`.

Comparison (all compare `SequenceNumber`):

- `IsLessThan`, `IsGreaterThanOrEqual`, `IsLessThanOrEqual` - the obvious comparisons.
- `IsAdjacentTo` - `true` when the sequence numbers differ by exactly 1.
- `SequenceDifference` - `this.SequenceNumber - other.SequenceNumber`.
- `IsNewerThan` - compares `IssuedAt`, not the sequence number.

Derivation:

- `WithSequenceNumber(long newSequenceNumber)` - same token string and issue time, new sequence. Throws `ArgumentOutOfRangeException` if negative.
- `WithNewToken()` - same sequence and issue time, new random token string.

Time:

- `TimeSpan GetAge()` - time since `IssuedAt`.
- `bool IsExpired(DateTimeOffset now)` - returns `true` when `IssuedAt` is earlier than `now`. Despite the name, it does not check a lifetime. For lifetime checks use `IsValid(TimeSpan)` or compare `GetAge()` with your limit.

Display:

- `string ToDisplayString()` - `"Token: {Token}, Sequence: {SequenceNumber}, Issued: {IssuedAt:O}"`.

## Service: `FencingTokenService`

Namespace: `SarmKadan.DistributedLock.Services`

`sealed` class with a constructor that takes `ILogger<FencingTokenService>`. It is registered as a singleton by `AddDistributedLocking(...)`, so one instance is shared per process.

State is held in memory:

- `_tokens`: current token per lock key.
- `_sequenceCounters`: sequence counter per lock key. It is not cleared by revoke or `ClearAllTokens`.

Because state is in-process, tokens are only comparable within the process that issued them. Across processes, the resource must enforce ordering itself (for example, by storing the last accepted sequence number).

### Methods

All methods take a `lockKey` string.

| Method | Behavior |
|--------|----------|
| `FencingToken IssueToken(string lockKey)` | Increments the key's sequence counter (starts at 1), creates a token with a new random string, stores it as current, and returns it. |
| `bool ValidateToken(string lockKey, FencingToken providedToken)` | Returns `false` if no current token exists for the key. Otherwise returns `true` when the provided sequence is greater than the current one, or the provided token equals the current token (same string and sequence). Logs a warning on failure. |
| `void ValidateTokenOrThrow(string lockKey, FencingToken providedToken)` | Same check as `ValidateToken`. On failure throws `InvalidFencingTokenException` with the provided token and the current token string (or `"none"`). |
| `FencingToken? GetToken(string lockKey)` | Returns the current token, or `null`. |
| `FencingToken IncrementToken(string lockKey)` | Replaces the current token with a new one whose sequence is one higher. The previous token then fails validation for ordinary use. Creates the token if none exists. |
| `void RevokeToken(string lockKey)` | Removes the current token for the key. Sequence counting continues on the next issue. |
| `bool IsResourceLocked(string lockKey)` | `true` if a current token exists for the key. |
| `void ClearAllTokens()` | Removes all current tokens. Sequence counters are kept. Intended mainly for tests. |

Notes on behavior:

- `ValidateToken` accepts a provided token whose sequence is greater than the current one. This happens only if a caller presents a token that the service did not issue. In normal use, only the current token exists, so the practical rule is "current token only".
- After `RevokeToken`, `ValidateToken` returns `false` for every token for that key until a new one is issued.
- `IssueToken` and `IncrementToken` both produce a sequence strictly greater than any earlier token for the same key in this process.

## Usage

### Basic issue and validate

```csharp
using SarmKadan.DistributedLock;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Services;

// Resolved from DI after AddDistributedLocking(...)
FencingTokenService tokens = serviceProvider.GetRequiredService<FencingTokenService>();

const string resource = "invoices";

// After acquiring the lock, issue a token for this grant
FencingToken token = tokens.IssueToken(resource);

// Before each write to the protected resource
if (tokens.ValidateToken(resource, token))
{
    // Safe to write
}
else
{
    // Lock was superseded or revoked; abort the write
}
```

### Guarding a write with an exception

```csharp
public async Task SaveInvoiceAsync(FencingToken token, Invoice invoice)
{
    // Throws InvalidFencingTokenException if the token is stale
    _tokens.ValidateTokenOrThrow("invoices", token);

    await _db.Invoices.AddAsync(invoice);
    await _db.SaveChangesAsync();
}
```

### Releasing the lock

```csharp
// On release, revoke the active token so any holder still carrying it is rejected
_tokens.RevokeToken("invoices");
```

### Passing tokens over the wire

`ToString()` gives `"{Token}:{SequenceNumber}"`, which is convenient for headers or message fields:

```csharp
string header = token.ToTokenString();          // e.g. "3f9a0c1e7b2d4a58:42"

if (FencingTokenExtensions.TryParse(header, out var parsed))
{
    bool isNewer = parsed.IsGreaterThanOrEqual(lastAccepted);
}
```

### Working with the model directly

```csharp
var a = new FencingToken("abc123", 5);
var b = a.IncrementSequence();          // sequence 6, new random token string

bool adjacent = a.IsAdjacentTo(b);      // true
long diff = b.SequenceDifference(a);    // 1

// Keep the token string, set a new sequence
var c = a.WithSequenceNumber(10);

// Lifetime check
bool fresh = a.IsValid(TimeSpan.FromSeconds(30));
```

### Persisting the last accepted sequence on the resource side

The service is in-memory, so a durable resource should keep the highest accepted sequence itself:

```csharp
// Pseudocode: accept only strictly newer writes
long lastAccepted = await store.GetLastSequenceAsync(resource);

if (incoming.SequenceNumber <= lastAccepted)
    throw new InvalidOperationException("Stale fencing token");

await store.WriteAsync(resource, data, incoming.SequenceNumber);
```

## Errors

- `InvalidFencingTokenException` (`SarmKadan.DistributedLock.Exceptions`) is thrown by `ValidateTokenOrThrow`. It exposes `ProvidedToken` and `CurrentToken` as strings.
- `FencingToken` constructor and `Parse` throw `ArgumentException` for invalid input.
- Null arguments throw `ArgumentNullException` in all public members.

## See also

- [FencingTokenService](FencingTokenService.md) - per-method reference
- [FencingToken](FencingToken.md) - model reference
- [FencingTokenExtensions](FencingTokenExtensions.md) - extension reference
- `examples/FencingTokenExample.cs` - runnable scenario with an expired lock
