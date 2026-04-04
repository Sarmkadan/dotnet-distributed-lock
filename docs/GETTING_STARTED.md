# Getting Started with SarmKadan.DistributedLock

This guide walks you through installing and using the distributed lock library in your .NET applications.

## Prerequisites

- .NET 10.0 SDK or later
- For Redis backend: Redis Server 6.0+
- For PostgreSQL backend: PostgreSQL 12+
- For SQLite backend: No additional requirements

## Installation Steps

### Step 1: Create a New .NET Project

```bash
dotnet new console -n DistributedLockDemo
cd DistributedLockDemo
```

### Step 2: Add the NuGet Package

```bash
dotnet add package SarmKadan.DistributedLock
```

### Step 3: Create a Simple Example

Create a file `Program.cs`:

```csharp
using SarmKadan.DistributedLock;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Configure distributed locking
services.AddDistributedLocking(options =>
{
    options.BackendType = BackendType.InMemory;
    options.DefaultLockDuration = TimeSpan.FromSeconds(30);
    options.EnableAutoRenewal = true;
});

var serviceProvider = services.BuildServiceProvider();
var lockService = serviceProvider.GetRequiredService<ILockService>();

// Acquire and use a lock
var lockKey = "my-critical-resource";
var ownerId = "worker-1";

try
{
    Console.WriteLine("Attempting to acquire lock...");
    var @lock = await lockService.AcquireAsync(lockKey, ownerId);
    
    Console.WriteLine("Lock acquired! Performing critical work...");
    await Task.Delay(2000); // Simulate work
    
    await lockService.ReleaseAsync(lockKey, ownerId);
    Console.WriteLine("Lock released.");
}
catch (LockAcquisitionException ex)
{
    Console.WriteLine($"Failed to acquire lock: {ex.Message}");
}
```

### Step 4: Run the Example

```bash
dotnet run
```

## Choosing a Backend

### Development: Use In-Memory

```csharp
services.AddDistributedLocking(options =>
{
    options.BackendType = BackendType.InMemory;
});
```

Fastest option with zero dependencies. Loses locks on restart.

### Production: Use Redis

```bash
# Start Redis server (Docker)
docker run -d -p 6379:6379 redis:latest

# In your code:
services.AddDistributedLocking(options =>
{
    options.BackendType = BackendType.Redis;
    options.ConnectionString = "localhost:6379";
});
```

Most scalable option with automatic failover support.

### Alternative: Use PostgreSQL

```csharp
services.AddDistributedLocking(options =>
{
    options.BackendType = BackendType.PostgreSQL;
    options.ConnectionString = "Host=localhost;Database=locks;Username=postgres;Password=password";
});
```

Best if your application already uses PostgreSQL.

### Lightweight: Use SQLite

```csharp
services.AddDistributedLocking(options =>
{
    options.BackendType = BackendType.SQLite;
    options.ConnectionString = "Data Source=locks.db;";
});
```

Simple file-based option for single-server deployments.

## Common Patterns

### Pattern 1: Try-Acquire (Non-Blocking)

```csharp
var @lock = await lockService.TryAcquireAsync("resource", "owner");

if (@lock != null)
{
    try
    {
        // Do work
    }
    finally
    {
        await lockService.ReleaseAsync("resource", "owner");
    }
}
else
{
    Console.WriteLine("Resource is locked by another process");
}
```

### Pattern 2: Blocking Acquire with Timeout

```csharp
try
{
    var @lock = await lockService.AcquireAsync(
        "resource",
        "owner",
        TimeSpan.FromSeconds(5) // Timeout
    );
    
    // Do work
}
catch (LockAcquisitionException)
{
    Console.WriteLine("Could not acquire lock within timeout");
}
```

### Pattern 3: Using Statement (Auto-Release)

```csharp
using (await lockService.AcquireAsync("resource", "owner"))
{
    // Work is protected
    // Lock auto-releases when leaving scope
}
```

### Pattern 4: Auto-Renewal for Long Operations

```csharp
var lockKey = "batch-processing";
var ownerId = Environment.MachineName;

var @lock = await lockService.AcquireAsync(lockKey, ownerId, TimeSpan.FromMinutes(10));

// Register for auto-renewal every 5 minutes
monitor.RegisterLock(lockKey, ownerId, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10));

try
{
    await LongRunningOperationAsync(); // Can run for up to 10 minutes
}
finally
{
    await lockService.ReleaseAsync(lockKey, ownerId);
    monitor.UnregisterLock(lockKey, ownerId);
}
```

