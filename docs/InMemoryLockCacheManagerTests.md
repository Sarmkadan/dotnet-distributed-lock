# InMemoryLockCacheManagerTests

Unit tests for the `InMemoryLockCacheManager` class, which provides an in-memory cache for distributed lock entries. These tests verify the correct behavior of lock storage, retrieval, removal, and statistics tracking in a single-process scenario.

## API

### `InMemoryLockCacheManagerTests`
Constructor for the test class. Initializes a new instance of the test suite targeting the in-memory lock cache manager.

### `Task GetAsync_WithEmptyCache_ReturnsNull()`
Verifies that retrieving a lock by a non-existent identifier from an empty cache returns `null`.

### `Task GetAsync_AfterSet_ReturnsCachedLock()`
Ensures that a lock stored via `SetAsync` can be retrieved immediately afterward using the same identifier.

### `Task GetAsync_WithNullId_ReturnsNull()`
Confirms that attempting to retrieve a lock using a `null` identifier returns `null` without throwing an exception.

### `Task GetAsync_WithEmptyId_ReturnsNull()`
Confirms that attempting to retrieve a lock using an empty string identifier returns `null` without throwing an exception.

### `Task SetAsync_WithNullLock_DoesNotThrow()`
Validates that calling `SetAsync` with a `null` lock object does not throw an exception and handles the case gracefully.

### `Task SetAsync_StoreLock_CanBeRetrieved()`
Ensures that a lock stored via `SetAsync` can be successfully retrieved using `GetAsync` with the same identifier.

### `Task SetAsync_MultiipleLocks_AllCanBeRetrieved()`
Verifies that multiple distinct locks can be stored and individually retrieved without interference.

### `Task SetAsync_OverwriteExistingLock()`
Tests that storing a lock under an identifier that already exists overwrites the previous entry.

### `Task RemoveAsync_RemovesLock()`
Confirms that calling `RemoveAsync` with a valid identifier removes the corresponding lock from the cache.

### `Task RemoveAsync_WithNonexistentLock_DoesNotThrow()`
Ensures that attempting to remove a lock with a non-existent identifier does not throw an exception.

### `Task RemoveAsync_WithEmptyId_DoesNotThrow()`
Ensures that attempting to remove a lock with an empty string identifier does not throw an exception.

### `Task GetAllAsync_WithEmptyCache_ReturnsEmptyList()`
Verifies that retrieving all locks from an empty cache returns an empty collection.

### `Task GetAllAsync_ReturnsAllCachedLocks()`
Confirms that `GetAllAsync` returns all locks currently stored in the cache.

### `Task GetAllAsync_DoesNotReturnRemovedLocks()`
Ensures that locks removed via `RemoveAsync` are not included in the collection returned by `GetAllAsync`.

### `Task ClearAsync_RemovesAllLocks()`
Validates that calling `ClearAsync` removes all locks from the cache.

### `Task ClearAsync_WithEmptyCache_DoesNotThrow()`
Ensures that calling `ClearAsync` on an empty cache does not throw an exception.

### `Task GetStatistics_InitialState_HasZeroHitsAndMisses()`
Confirms that the statistics returned by `GetStatistics` start at zero hits and zero misses.

### `Task GetStatistics_AfterCacheHit_RecordsHit()`
Verifies that a successful lock retrieval via `GetAsync` increments the hit counter in the statistics.

### `Task GetStatistics_AfterCacheMiss_RecordsMiss()`
Verifies that a failed lock retrieval via `GetAsync` increments the miss counter in the statistics.

## Usage
