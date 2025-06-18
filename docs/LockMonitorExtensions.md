# LockMonitorExtensions

Provides extension methods for monitoring and querying the state of distributed locks managed by the `dotnet-distributed-lock` library. These methods allow applications to inspect the current lock state, wait for lock releases, and determine whether locks are actively being held by other processes.

## API

### `IsLockMonitored`

Determines whether the specified lock is currently being monitored by the lock management system.