## Configuration Deep Dive

### Lock Duration

Controls how long a lock is valid if not renewed:

```csharp
options.DefaultLockDuration = TimeSpan.FromSeconds(30);
```

- Too short: High renewal overhead
- Too long: Delayed zombie detection
- Recommended: 3-5x your operation duration

### Acquisition Timeout

How long to wait before giving up on lock acquisition:

```csharp
options.DefaultAcquisitionTimeout = TimeSpan.FromSeconds(5);
```

### Acquisition Mode

```csharp
// Non-blocking: Returns immediately if locked
options.DefaultAcquisitionMode = AcquisitionMode.NonBlocking;

// Blocking: Retries until acquired
options.DefaultAcquisitionMode = AcquisitionMode.Blocking;

// Exponential backoff: Reduces contention
options.DefaultAcquisitionMode = AcquisitionMode.ExponentialBackoff;

// Linear backoff: Steady retry rate
options.DefaultAcquisitionMode = AcquisitionMode.LinearBackoff;
```

### Enable Fencing Tokens

Prevent zombie writes after lock expiration:

```csharp
options.UseFencingTokens = true;

// Then verify tokens during operations:
var token = tokenService.IssueToken(resourceId);
if (!tokenService.ValidateToken(resourceId, token))
{
    throw new InvalidOperationException("Lock expired - token invalid");
}
```

## Testing

Use in-memory backend for unit tests:

```csharp
[TestClass]
public class LockTests
{
    private ServiceProvider _serviceProvider;
    private ILockService _lockService;

    [TestInitialize]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddDistributedLocking(options =>
        {
            options.BackendType = BackendType.InMemory;
            options.DefaultLockDuration = TimeSpan.FromSeconds(5);
        });

        _serviceProvider = services.BuildServiceProvider();
        _lockService = _serviceProvider.GetRequiredService<ILockService>();
    }

    [TestMethod]
    public async Task Acquire_Should_Return_Lock()
    {
        var @lock = await _lockService.AcquireAsync("test-key", "owner1");
        Assert.IsNotNull(@lock);
        await _lockService.ReleaseAsync("test-key", "owner1");
    }

    [TestMethod]
    public async Task Acquire_Should_Block_Second_Owner()
    {
        await _lockService.AcquireAsync("test-key", "owner1");
        
        var acquired = await _lockService.TryAcquireAsync("test-key", "owner2");
        Assert.IsNull(acquired);
    }
}
```

## Troubleshooting

### Connection String Issues

**Redis:**
```csharp
// Simple format
"localhost:6379"

// With password
"localhost:6379,password=mypassword"

// Sentinel
"sentinel1:26379,sentinel2:26379,serviceName=mymaster"
```

**PostgreSQL:**
```csharp
// Basic
"Host=localhost;Database=locks;Username=postgres;Password=secret"

// With SSL
"Host=localhost;Database=locks;Username=postgres;Password=secret;SSL Mode=Require"

// With connection pooling
"Host=localhost;Database=locks;Username=postgres;Password=secret;Max Pool Size=100"
```

**SQLite:**
```csharp
// File-based
"Data Source=locks.db;"

// Shared cache (recommended for multi-threaded)
"Data Source=locks.db;Cache=Shared;"

// In-memory (single connection only)
"Data Source=:memory:;"
```

### Debugging

Enable debug logging:

```csharp
services.AddLogging(builder =>
{
    builder.SetMinimumLevel(LogLevel.Debug);
    builder.AddConsole();
});
```

Watch for these log messages:
- `Lock acquisition started for key: {key}`
- `Lock acquired for key: {key}`
- `Lock release started for key: {key}`
- `Lock renewal started for key: {key}`

### Performance Issues

1. Check backend responsiveness
2. Monitor active lock count: `await lockService.GetAllActiveLockAsync()`
3. Review metrics: `lockService.GetMetrics()`
4. Adjust lock durations and renewal intervals
5. Consider scaling backend infrastructure

## Next Steps

- Review [API Reference](./API_REFERENCE.md) for complete API documentation
- Check [Architecture](./ARCHITECTURE.md) to understand the design
- Explore [Deployment](./DEPLOYMENT.md) for production setup
- Read [FAQ](./FAQ.md) for common questions
