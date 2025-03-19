# Frequently Asked Questions

## General Questions

### What is a distributed lock?

A distributed lock is a synchronization primitive that allows multiple processes or servers to coordinate exclusive access to a shared resource. Only one owner can hold the lock at a time, preventing concurrent access and race conditions.

### When should I use a distributed lock?

Use distributed locks when:
- Multiple services/processes access the same resource
- You need to prevent concurrent modifications
- You need to coordinate work across multiple machines
- You have a single critical resource that needs exclusive access

**Examples:**
- Database migrations (prevent simultaneous schema changes)
- Scheduled job coordination (ensure only one instance runs)
- Data processing pipelines (coordinate stages across workers)
- Configuration updates (atomic, coordinated changes)

### When should I NOT use a distributed lock?

Don't use when:
- You need fine-grained locking (use database-level row locks)
- You need reader-writer differentiation (use read-write locks)
- You only have single-threaded access (use language-level locks)
- Performance is critical (locks have overhead)

### How is this different from database-level locking?

| Aspect | Distributed Lock | Database Lock |
|--------|------------------|----------------|
| Scope | Across services | Single database |
| Granularity | Coarse (whole resource) | Fine (rows, pages) |
| Setup | Application layer | Built into database |
| Coordination | Via backend service | Via database server |
| Use case | Service coordination | Data consistency |

## Deployment Questions

### What's the best backend for production?

**By scenario:**
- **High throughput (10K+ ops/sec)**: Redis or Redis Cluster
- **SQL-first architecture**: PostgreSQL
- **Single-server deployment**: SQLite
- **Multi-server with strong consistency**: PostgreSQL with replication
- **Simple, embedded use**: SQLite

### Can I switch backends after deployment?

**Technically yes, but with caveats:**
1. Existing locks won't migrate
2. Brief service disruption needed
3. Migration script required

**Recommended approach:**
- Run parallel backends during transition
- Copy active locks to new backend
- Verify locks are synchronized
- Then switch to new backend

### How do I handle backend failures?

**Redis:**
- Use Redis Sentinel for automatic failover
- Or use Redis Cluster for distributed resilience

**PostgreSQL:**
- Use replication (streaming replication)
- Set up hot standby for failover
- Use external HA tool (patroni, etcd)

**SQLite:**
- Backup regularly to remote storage
- Restore from backup on failure
- Consider moving to PostgreSQL for HA

### Should I use the same database as my application?

**Pros of shared database:**
- Simplified deployment
- Single database to manage
- Shared connection pool

**Cons of shared database:**
- Lock contention affects application queries
- Locks schema locked with application schema
- Database migrations more complex

**Recommendation:** Use separate database for locks in production, shared database for development.

## Configuration Questions

### What's a good default lock duration?

**Rule of thumb:** 3-5x your expected operation time

**Examples:**
- Database migration: 5-10 minutes
- Report generation: 10-30 minutes
- Scheduled job: 5-15 minutes
- Short operation: 30 seconds

**Too short:**
- Frequent renewals (overhead)
- Zombie detection faster

**Too long:**
- Delayed failure detection
- Orphaned locks remain longer

### Should I enable fencing tokens?

**Yes, for write operations.** Fencing tokens prevent zombie writes:

```csharp
// Without tokens, this could write even after lock expires:
await lockService.AcquireAsync("data-store", "worker-1");
// Long operation...
// Lock expires but process continues
await WriteDataAsync();  // ❌ Unsafe!

// With tokens, this is safe:
var token = tokenService.IssueToken("data-store");
if (tokenService.ValidateToken("data-store", token))
{
    await WriteDataAsync();  // ✅ Safe
}
```

### What's the difference between Blocking and NonBlocking modes?

**AcquisitionMode.NonBlocking:**
```csharp
// Returns null immediately if locked
var @lock = await lockService.TryAcquireAsync(...);
if (@lock == null)
{
    // Skip work if locked
}
```

**AcquisitionMode.Blocking:**
```csharp
// Retries until lock acquired or timeout
try
{
    var @lock = await lockService.AcquireAsync(...);
    // Lock is guaranteed acquired
}
catch (LockAcquisitionException)
{
    // Timeout reached
}
```

**Use NonBlocking when:**
- You can skip the operation
- You want immediate feedback
- You don't want to wait

**Use Blocking when:**
- The operation must happen
- You're okay waiting for the lock
- Coordination is critical

## Troubleshooting Questions

### Lock acquisition always fails

**Check:**
1. Backend service is running and accessible
2. Connection string is correct
3. Network connectivity is available
4. Database/Redis has space

**Debug:**
```csharp
// Enable debug logging
services.AddLogging(builder =>
{
    builder.SetMinimumLevel(LogLevel.Debug);
    builder.AddConsole();
});

// Try acquiring a test lock
await lockService.AcquireAsync("test", "owner");
```

### Process continues writing after lock expires

**Problem:** Zombie writes - process continues after lock timeout

**Solution:** Enable and validate fencing tokens

```csharp
options.UseFencingTokens = true;

// Before each write:
var token = tokenService.IssueToken(resourceId);
if (!tokenService.ValidateToken(resourceId, token))
{
    throw new InvalidOperationException("Lock expired");
}
```

### Performance is degrading

**Check:**
1. Lock contention: `metrics.CurrentActiveLocks`
2. Backend resource usage (CPU, memory, I/O)
3. Network latency to backend
4. Lock duration configuration

