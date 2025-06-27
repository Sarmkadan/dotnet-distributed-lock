// existing content ...

## LockRenewalFailedExceptionJsonExtensions

The `LockRenewalFailedExceptionJsonExtensions` class provides utility methods to serialize and deserialize `LockRenewalFailedException` instances to and from JSON. This is useful for logging or storing exception details in a database.

### Usage Example

```csharp
var json = LockRenewalFailedException.ToJson(
    new LockRenewalFailedException("Lock renewal failed")
);
Console.WriteLine(json);

var exception = LockRenewalFailedException.FromJson(json);
if (exception != null)
{
    Console.WriteLine(exception.Message);
}

if (LockRenewalFailedException.TryFromJson(json, out var deserializedException))
{
    Console.WriteLine(deserializedException.Message);
}

// Using JsonSerializer with converters
var options = new JsonSerializerOptions();
options.Converters.Add(new LockRenewalFailedExceptionJsonConverter());

var json2 = JsonSerializer.Serialize(
    new LockRenewalFailedException("Test"),
    options
);

var exception2 = JsonSerializer.Deserialize<LockRenewalFailedException>(json2, options);
if (exception2 != null)
{
    Console.WriteLine(exception2.Message);
}
```

## PostgresLockRepositoryExtensions

The `PostgresLockRepositoryExtensions` class provides convenient extension methods for working with a PostgreSQL-backed lock repository. It allows you to acquire and release locks with retry logic, query lock statistics, and inspect locks owned by a specific requester.

### Usage Example

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DistributedLock;
using DistributedLock.Backends.PostgreSQL;

// Create a repository instance (constructor may vary)
var repository = new PostgresLockRepository("Host=localhost;Database=locks;Username=postgres;Password=secret");

// Acquire a lock with retry
bool acquired = await repository.AcquireWithRetryAsync(
    lockKey: "my-resource",
    requesterId: "worker-1",
    mode: LockMode.Exclusive,
    timeout: TimeSpan.FromSeconds(30)
);

// Release the lock with retry
bool released = await repository.ReleaseWithRetryAsync(
    lockKey: "my-resource",
    requesterId: "worker-1"
);

// Query lock statistics
int activeCount = await repository.GetActiveLockCountAsync();
bool hasActive = await repository.HasActiveLocksAsync();
IEnumerable<Lock> ownerLocks = await repository.GetAllLocksByOwnerAsync("worker-1");
int totalCount = await repository.GetTotalLockCountAsync();
int expiredCount = await repository.GetExpiredLockCountAsync();
IEnumerable<Lock> expiringSoon = await repository.GetLocksExpiringSoonAsync();
Lock? oldest = await repository.GetOldestActiveLockAsync();
Lock? newest = await repository.GetNewestActiveLockAsync();
double avgDuration = await repository.GetAverageLockDurationAsync();
```

## LockCleanupWorkerExtensions

The `LockCleanupWorkerExtensions` class provides utility methods for running lock cleanup tasks. It allows you to run a cleanup task once with a specified timeout, get the number of cleaned locks, and configure the worker for testing purposes.

### Usage Example

```csharp
var worker = new LockCleanupWorker();
var cleanedCount = await LockCleanupWorkerExtensions.RunCleanupOnceAsyncWithStats(worker);
Console.WriteLine($"Cleaned {cleanedCount.CleanedCount} locks in {cleanedCount.Duration}");

var testWorker = LockCleanupWorkerExtensions.WithTestInterval(worker, TimeSpan.FromMinutes(1));
LockCleanupWorkerExtensions.LogConfiguration(testWorker);
```

// existing content ...
