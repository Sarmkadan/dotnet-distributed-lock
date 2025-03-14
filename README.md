# SarmKadan.DistributedLock

A high-performance, production-ready distributed locking library for .NET with support for multiple backends including Redis, SQLite, and PostgreSQL. Features include fencing tokens, automatic lock renewal, configurable acquisition strategies, and comprehensive metrics.

## Features

- **Multiple Backend Support**: Redis, SQLite, PostgreSQL, and in-memory backends
- **Fencing Tokens**: Prevent zombie processes from writing to shared resources
- **Auto-Renewal**: Automatic lock renewal with configurable intervals
- **Flexible Acquisition Strategies**: Non-blocking, blocking, exponential backoff, and linear backoff modes
- **Lock Monitoring**: Background monitoring with automatic renewal capabilities
- **Metrics**: Built-in metrics tracking for acquisition success rates, hold times, and performance
- **Async/Await Support**: Fully asynchronous API with CancellationToken support
- **Thread-Safe**: Concurrent-safe implementation across all backends
- **Logging**: Comprehensive logging via Microsoft.Extensions.Logging
- **Type-Safe**: Written in modern C# with nullable reference types enabled

## Quick Start

### Installation

Add the NuGet package to your project:

```bash
dotnet add package SarmKadan.DistributedLock
```

### Basic Usage

```csharp
using SarmKadan.DistributedLock;
using Microsoft.Extensions.DependencyInjection;

// Configure services
var services = new ServiceCollection();

services.AddDistributedLocking(options =>
{
    options.BackendType = BackendType.InMemory; // or Redis, SQLite, PostgreSQL
    options.DefaultLockDuration = TimeSpan.FromSeconds(30);
    options.EnableAutoRenewal = true;
});

var serviceProvider = services.BuildServiceProvider();
var lockService = serviceProvider.GetRequiredService<ILockService>();

// Acquire a lock
try
{
    var @lock = await lockService.AcquireAsync("my-resource", "process-1");
    
    // Do critical work...
    
    // Release the lock
    await lockService.ReleaseAsync("my-resource", "process-1");
}
catch (LockAcquisitionException ex)
{
    Console.WriteLine($"Failed to acquire lock: {ex.Message}");
}
```

## Backend Configuration

### In-Memory (Development/Testing)

```csharp
services.AddDistributedLocking(options =>
{
    options.BackendType = BackendType.InMemory;
});
```

### Redis

```csharp
services.AddDistributedLocking(options =>
{
    options.BackendType = BackendType.Redis;
    options.ConnectionString = "localhost:6379";
});
```

### SQLite

```csharp
services.AddDistributedLocking(options =>
{
    options.BackendType = BackendType.SQLite;
    options.ConnectionString = "Data Source=locks.db;";
});
```

### PostgreSQL

```csharp
services.AddDistributedLocking(options =>
{
    options.BackendType = BackendType.PostgreSQL;
    options.ConnectionString = "Host=localhost;Database=locks;Username=postgres;Password=password";
});
```

## API Reference

### ILockService

Core service for lock operations:

- `AcquireAsync(lockKey, ownerId, duration?, cancellationToken?)` - Acquire a lock with retries
- `TryAcquireAsync(lockKey, ownerId, duration?, cancellationToken?)` - Non-blocking lock acquisition
- `RenewAsync(lockKey, ownerId, newDuration?, cancellationToken?)` - Extend lock expiration
- `ReleaseAsync(lockKey, ownerId, cancellationToken?)` - Release a lock
- `GetLockAsync(lockKey, cancellationToken?)` - Retrieve lock information
- `IsLockedAsync(lockKey, cancellationToken?)` - Check if lock is held
- `GetAllActiveLockAsync(cancellationToken?)` - Get all active locks

### LockMonitor

Automatic lock renewal service:

```csharp
var monitor = serviceProvider.GetRequiredService<LockMonitor>();

// Register locks for monitoring
monitor.RegisterLock("my-resource", "process-1", renewalInterval, lockDuration);

// Start monitoring
monitor.StartMonitoring(TimeSpan.FromSeconds(1));

// Stop monitoring
await monitor.StopMonitoringAsync();
```

### FencingTokenService

Prevents zombie processes from writing to shared resources:

```csharp
var tokenService = serviceProvider.GetRequiredService<FencingTokenService>();

// Issue a new token
var token = tokenService.IssueToken("my-resource");

// Validate a token
if (tokenService.ValidateToken("my-resource", providedToken))
{
    // Safe to proceed
}
```

## Configuration Options

```csharp
services.AddDistributedLocking(options =>
{
    // Backend selection
    options.BackendType = BackendType.Redis;
    options.ConnectionString = "localhost:6379";
    
    // Lock timing
    options.DefaultLockDuration = TimeSpan.FromSeconds(30);
    options.DefaultAcquisitionTimeout = TimeSpan.FromSeconds(5);
    options.DefaultRenewalInterval = TimeSpan.FromSeconds(10);
    
    // Retry strategy
    options.DefaultMaxRetries = 3;
    options.DefaultRetryDelayMs = 100;
    options.DefaultAcquisitionMode = AcquisitionMode.Blocking;
    
    // Features
    options.EnableAutoRenewal = true;
    options.UseFencingTokens = true;
    options.EnableMetrics = true;
    options.EnableLogging = true;
    
    // Capacity
    options.MaxConcurrentLocks = 1000;
});
```

## Exception Handling

The library provides specific exception types:

- `LockAcquisitionException` - Failed to acquire lock within timeout
- `LockExpiredException` - Operation on expired lock
- `LockNotOwnedException` - Attempting to release/renew lock not owned by caller
- `InvalidFencingTokenException` - Fencing token validation failed
- `DistributedLockException` - Base exception for all lock-related errors

## Performance Considerations

- **Redis**: Best for high-throughput scenarios with cluster support
- **PostgreSQL**: Best for SQL-integrated applications with strong consistency
- **SQLite**: Best for lightweight, embedded scenarios
- **In-Memory**: Development and testing only

## Metrics

Access performance metrics via the LockService:

```csharp
var lockService = serviceProvider.GetRequiredService<ILockService>();
if (lockService is LockService concrete)
{
    var metrics = concrete.GetMetrics();
    Console.WriteLine($"Active Locks: {metrics.CurrentActiveLocks}");
    Console.WriteLine($"Acquisition Success Rate: {metrics.AcquisitionSuccessRate:F2}%");
    Console.WriteLine($"Average Acquisition Time: {metrics.AverageAcquisitionTimeMs:F2}ms");
}
```

## Thread Safety

All repository implementations are fully thread-safe:
- In-memory uses `ReaderWriterLockSlim` for concurrent access
- Redis uses atomic operations
- SQLite and PostgreSQL use database-level locking

## License

MIT License - Copyright © 2026 Vladyslav Zaiets

See LICENSE file for details.

## Support

For issues, bug reports, or feature requests, please open an issue on GitHub.

---

**Author**: Vladyslav Zaiets  
**Website**: https://sarmkadan.com