**Optimize:**
```csharp
// Use shorter lock durations
options.DefaultLockDuration = TimeSpan.FromSeconds(10);

// Enable caching for reads
options.EnableCaching = true;
options.CacheDurationSeconds = 30;

// Monitor metrics
var metrics = lockService.GetMetrics();
```

### PostgreSQL says "relation locks does not exist"

**Solution:** Create the locks table

```sql
CREATE TABLE locks (
    key VARCHAR(255) PRIMARY KEY,
    owner_id VARCHAR(255) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMP NOT NULL,
    status VARCHAR(50) NOT NULL,
    renewal_count INTEGER NOT NULL DEFAULT 0
);
```

### Redis says "WRONGTYPE Operation against a key holding the wrong kind of value"

**Cause:** Lock key conflicts with other data in Redis

**Solution:**
- Use namespaced keys: `"app:lock:resource-name"`
- Use separate Redis database: `connectionString,defaultDatabase=1`
- Use different Redis instance

### Lock doesn't auto-renew

**Check:**
1. Is `LockMonitor.StartMonitoring()` called?
2. Is lock registered: `monitor.RegisterLock(...)`?
3. Is the owner ID correct?
4. Is the lock still owned?

**Debug:**
```csharp
monitor.RegisterLock(lockKey, ownerId, renewalInterval, duration);
monitor.StartMonitoring(checkInterval);

// Check if lock still exists
var @lock = await lockService.GetLockAsync(lockKey);
Console.WriteLine($"Lock exists: {lock != null}");
```

### Too many connections to database

**Check PostgreSQL:**
```sql
SELECT count(*) FROM pg_stat_activity;
```

**Solutions:**
1. Increase `max_connections` in postgresql.conf
2. Use connection pooler (PgBouncer)
3. Reduce application instances
4. Close unused connections

**Connection pooling in code:**
```csharp
// In connection string:
"...;Max Pool Size=50;Min Pool Size=10;..."
```

## Performance Questions

### How many locks can the system handle?

**Depends on backend:**

| Backend | Max Locks | Lock/sec | Latency |
|---------|-----------|----------|---------|
| Redis | Millions | 10K+ | <5ms |
| PostgreSQL | 100K | 1K-5K | 10-50ms |
| SQLite | 10K | 1K-2K | 0.1-1ms |
| InMemory | 1M | 100K+ | <0.01ms |

### How much memory does a lock consume?

**Approximate overhead per lock:**
- In-Memory: ~500 bytes
- Redis: ~100 bytes (network overhead)
- PostgreSQL: ~1 KB (row overhead)
- SQLite: ~1 KB (page overhead)

### Does caching really help?

**Benchmark results:**
- Without cache: 10ms per IsLockedAsync call
- With cache (hit): <1ms per call
- Cache hit rate: Usually 80-95%

**Enable caching:**
```csharp
options.EnableCaching = true;
options.CacheDurationSeconds = 30;
```

## Best Practices

### Do

✅ Always release locks in finally blocks
```csharp
try
{
    await lockService.AcquireAsync(...);
    // Work
}
finally
{
    await lockService.ReleaseAsync(...);
}
```

✅ Use using pattern when possible
```csharp
using (await lockService.AcquireAsync(...))
{
    // Work
}
```

✅ Enable fencing tokens for write operations
```csharp
options.UseFencingTokens = true;
```

✅ Set reasonable lock durations
```csharp
// 3-5x expected operation time
options.DefaultLockDuration = TimeSpan.FromSeconds(30);
```

✅ Monitor metrics
```csharp
var metrics = lockService.GetMetrics();
```

### Don't

❌ Use the same lock key for unrelated operations
```csharp
// Bad: "lock" is too generic
await lockService.AcquireAsync("lock", "owner");

// Good: specific resource
await lockService.AcquireAsync("database-migration", "owner");
```

❌ Hold locks for longer than necessary
```csharp
// Bad: 30 minute lock for 30 second operation
var @lock = await lockService.AcquireAsync(..., TimeSpan.FromMinutes(30));

// Good: reasonable duration
var @lock = await lockService.AcquireAsync(..., TimeSpan.FromSeconds(30));
```

❌ Acquire locks in unrelated order (deadlock risk)
```csharp
// Bad: inconsistent order
Process1: Acquire A, then B
Process2: Acquire B, then A

// Good: always same order
var locks = new[] { "a", "b" };
Array.Sort(locks);
foreach (var lock in locks)
    await lockService.AcquireAsync(lock, ...);
```

❌ Ignore lock acquisition failures
```csharp
// Bad: no error handling
await lockService.AcquireAsync(...);

// Good: handle failures
try
{
    await lockService.AcquireAsync(...);
}
catch (LockAcquisitionException ex)
{
    // Handle appropriately
}
```

## Support

### Where can I report issues?

GitHub Issues: https://github.com/sarmkadan/dotnet-distributed-lock/issues

### Where can I suggest features?

GitHub Discussions or Issues with feature request tag

### How do I contribute?

See CONTRIBUTING section in README.md

### Is there commercial support?

Not currently. Community support via GitHub Issues.

### Which .NET versions are supported?

.NET 10.0+ (net10.0 target framework)

### Can I use this with .NET Framework?

No, this library targets .NET 10.0 only. Use standard locking primitives or a different library for .NET Framework.
