# ContentionMetrics

Provides runtime statistics for a specific distributed lock key, tracking waiter activity and deadlock occurrences to help monitor contention and diagnose performance issues.

## API

### LockKey  
**Type:** `string`  
**Purpose:** Identifies the lock whose contention is being measured.  
**Remarks:** Set once when the instance is created; changing the value after creation is not supported and may lead to inconsistent metrics.

### CreatedAt  
**Type:** `DateTime`  
**Purpose:** Timestamp indicating when the `ContentionMetrics` instance was instantiated.  
**Remarks:** Reflects the system clock at construction time; does not change during the object's lifetime.

### LastUpdatedAt  
**Type:** `DateTime`  
**Purpose:** Timestamp of the most recent modification to any metric (waiter added/removed or deadlock recorded).  
**Remarks:** Updated automatically by the recording methods; useful for determining staleness of the data.

### ContentionMetrics  
**Type:** `ContentionMetrics`  
**Purpose:** Gets or sets a nested `ContentionMetrics` instance, enabling hierarchical tracking (e.g., per‑resource sub‑metrics).  
**Remarks:** If set to `null`, no sub‑metrics are maintained. Assigning a new instance replaces any existing nested metrics.

### RecordWaiterAdded()  
**Parameters:** None  
**Return:** `void`  
**Purpose:** Increments the internal waiter count for the lock key.  
**Exceptions:** None; the method is designed to be callable from any thread without throwing.

### RecordWaiterRemoved()  
**Parameters:** None  
**Return:** `void`  
**Purpose:** Decrements the internal waiter count for the lock key.  
**Exceptions:** None; calling this method when the waiter count is zero will leave the count at zero (no underflow).

### RecordDeadlock()  
**Parameters:** None  
**Return:** `void`  
**Purpose:** Records that a deadlock was detected for the lock key, incrementing an internal deadlock counter.  
**Exceptions:** None; safe to invoke concurrently.

### ToString()  
**Return:** `string`  
**Purpose:** Returns a human‑readable summary containing the lock key, creation time, last update time, current waiter count, and deadlock count.  
**Exceptions:** None; the method never throws and is safe to call from any thread.

## Usage

```csharp
// Creating a metrics instance for a lock named "resource-123"
var metrics = new ContentionMetrics
{
    LockKey = "resource-123"
};

// Simulating a waiter acquiring interest in the lock
metrics.RecordWaiterAdded();
// ... later, when the waiter gives up or acquires the lock
metrics.RecordWaiterRemoved();

// If a deadlock is detected, record it
metrics.RecordDeadlock();

Console.WriteLine(metrics.ToString());
// Example output:
// LockKey: resource-123, CreatedAt: 2025-11-02T10:15:30Z, LastUpdatedAt: 2025-11-02T10:16:05Z,
// Waiters: 0, Deadlocks: 1
```

```csharp
// Hierarchical metrics: track per‑sub‑resource contention under a parent lock
var parentMetrics = new ContentionMetrics { LockKey = "parent-lock" };
var childMetrics  = new ContentionMetrics { LockKey = "child-resource" };

// Attach child metrics to the parent
parentMetrics.ContentionMetrics = childMetrics;

// Record activity on the child
childMetrics.RecordWaiterAdded();
childMetrics.RecordWaiterRemoved();

// The parent's ToString will reflect its own state; child metrics can be inspected separately
Console.WriteLine(parentMetrics.ToString());
Console.WriteLine(childMetrics.ToString());
```

## Notes

- The type does not employ any internal locking; all members are safe for concurrent access because they rely on atomic reads/writes of primitive fields or use `Interlocked` operations internally (not exposed in the public API).  
- `LockKey` should be treated as immutable after construction; modifying it while metrics are being recorded may cause the instance to report data for the wrong lock.  
- The `ContentionMetrics` property allows nesting but does not automatically aggregate child metrics into the parent; consumers must manually combine values if a combined view is required.  
- `RecordWaiterRemoved` guards against negative counts internally; excessive calls beyond the number of added waiters have no effect beyond keeping the count at zero.  
- `ToString` allocates a new string on each call; for high‑frequency logging consider caching the result or using a custom formatting approach.  
- Instances are not pooled; each lock key should have its own `ContentionMetrics` object to avoid cross‑talk of metrics.
