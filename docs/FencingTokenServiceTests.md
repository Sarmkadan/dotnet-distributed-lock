# FencingTokenServiceTests

Overview of the test suite that validates the behavior of the fencing token service implementation. The class contains unit tests that verify token issuance, retrieval, validation, revocation, incrementing, and lock‑name validation under various conditions.

## API

### IssueToken_ForNewKey_ReturnsTokenWithPositiveSequence
- **Purpose**: Confirms that calling `IssueToken` with a previously unseen key produces a token whose sequence number is greater than zero.
- **Parameters**: None.
- **Return Value**: `void` (test method).
- **When it throws**: Throws an exception (e.g., `Xunit.Assert` failure) if the returned token’s sequence is not positive.

### IssueToken_CalledTwice_SecondTokenHasHigherSequence
- **Purpose**: Ensures that successive calls to `IssueToken` for the same key yield tokens with strictly increasing sequence numbers.
- **Parameters**: None.
- **Return Value**: `void`.
- **When it throws**: Throws an exception if the second token’s sequence is not higher than the first’s.

### IssueToken_DifferentKeys_AreTrackedIndependently
- **Purpose**: Verifies that tokens for distinct keys maintain independent sequence counters.
- **Parameters**: None.
- **Return Value**: `void`.
- **When it throws**: Throws an exception if issuing a token for one key affects the sequence of another key.

### GetToken_ForUnknownKey_ReturnsNull
- **Purpose**: Checks that requesting a token for a key that has never been issued returns `null`.
- **Parameters**: None.
- **Return Value**: `void`.
- **When it throws**: Throws an exception if a non‑null token is returned for an unknown key.

### GetToken_AfterIssue_ReturnsSameToken
- **Purpose**: Asserts that after issuing a token for a key, subsequent calls to `GetToken` return the exact same token instance (or an equivalent token with the same sequence).
- **Parameters**: None.
- **Return Value**: `void`.
- **When it throws**: Throws an exception if the retrieved token differs from the originally issued token.

### ValidateToken_WithExactCurrentToken_ReturnsTrue
- **Purpose**: Validates that a token matching the current token for its key is considered valid.
- **Parameters**: None.
- **Return Value**: `void`.
- **When it throws**: Throws an exception if validation returns `false` for the exact current token.

### ValidateToken_WithHigherSequenceToken_ReturnsTrue
- **Purpose**: Confirms that a token with a sequence number higher than the current token is accepted as valid (supporting fencing semantics).
- **Parameters**: None.
- **Return Value**: `void`.
- **When it throws**: Throws an exception if validation returns `false` for a higher‑sequence token.

### ValidateToken_WithStaleToken_ReturnsFalse
- **Purpose**: Ensures that a token with a sequence number lower than the current token is rejected as stale.
- **Parameters**: None.
- **Return Value**: `void`.
- **When it throws**: Throws an exception if validation returns `true` for a stale token.

### ValidateToken_ForUnknownKey_ReturnsFalse
- **Purpose**: Verifies that validation of a token for a key that has never been issued returns `false`.
- **Parameters**: None.
- **Return Value**: `void`.
- **When it throws**: Throws an exception if validation returns `true` for an unknown key.

### ValidateTokenOrThrow_WithInvalidToken_ThrowsInvalidFencingTokenException
- **Purpose**: Checks that `ValidateTokenOrThrow` throws `InvalidFencingTokenException` when presented with an invalid token (unknown key or stale sequence).
- **Parameters**: None.
- **Return Value**: `void`.
- **When it throws**: Throws an exception if no exception is thrown, or if a different exception type is thrown.

### ValidateTokenOrThrow_WithValidToken_DoesNotThrow
- **Purpose**: Confirms that `ValidateTokenOrThrow` completes without throwing when given a valid token (current or higher sequence).
- **Parameters**: None.
- **Return Value**: `void`.
- **When it throws**: Throws an exception if an unexpected exception is raised.

### RevokeToken_AfterRevoke_GetTokenReturnsNull
- **Purpose**: Ensures that after revoking a token for a key, subsequent `GetToken` calls return `null`.
- **Parameters**: None.
- **Return Value**: `void`.
- **When it throws**: Throws an exception if a non‑null token is returned after revocation.

