// existing content ...

## InMemoryLockRepository

The `InMemoryLockRepository` class provides an in-memory implementation of the `ILockRepository` interface, primarily intended for testing and development purposes. It stores locks in a dictionary and uses a reader-writer lock to ensure thread safety. Note that this repository is not suitable for production use in a distributed system.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Repository;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Enums;

var repository = new InMemoryLockRepository();

// Acquire a lock
var @lock = new Lock("my-lock", "owner-123");
await repository.AcquireAsync(@lock);

// Check if lock exists
var exists = await repository.ExistsAsync("my-lock");
Console.WriteLine($"Lock exists: {exists}");

// Get lock by key
var lockByKey = await repository.GetByKeyAsync("my-lock");
Console.WriteLine($"Lock by key: {@lockByKey}");

// Renew the lock
var renewed = await repository.RenewAsync("my-lock", "owner-123", TimeSpan.FromSeconds(30));
Console.WriteLine($"Lock renewed: {renewed}");

// Release the lock
var released = await repository.ReleaseAsync("my-lock", "owner-123");
Console.WriteLine($"Lock released: {released}");

// Get all active locks
var activeLocks = await repository.GetAllActiveLockAsync();
Console.WriteLine($"Active locks: {string.Join(", ", activeLocks)}");

// Clear all locks
var clearedCount = await repository.ClearAllAsync();
Console.WriteLine($"Cleared locks: {clearedCount}");
```

This example demonstrates basic usage of the `InMemoryLockRepository`, including acquiring, renewing, releasing, and clearing locks, as well as checking for lock existence and retrieving active locks.
