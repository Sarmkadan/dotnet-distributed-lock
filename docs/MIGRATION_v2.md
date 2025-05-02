# Migration Guide: v1.x to v2.0

This guide covers all changes required to upgrade from SarmKadan.DistributedLock v1.x to v2.0.

## Breaking Changes

### Target Framework

v2.0 targets **.NET 10.0** exclusively. If you are on .NET 8 or 9, upgrade your project first:

```xml
<TargetFramework>net10.0</TargetFramework>
```

### Semaphore API

v2.0 introduces `IDistributedSemaphore` alongside `ILockService`. The semaphore uses the same backend infrastructure but allows multiple concurrent holders.

If you previously worked around this limitation by acquiring multiple named locks, replace with:

```csharp
// v1.x workaround
await lockService.AcquireAsync("resource-slot-1", ownerId);
await lockService.AcquireAsync("resource-slot-2", ownerId);

// v2.0
await semaphoreService.AcquireAsync("resource", ownerId, maxConcurrent: 3);
```

### Fair Queuing

Lock acquisition now uses fair queuing by default. Requests are served in FIFO order rather than competing freely. This changes timing behaviour under contention.

To revert to the previous behaviour:

```csharp
options.UseFairQueuing = false;
```

### Priority Escalation

`LockRequestContext` gained a `Priority` property. Requests with higher priority can preempt waiting requests in the queue. Existing code that does not set priority will default to `Priority.Normal` and behave the same as v1.x.

```csharp
var context = new LockRequestContext
{
    Priority = LockPriority.High,
    CorrelationId = correlationId
};
await lockService.AcquireAsync(key, owner, duration, context, ct);
```

## New Features

### Distributed Semaphore

Control concurrent access to a resource with a configurable limit:

```csharp
services.AddDistributedLocking(options =>
{
    options.BackendType = BackendType.Redis;
    options.ConnectionString = "localhost:6379";
    options.EnableSemaphore = true;
    options.DefaultMaxConcurrent = 5;
});

var semaphore = provider.GetRequiredService<IDistributedSemaphore>();
await semaphore.AcquireAsync("rate-limited-api", workerId, maxConcurrent: 10);
```

### Fair Queuing

Prevents starvation under high contention. Lock requests are queued and granted in arrival order:

```csharp
options.UseFairQueuing = true;  // default in v2.0
options.QueueTimeout = TimeSpan.FromSeconds(30);
```

### Priority Escalation

Assign priorities to lock requests. Higher-priority requests move ahead in the fair queue:

```csharp
options.EnablePriorityEscalation = true;
options.EscalationThreshold = TimeSpan.FromSeconds(10); // auto-escalate after waiting 10s
```

### Docker Support

v2.0 ships with a production-ready Dockerfile and docker-compose.yml:

```bash
docker compose up -d
```

The container exposes port 8080 with built-in health check at `/health`.

### Health Endpoints

Two endpoints are available out of the box:

| Endpoint | Purpose |
|----------|---------|
| `/health` | Liveness - returns 200 if the process is running |
| `/health/ready` | Readiness - checks backend connectivity |

## Configuration Changes

### New Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `EnableSemaphore` | `bool` | `false` | Enable distributed semaphore support |
| `DefaultMaxConcurrent` | `int` | `1` | Default max concurrent holders for semaphore |
| `UseFairQueuing` | `bool` | `true` | FIFO ordering for lock acquisition |
| `QueueTimeout` | `TimeSpan` | `30s` | Max time a request waits in the queue |
| `EnablePriorityEscalation` | `bool` | `false` | Allow priority-based queue ordering |
| `EscalationThreshold` | `TimeSpan` | `10s` | Auto-escalate priority after this wait time |

### Removed Options

None. All v1.x options remain supported.

### Renamed Options

None. No options were renamed.

## Step-by-Step Migration

1. **Update the package reference:**

   ```xml
   <PackageReference Include="SarmKadan.DistributedLock" Version="2.0.0" />
   ```

2. **Update target framework** to `net10.0` if not already done.

3. **Review acquisition behaviour.** Fair queuing is now the default. Under low contention there is no visible difference. Under high contention, lock acquisition order becomes deterministic (FIFO).

4. **Replace multi-lock semaphore workarounds** with the new `IDistributedSemaphore` API.

5. **Add health check integration** if running in Docker or Kubernetes:

   ```csharp
   app.MapHealthChecks("/health");
   app.MapHealthChecks("/health/ready", new HealthCheckOptions
   {
       Predicate = check => check.Tags.Contains("ready")
   });
   ```

6. **Run the test suite** to confirm no regressions:

   ```bash
   dotnet test
   ```

## Compatibility Matrix

| v1.x Feature | v2.0 Status | Notes |
|--------------|-------------|-------|
| Redis backend | Supported | No changes |
| PostgreSQL backend | Supported | No changes |
| SQLite backend | Supported | No changes |
| InMemory backend | Supported | No changes |
| Fencing tokens | Supported | No changes |
| Auto-renewal | Supported | No changes |
| Event system | Supported | No changes |
| Caching layer | Supported | No changes |
| Webhook integration | Supported | No changes |
| `AcquireAsync` | Supported | Added optional `LockRequestContext` overload |
| `TryAcquireAsync` | Supported | Added optional `LockRequestContext` overload |
| `GetMetrics()` | Supported | Extended with semaphore and queue metrics |

## Rollback

If you need to roll back to v1.x:

1. Remove any `IDistributedSemaphore` usage.
2. Remove `LockRequestContext.Priority` assignments.
3. Set `UseFairQueuing = false` before downgrading.
4. Revert the package reference to `1.0.0`.

No database schema changes are required for rollback.