### RevokeToken_ForNonExistentKey_DoesNotThrow
- **Purpose**: Verifies that attempting to revoke a token for a key that has never been issued does not raise an exception.
- **Parameters**: None.
- **Return Value**: `void`.
- **When it throws**: Throws an exception if any exception is observed during the revocation call.

### IncrementToken_WhenTokenExists_ReturnsTokenWithIncrementedSequence
- **Purpose**: Confirms that calling `IncrementToken` on an existing token yields a token whose sequence number is exactly one greater than the original.
- **Parameters**: None.
- **Return Value**: `void`.
- **When it throws**: Throws an exception if the returned token’s sequence is not the expected increment.

### IncrementToken_WhenNoExistingToken_IssuesNewToken
- **Purpose**: Ensures that invoking `IncrementToken` for a key with no current token results in the issuance of a new token with a positive sequence (typically 1).
- **Parameters**: None.
- **Return Value**: `void`.
- **When it throws**: Throws an exception if the returned token is `null` or has a non‑positive sequence.

### ClearAllTokens_RemovesEveryTrackedToken
- **Purpose**: Asserts that after calling `ClearAllTokens`, any subsequent `GetToken` call for any previously known key returns `null`.
- **Parameters**: None.
- **Return Value**: `void`.
- **When it throws**: Throws an exception if any token remains retrievable after the clear operation.

### IncrementToken_WithLargeSequenceNumber_HandlesOverflow
- **Purpose**: Validates that the service correctly handles sequence number overflow (e.g., wrapping to zero or using a larger numeric type) when incrementing a token at the maximum representable value.
- **Parameters**: None.
- **Return Value**: `void`.
- **When it throws**: Throws an exception if overflow results in an invalid token or an unexpected exception.

### ValidateLockName_WithValidName_DoesNotThrow
- **Purpose**: Confirms that supplying a well‑formed lock name to the validation routine does not cause an exception.
- **Parameters**: None.
- **Return Value**: `void`.
- **When it throws**: Throws an exception if an exception is thrown for a valid name.

### ValidateLockName_WithNullOrWhiteSpace_ThrowsInvalidOperationException
- **Purpose**: Ensures that passing `null`, empty, or whitespace‑only strings to the lock‑name validator results in an `InvalidOperationException`.
- **Parameters**: None.
- **Return Value**: `void`.
- **When it throws**: Throws an exception if the validator does not throw `InvalidOperationException` for invalid input.

### ValidateLockName_WithInvalidCharacters_ThrowsInvalidOperationException
- **Purpose**: Verifies that lock names containing characters outside the allowed set trigger an `InvalidOperationException`.
- **Parameters**: None.
- **Return Value**: `void`.
- **When it throws**: Throws an exception if no exception is raised, or if a different exception type is thrown, for an invalid‑character name.

## Usage

```csharp
// Example 1: Executing a single test method directly.
var tests = new FencingTokenServiceTests();
tests.IssueToken_ForNewKey_ReturnsTokenWithPositiveSequence(); // passes if the service behaves correctly
```

```csharp
// Example 2: Running a group of related tests in a custom test harness.
var tests = new FencingTokenServiceTests();
tests.IssueToken_CalledTwice_SecondTokenHasHigherSequence();
tests.IssueToken_DifferentKeys_AreTrackedIndependently();
tests.GetToken_AfterIssue_ReturnsSameToken();
// If any assertion fails, an exception will be propagated from the corresponding method.
```

## Notes

- The test methods are deterministic; they rely on a fresh instance of the underlying fencing token service per test (or on proper state reset between calls).  
- Thread safety is not exercised by these tests; the service is expected to be used from a single thread in the test scenarios. Concurrent access would require external synchronization or internal locking, which is not verified here.  
- Sequence overflow handling assumes the service uses an unsigned integer type with well‑defined wrap‑around behavior; the test validates that the implementation does not throw when the maximum value is incremented.  
- Validation methods treat any token with a sequence number lower than the current token as stale and reject it, while equal or higher sequences are accepted.  
- Lock‑name validation is deliberately strict: `null`, empty, whitespace, or any non‑alphanumeric/dash/underscore characters cause an `InvalidOperationException`.  
- Revoking a non‑existent key is a no‑op and must not throw; this protects callers from having to check existence beforehand.  
- All test methods return `void` and signal failure only by throwing an exception (typically from the assertion framework). Successful execution results in no observable output.
