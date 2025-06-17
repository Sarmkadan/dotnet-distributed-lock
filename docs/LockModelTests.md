# LockModelTests

Unit tests for the `LockModel` class in the `dotnet-distributed-lock` project, verifying behavior of lock state management, expiration, ownership validation, and renewal mechanics.

## API

### `Constructor_WithValidArguments_SetsPropertiesCorrectly`
Verifies that a `LockModel` instance is correctly initialized when constructed with valid arguments. Ensures `Key`, `OwnerId`, `ExpiresAt`, and `Status` are set as expected.

### `Constructor_WithNullOrWhiteSpaceKey_ThrowsArgumentException`
Ensures that constructing a `LockModel` with a null, empty, or whitespace key throws an `ArgumentException`.

### `Constructor_WithEmptyOwnerId_ThrowsArgumentException`
Ensures that constructing a `LockModel` with an empty or whitespace `OwnerId` throws an `ArgumentException`.

### `Constructor_WithDurationBelowMinimum_ThrowsArgumentException`
Validates that constructing a `LockModel` with a duration below the minimum allowed value throws an `ArgumentException`.

### `IsExpired_WhenExpiresAtIsInThePast_ReturnsTrue`
Confirms that `IsExpired()` returns `true` when the lock's `ExpiresAt` timestamp is in the past.

### `IsExpired_WhenExpiresAtIsInTheFuture_ReturnsFalse`
Confirms that `IsExpired()` returns `false` when the lock's `ExpiresAt` timestamp is in the future.

### `IsValid_WhenStatusIsHeldAndNotExpired_ReturnsTrue`
Ensures that `IsValid()` returns `true` when the lock's `Status` is `Held` and it has not expired.

### `IsValid_WhenStatusIsAcquiredNotHeld_ReturnsFalse`
Ensures that `IsValid()` returns `false` when the lock's `Status` is `Acquired` but not yet `Held`.

### `IsValid_WhenExpired_ReturnsFalse`
Ensures that `IsValid()` returns `false` when the lock has expired, regardless of status.

### `IsCloseToExpiration_WhenRemainingTimeLessThan25Percent_ReturnsTrue`
Validates that `IsCloseToExpiration()` returns `true` when the remaining time until expiration is less than 25% of the total duration.

### `IsCloseToExpiration_WhenRemainingTimeMoreThan25Percent_ReturnsFalse`
Validates that `IsCloseToExpiration()` returns `false` when the remaining time until expiration exceeds 25% of the total duration.

### `Renew_WhenNotExpired_IncrementsRenewalCountAndSetsRenewedAt`
Ensures that calling `Renew()` on a non-expired lock increments the `RenewalCount` and updates the `RenewedAt` timestamp.

### `Renew_WithNewDuration_ExtendsByNewDuration`
Verifies that `Renew(Duration)` extends the lock's expiration by the specified duration from the current `ExpiresAt`.

### `Renew_WhenExpired_ThrowsLockExpiredException`
Confirms that calling `Renew()` on an expired lock throws a `LockExpiredException`.

### `Release_SetsStatusToReleasedAndExpiresImmediately`
Ensures that calling `Release()` sets the lock's `Status` to `Released` and sets `ExpiresAt` to a past timestamp.

### `ValidateOwnership_WithCorrectOwner_DoesNotThrow`
Validates that `ValidateOwnership(string)` does not throw when the provided `ownerId` matches the lock's `OwnerId`.

### `ValidateOwnership_WithWrongOwner_ThrowsLockNotOwnedException`
Ensures that `ValidateOwnership(string)` throws a `LockNotOwnedException` when the provided `ownerId` does not match the lock's `OwnerId`.

### `FencingToken_Constructor_WithNegativeSequenceNumber_ThrowsArgumentException`
Validates that constructing a `FencingToken` with a negative sequence number throws an `ArgumentException`.

### `FencingToken_Constructor_WithEmptyToken_ThrowsArgumentException`
Ensures that constructing a `FencingToken` with an empty or whitespace token string throws an `ArgumentException`.

### `FencingToken_IncrementSequence_CreatesTokenWithSequencePlusOne`
Confirms that calling `IncrementSequence()` on a `FencingToken` returns a new token with a sequence number incremented by one.

## Usage

### Example 1: Basic Lock Lifecycle
