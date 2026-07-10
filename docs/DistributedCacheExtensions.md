# DistributedCacheExtensions

Extension methods for `IDistributedCache` that add JSON serialization support and advanced cache operations such as sliding expiration, pattern-based invalidation, and atomic create-or-get semantics.

## API

### `GetAsJsonAsync<T>`
Deserializes a cached JSON value into an instance of type `T` asynchronously.

- **Parameters**:
  - `cache`: The `IDistributedCache` instance.
  - `key`: The cache key to retrieve.
- **Return value**: The deserialized object of type `T`, or `null` if the key does not exist or the value is not valid JSON.
- **Exceptions**: Throws `JsonException` if the cached value is not valid JSON.

### `SetAsJsonAsync<T>`
Serializes and stores an object of type `T` as JSON in the cache asynchronously.

- **Parameters**:
  - `cache`: The `IDistributedCache` instance.
  - `key`: The cache key to store the value under.
  - `value`: The object to serialize and cache.
  - `options` (optional): Optional `DistributedCacheEntryOptions` to configure expiration and other settings.
- **Return value**: A `Task` representing the asynchronous operation.
- **Exceptions**: Throws `JsonException` if the object cannot be serialized.

### `GetOrCreateAsync<T>`
Retrieves a value from the cache or creates and stores it if it does not exist, using the provided factory function asynchronously.

- **Parameters**:
  - `cache`: The `IDistributedCache` instance.
  - `key`: The cache key to retrieve or create.
  - `factory`: A function that returns a `Task<T>` producing the value to cache if the key does not exist.
  - `options` (optional): Optional `DistributedCacheEntryOptions` to configure expiration and other settings.
- **Return value**: The cached or newly created value of type `T`.
- **Exceptions**: Propagates exceptions from the `factory` function or `SetAsJsonAsync<T>`.

### `RemoveAsync`
Removes a cache entry by key asynchronously.

- **Parameters**:
  - `cache`: The `IDistributedCache` instance.
  - `key`: The cache key to remove.
- **Return value**: A `Task` representing the asynchronous operation.
- **Exceptions**: None.

### `ExistsAsync`
Checks whether a cache entry with the given key exists asynchronously.

- **Parameters**:
  - `cache`: The `IDistributedCache` instance.
  - `key`: The cache key to check.
- **Return value**: `true` if the key exists; otherwise, `false`.
- **Exceptions**: None.

### `SetExpirationAsync`
Updates the absolute expiration time of an existing cache entry asynchronously.

- **Parameters**:
  - `cache`: The `IDistributedCache` instance.
  - `key`: The cache key whose expiration should be updated.
  - `absoluteExpiration`: The new absolute expiration time.
- **Return value**: A `Task` representing the asynchronous operation.
- **Exceptions**: Throws `ArgumentNullException` if `key` is `null`. Throws `InvalidOperationException` if the key does not exist.

### `InvalidatePatternAsync`
Removes all cache entries whose keys match the provided pattern asynchronously.

- **Parameters**:
  - `cache`: The `IDistributedCache` instance.
  - `pattern`: A string pattern to match keys against (e.g., `"user:*"`).
- **Return value**: A `Task` representing the asynchronous operation.
- **Exceptions**: None.

### `SetWithSlidingExpirationAsync<T>`
Stores an object of type `T` as JSON with a sliding expiration policy asynchronously.

- **Parameters**:
  - `cache`: The `IDistributedCache` instance.
  - `key`: The cache key to store the value under.
  - `value`: The object to serialize and cache.
  - `slidingExpiration`: The sliding expiration duration.
  - `options` (optional): Optional additional `DistributedCacheEntryOptions`.
- **Return value**: A `Task` representing the asynchronous operation.
- **Exceptions**: Throws `JsonException` if the object cannot be serialized. Throws `ArgumentNullException` if `key` is `null` or `slidingExpiration` is not positive.

## Usage
