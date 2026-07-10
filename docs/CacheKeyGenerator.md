# CacheKeyGenerator

The `CacheKeyGenerator` class provides a collection of static helper methods for constructing and interpreting the Redis key names used by the distributed lock implementation. Centralizing key generation ensures consistent naming across lock acquisition, release, metrics, and administrative operations, reducing the risk of collisions and simplifying key‑based queries.

## API

### GenerateLockKey
**Purpose:** Creates the Redis key that stores the lock state for a given resource.  
**Parameters:**  
- `resourceId` (string) – Identifier of the resource to lock. Must not be null or empty.  
**Return value:** A string representing the full lock key.  
**Exceptions:**  
- `ArgumentNullException` if `resourceId` is null.  
- `ArgumentException` if `resourceId` is empty or consists only of white‑space.

### GenerateLockNameKey
**Purpose:** Produces a key used to track metadata associated with a specific lock name (e.g., lock type or category).  
**Parameters:**  
- `lockName` (string) – Name of the lock. Must not be null or empty.  
**Return value:** A string key for lock‑name metadata.  
**Exceptions:**  
- `ArgumentNullException` if `lockName` is null.  
- `ArgumentException` if `lockName` is empty or white‑space.

### GenerateMetricsKey
**Purpose:** Generates the key under which lock‑related metrics (e.g., acquisition latency, contention) are stored for a resource.  
**Parameters:**  
- `resourceId` (string) – Identifier of the resource. Must not be null or empty.  
**Return value:** A string key for metrics data.  
**Exceptions:**  
- `ArgumentNullException` if `resourceId` is null.  
- `ArgumentException` if `resourceId` is empty or white‑space.

### GenerateSystemMetricsKey
**Purpose:** Returns the key used for system‑wide metrics that are not tied to a particular resource (e.g., total lock count).  
**Parameters:** None.  
**Return value:** A string key for system metrics.  
**Exceptions:** None.

### GenerateStatusKey
**Purpose:** Creates the key that holds the current status (e.g., held, waiting) of a lock for a resource.  
**Parameters:**  
- `resourceId` (string) – Identifier of the resource. Must not be null or empty.  
**Return value:** A string key for lock status.  
**Exceptions:**  
- `ArgumentNullException` if `resourceId` is null.  
- `ArgumentException` if `resourceId` is empty or white‑space.

### GenerateOwnerLocksKey
**Purpose:** Produces a key that enumerates all locks currently owned by a specific owner (e.g., a client instance).  
**Parameters:**  
- `ownerId` (string) – Identifier of the lock owner. Must not be null or empty.  
**Return value:** A string key for the owner’s lock set.  
**Exceptions:**  
- `ArgumentNullException` if `ownerId` is null.  
- `ArgumentException` if `ownerId` is empty or white‑space.

### GenerateActiveLockKeysPattern
**Purpose:** Returns a pattern (suitable for `SCAN` or `KEYS`) that matches all active lock keys for a given resource.  
**Parameters:**  
- `resourceId` (string) – Identifier of the resource. Must not be null or empty.  
**Return value:** A string pattern that can be used to retrieve active lock keys.  
**Exceptions:**  
- `ArgumentNullException` if `resourceId` is null.  
- `ArgumentException` if `resourceId` is empty or white‑space.

### GenerateQueryKey
**Purpose:** Generates a key used for auxiliary query data (e.g., pending lock requests) associated with a resource.  
**Parameters:**  
- `resourceId` (string) – Identifier of the resource. Must not be null or empty.  
**Return value:** A string key for query data.  
**Exceptions:**  
- `ArgumentNullException` if `resourceId` is null.  
- `ArgumentException` if `resourceId` is empty or white‑space.

### GenerateConfigurationKey
**Purpose:** Creates the key under which lock configuration (e.g., timeout, retry policy) is stored for a resource.  
**Parameters:**  
- `resourceId` (string) – Identifier of the resource. Must not be null or empty.  
**Return value:** A string key for configuration data.  
**Exceptions:**  
- `ArgumentNullException` if `resourceId` is null.  
- `ArgumentException` if `resourceId` is empty or white‑space.

### GenerateTagKey
**Purpose:** Produces a key used to associate a user‑defined tag with a lock resource (useful for grouping or filtering).  
**Parameters:**  
- `tag` (string) – The tag value. Must not be null or empty.  
**Return value:** A string key for the tag.  
**Exceptions:**  
- `ArgumentNullException` if `tag` is null.  
- `ArgumentException` if `tag` is empty or white‑space.

