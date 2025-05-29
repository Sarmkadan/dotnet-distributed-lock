# LockMonitor

The `LockMonitor` type manages the lifecycle of distributed locks by registering lock identifiers, periodically renewing them, and providing a way to start and stop the renewal process. It is intended to be used by components that acquire a lock through an external locking mechanism and need to ensure the lock remains valid for the duration of their work.

## API

### LockMonitor  
Represents the monitor instance. Construct the object with no parameters; after construction configure the lock‑specific properties (`LockKey`, `OwnerId`, `RenewalInterval`, `LockDuration`) before calling `StartMonitoring`.

### RegisterLock  
Registers a lock so that the monitor will attempt to renew it periodically.  
**Parameters** – lock key, owner identifier, renewal interval, and lock duration (the exact parameter types correspond to the strings and `TimeSpan` values shown in the properties).  
**Return** – `void`.  
**Throws** –  
- `ArgumentNullException` if the lock key or owner identifier is `null`.  
- `ArgumentOutOfRangeException` if the renewal interval or lock duration is zero or negative.  
- `ObjectDisposedException` if the monitor has already been disposed.  
- `InvalidOperationException` if a lock with the same key is already registered.

### UnregisterLock  
Removes a lock from the monitor’s renewal list.  
**Parameters** – the lock key to unregister.  
**Return** – `void`.  
**Throws** –  
- `ArgumentNullException` if the lock key is `null`.  
- `ObjectDisposedException` if the monitor has been disposed.  
- `InvalidOperationException` if the specified lock is not currently registered.

### StartMonitoring  
Begins the periodic renewal process for all currently registered locks.  
**Return** – `void`.  
**Throws** –  
- `ObjectDisposedException` if the monitor has been disposed.  
- `InvalidOperationException` if monitoring is already started.

### StopMonitoringAsync  
Stops the renewal process asynchronously, allowing any in‑flight renewal attempts to complete.  
**Return** – a `Task` that completes when monitoring has stopped.  
**Throws** –  
- `ObjectDisposedException` if the monitor has been disposed.

### GetMonitoredLocks  
Retrieves the set of lock keys that are currently being monitored.  
**Return** – `IEnumerable<string>` containing the lock keys.  
**Throws** – `ObjectDisposedException` if the monitor has been disposed.

### Dispose  
Releases any internal timers or resources used by the monitor. The method may be called multiple times without effect.  
**.**Throws** – none.

### LockKey  
Gets the key of the lock associated with this monitor instance (if the monitor is configured for a single lock; otherwise returns the key of the most recently registered lock).  
**Return** – `string`.  
**Throws** – none.

### OwnerId  
Gets the identifier of the lock owner associated with this monitor instance.  
**Return** – `string`.  
**Throws** – none.

### RenewalInterval  
Gets the interval at which the monitor attempts to renew the lock.  
**Return** – `TimeSpan`.  
**Throws** – none.

### LockDuration  
Gets the duration for which a lock is considered valid after a successful renewal.  
**Return** – `TimeSpan`.  
**Throws** – none.

### LastRenewalAttempt  
Gets the UTC timestamp of the most recent renewal attempt, whether successful or not.  
**Return** – `DateTime`.  
**Throws** – none.

### RenewalCount  
Gets the total number of renewal attempts that have been made since the monitor started.  
**Return** – `int`.  
**Throws** – none.

## Usage

```csharp
using var monitor = new LockMonitor
{
    LockKey        = "resource-123",
    OwnerId        = Environment.MachineName,
    RenewalInterval = TimeSpan.FromSeconds(15),
    LockDuration   = TimeSpan.FromSeconds(30)
};

monitor.RegisterLock(monitor.LockKey, monitor.OwnerId,
                     monitor.RenewalInterval, monitor.LockDuration);
monitor.StartMonitoring();

// Perform work while the lock is held …
// ...

await monitor.StopMonitoringAsync();
// monitor.Dispose();
```

```csharp
var monitor = new LockMonitor();
// Configure multiple locks
monitor.RegisterLock("lock-a", TimeSpan.Zero);
```

```csharp
var monitor = new LockMonitor();
// Configure properties for a lock that will be registered later
monitor.LockKey    = "config-lock";
monitor.OwnerId    = "worker-7";
monitor.RenewalInterval = TimeSpan.FromMinutes(1);
monitor.LockDuration    = TimeSpan.FromMinutes(2);

monitor.RegisterLock(monitor.LockKey, monitor.OwnerId,
                     monitor.RenewalInterval, monitor.LockDuration);
monitor.StartMonitoring();

// Later, when the lock is no longer needed:
monitor.UnregisterLock(monitor.LockKey);
await monitor.StopMonitoringAsync();
monitor.Dispose();
```

## Notes

- The monitor is thread‑safe for concurrent calls to `RegisterLock`, `UnregisterLock`, and `GetMonitoredLocks`.  
- `StartMonitoring` and `StopMonitoringAsync` should not be invoked concurrently from multiple threads without external synchronization; doing so may result in `InvalidOperationException`.  
- Once `Dispose` has been called, all subsequent member invocations (except for repeated calls to `Dispose`) will throw `ObjectDisposedException`.  
- Property values (`LockKey`, `OwnerId`, `RenewalInterval`, `LockDuration`) are snapshots taken at the time the monitor is configured; changing them after `StartMonitoring` has been called does not affect already‑registered locks.  
- `LastRenewalAttempt` and `RenewalCount` are updated only when a renewal attempt is actually made; they are not altered by manual registration or unregistration of locks.  
- If the underlying locking system fails to renew a lock, the monitor does not automatically re‑acquire the lock; it merely records the failed attempt and continues with the next interval. Consumers should handle lock loss according to their own retry or cancellation logic.
