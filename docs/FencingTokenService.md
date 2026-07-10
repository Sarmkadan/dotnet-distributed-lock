# FencingTokenService

`FencingTokenService` provides the fencing-token mechanism for the distributed lock system. It issues monotonically increasing tokens that serve as a guard against split-brain scenarios: a lock holder must present its current token when accessing a protected resource, and the service validates that the token has not been superseded or revoked. This ensures that even if a lock appears to be held by a stale participant, any attempt to use the resource with an outdated token is rejected.

## API

### `FencingTokenService`

Constructor. Initializes a new instance of the fencing token service, ready to issue and validate tokens for a specific logical resource.

### `FencingToken IssueToken`

Issues a new fencing token. The returned token is guaranteed to be strictly greater than any previously issued token for the same resource. The caller should associate this token with a lock acquisition and present it when accessing the protected resource.

- **Returns:** A new `FencingToken` instance with a value higher than all prior tokens.

### `bool ValidateToken`

Validates whether a given fencing token is still the current, non-revoked token for the resource. Returns `true` if the token is valid and access should be permitted; `false` if the token has been superseded by a newer issuance or explicitly revoked.

- **Parameters:** The `FencingToken` to validate.
- **Returns:** `true` if the token is the active, unrevoked token; otherwise `false`.

### `FencingToken? GetToken`

Retrieves the currently active fencing token for the resource, if one exists. Returns `null` when no token has been issued or all tokens have been cleared.

- **Returns:** The current `FencingToken`, or `null` if none is active.

### `void RevokeToken`

Explicitly revokes a specific fencing token so that it is no longer considered valid, even if it was the most recently issued token. After revocation, `ValidateToken` returns `false` for that token, and the resource is considered unlocked until a new token is issued.

- **Parameters:** The `FencingToken` to revoke.

### `FencingToken IncrementToken`

Issues a new token that supersedes the current one, equivalent to calling `IssueToken` but explicitly signaling an intent to advance the fencing boundary. The previous token becomes invalid.

- **Returns:** A new `FencingToken` with a value greater than the previously active token.

### `void ValidateTokenOrThrow`

Validates the given token and throws an exception if it is not the current, non-revoked token. This is a convenience method for guard clauses where access should be denied immediately on an invalid token.

- **Parameters:** The `FencingToken` to validate.
- **Throws:** An exception (typically `InvalidOperationException` or a fencing-specific exception) when the token is invalid or revoked.

### `bool IsResourceLocked`

Indicates whether the resource currently has an active, non-revoked fencing token. Returns `true` if a token has been issued and not revoked or cleared; `false` otherwise.

- **Returns:** `true` if an active token exists; `false` if no token is present.

### `void ClearAllTokens`

Removes all issued and tracked tokens for the resource, resetting the service to its initial state. After this call, `IsResourceLocked` returns `false`, `GetToken` returns `null`, and any previously issued tokens are considered invalid.

## Usage

### Example 1: Acquire lock, access resource, release

```csharp
var fencingService = new FencingTokenService();

// Acquire the distributed lock and obtain a fencing token
FencingToken token = fencingService.IssueToken();

// Before accessing the shared resource, validate the token
fencingService.ValidateTokenOrThrow(token);

// Perform protected work
Console.WriteLine("Accessing resource with token.");

// Explicitly revoke the token when done (release semantics)
fencingService.RevokeToken(token);
```

### Example 2: Increment token on leader re-election

```csharp
var fencingService = new FencingTokenService();

// Initial leader acquires token
FencingToken currentToken = fencingService.IssueToken();

// Later, a new leader is elected and increments the fencing boundary
FencingToken newToken = fencingService.IncrementToken();

// The old leader attempts to use its stale token
if (!fencingService.ValidateToken(currentToken))
{
    Console.WriteLine("Stale token rejected — new leader is active.");
}

// New leader proceeds safely
fencingService.ValidateTokenOrThrow(newToken);
Console.WriteLine("New leader accessing resource.");
```

## Notes

- **Monotonicity:** Each call to `IssueToken` or `IncrementToken` produces a token strictly greater than all previous tokens. This ordering is the foundation of the fencing guarantee.
- **Revocation vs. clearing:** `RevokeToken` invalidates a specific token but does not reset the service’s internal state entirely; a subsequent `IssueToken` will still produce a higher value. `ClearAllTokens` fully resets the service, discarding all history.
- **Thread safety:** The service is designed for use in concurrent environments. All public methods that mutate or read token state are safe to call from multiple threads without external synchronization.
- **Null token handling:** Passing a null token to `ValidateToken`, `RevokeToken`, or `ValidateTokenOrThrow` is not meaningful and will typically result in a `false` return or an exception, depending on the method.
- **Stale token rejection:** Once a token is superseded by a new issuance or explicitly revoked, it can never become valid again. There is no mechanism to restore a revoked or outdated token.
- **Resource identity:** Each instance of `FencingTokenService` manages tokens for a single logical resource. To protect multiple distinct resources, create separate instances.