### ExtractLockIdFromKey
**Purpose:** Attempts to parse a lock‑related Redis key and return the embedded lock identifier (if the key matches the expected format).  
**Parameters:**  
- `key` (string) – The Redis key to inspect. May be null.  
**Return value:** The extracted lock identifier as a string, or `null` if the key does not conform to the lock key pattern.  
**Exceptions:** None.

### IsLockKey
**Purpose:** Determines whether a given string matches the pattern of a lock key generated by this class.  
**Parameters:**  
- `key` (string) – The key to test. May be null.  
**Return value:** `true` if the key is a lock key; otherwise `false`.  
**Exceptions:** None.

### IsMetricsKey
**Purpose:** Determines whether a given string matches the pattern of a metrics key generated by this class.  
**Parameters:**  
- `key` (string) – The key to test. May be null.  
**Return value:** `true` if the key is a metrics key; otherwise `false`.  
**Exceptions:** None.

### GetKeysByAcquisition
**Purpose:** Returns an array of all Redis keys that are touched when acquiring a lock for the specified resource (e.g., lock, status, metrics, query keys).  
**Parameters:**  
- `resourceId` (string) – Identifier of the resource. Must not be null or empty.  
**Return value:** A string array containing the relevant keys in a deterministic order.  
**Exceptions:**  
- `ArgumentNullException` if `resourceId` is null.  
- `ArgumentException` if `resourceId` is empty or white‑space.

### GetKeysByRelease
**Purpose:** Returns an array of all Redis keys that are touched when releasing a lock for the specified resource (e.g., lock, status, owner set).  
**Parameters:**  
- `resourceId` (string) – Identifier of the resource. Must not be null or empty.  
**Return value:** A string array containing the relevant keys in a deterministic order.  
**Exceptions:**  
- `ArgumentNullException` if `resourceId` is null.  
- `ArgumentException` if `resourceId` is empty or white‑space.

## Usage

```csharp
using DotNetDistributedLock;

// Acquiring a lock for a resource named "inventory:item-42"
string resourceId = "inventory:item-42";
string lockKey = CacheKeyGenerator.GenerateLockKey(resourceId);
string statusKey = CacheKeyGenerator.GenerateStatusKey(resourceId);
string[] acquisitionKeys = CacheKeyGenerator.GetKeysByAcquisition(resourceId);

// Example: using the keys with a Redis client (pseudo‑code)
// db.LockTake(lockKey, ownerId, TimeSpan.FromSeconds(30));
// db.HashSet(statusKey, new HashEntry[] { new("owner", ownerId), new("acquiredAt", DateTime.UtcNow.ToString()) });
// foreach (var k in acquisitionKeys) { /* ensure keys exist or set TTL */ }
```

```csharp
using DotNetDistributedLock;

// Inspecting a key retrieved from Redis
string redisKey = "lock:inventory:item-42";
if (CacheKeyGenerator.IsLockKey(redisKey))
{
    string? lockId = CacheKeyGenerator.ExtractLockIdFromKey(redisKey);
    Console.WriteLine($"Lock ID: {lockId ?? "<unknown>"}");
}
else if (CacheKeyGenerator.IsMetricsKey(redisKey))
{
    Console.WriteLine("The key holds metrics data.");
}
else
{
    Console.WriteLine("Unrecognized key type.");
}
```

## Notes

- All methods are **static** and contain no mutable state; therefore they are inherently thread‑safe and can be called concurrently from multiple threads without external synchronization.  
- Parameter validation is performed consistently: null arguments raise `ArgumentNullException`, while empty or whitespace‑only strings raise `ArgumentException`.  
- `ExtractLockIdFromKey` returns `null` when the supplied key does not match the expected lock‑key format; callers should check for `null` before using the result.  
- The arrays returned by `GetKeysByAcquisition` and `GetKeysByRelease` are ordered to reflect the typical sequence of operations (e.g., lock key first, then auxiliary keys). Consumers should not rely on the exact ordering beyond the guarantee that all necessary keys for the operation are present.  
- Keys generated for different resources are guaranteed to be distinct as long as the supplied `resourceId`, `lockName`, `ownerId`, or `tag` values differ; however, the caller remains responsible for ensuring those identifiers are unique within their application context.  
- No method performs I/O; all work is pure string manipulation, making the class suitable for use in hot paths such as lock acquisition loops.
