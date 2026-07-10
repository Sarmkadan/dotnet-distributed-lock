# InvalidFencingTokenException

Exception thrown when a provided fencing token does not match the current expected token for a distributed lock. This ensures safe coordination in distributed systems by preventing stale or conflicting lock acquisitions.

## API

### Constructors

#### `InvalidFencingTokenException()`
Initializes a new instance of the `InvalidFencingTokenException` class with default values for the `ProvidedToken` and `CurrentToken` properties.

#### `InvalidFencingTokenException(string providedToken, string currentToken)`
Initializes a new instance of the `InvalidFencingTokenException` class with the specified provided and current fencing tokens.

- **providedToken**: The fencing token provided by the caller when attempting to acquire or renew the lock.
- **currentToken**: The fencing token currently associated with the lock.

## Usage

### Example 1: Handling an invalid token during lock acquisition
