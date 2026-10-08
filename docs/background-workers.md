# Background Workers

The library ships four `BackgroundService` implementations under `src/Workers/`. Each one runs a loop inside the host, can be registered through the DI container, and exposes a small public API for inspection or manual control.

| Worker | Purpose | Source |
|---|---|---|
| `LockCleanupWorker` | Removes expired locks from the backend | `src/Workers/LockCleanupWorker.cs` |
| `LockRenewalWorker` | Extends locks registered for auto-renewal before they expire | `src/Workers/LockRenewalWorker.cs` |
| `HealthMonitoringWorker` | Periodically checks backend connectivity and tracks health state | `src/Workers/HealthMonitoringWorker.cs` |
| `MetricsCollectionWorker` | Takes periodic cache statistics snapshots | `src/Workers/MetricsCollectionWorker.cs` |

Common behavior:

- All four derive from `Microsoft.Extensions.Hosting.BackgroundService` and run in `ExecuteAsync` until the host's stopping token is cancelled.
- Each has a constructor that takes its dependencies plus an optional options object. If the options are `null`, defaults are used.
- Each overrides `StopAsync` only to log the stop and then calls `base.StopAsync`.
- Each overrides `ToString()` to produce a diagnostic string with its current configuration (and, where relevant, its current state).
- A loop iteration that throws (other than cancellation) is logged and the loop continues. A single failure does not stop the worker.

---

## LockCleanupWorker

Deletes expired locks from the repository (`ILockRepository`) so that database or Redis storage does not grow without bound.

### Loop

1. Wait `InitialDelayMs` once before the first sweep.
2. Run `PerformCleanupAsync`.
3. Wait `CleanupIntervalMs`, whether the sweep succeeded or failed. This prevents a tight retry loop when the backend is down.
4. Repeat until cancelled.

Error handling inside the loop:

- `OperationCanceledException` ends the loop.
- `LockExpiredException` is logged at Debug. It means a lock expired again between being read and being deleted, which is a benign race.
- Any other exception is logged at Error and the loop continues.

### Sweep algorithm

`PerformCleanupAsync` in `LockCleanupWorker.cs`:

1. Calls `GetExpiredLocksAsync` to snapshot expired locks.
2. Computes `cutoff = UtcNow - MinimumExpiredDuration`.
3. For each lock:
   - If `ExpiresAt > cutoff`, the lock is skipped. It expired too recently.
   - If the number of deletion attempts has reached `BatchSize`, the lock is deferred to the next sweep.
   - Otherwise it calls `DeleteLockIfExpirationMatchesAsync(key, expiresAt)`. This is a compare-and-delete: the delete only happens if the stored expiration still matches what was observed. If a holder renewed the lock in the meantime, the delete is refused and the lock is counted as skipped.
4. Adds the number of successful deletions to the cumulative `_cleanedCount`.
5. Logs at Information when something was cleaned or skipped, or when `VerboseLogging` is on. Logs deferred counts at Debug.

### Public API

- `RunCleanupOnceAsync(CancellationToken)` runs one sweep on demand, outside the schedule.
- `LockCleanupWorkerExtensions` (`LockCleanupWorkerExtensions.cs`):
  - `RunCleanupOnceAsyncWithTimeout(TimeSpan, CancellationToken)` wraps the sweep with a timeout. On timeout it throws `TimeoutException`.
  - `RunCleanupOnceAsyncWithStats(CancellationToken)` returns `(Duration, CleanedCount)` for one sweep.
  - `GetCleanedCount()` returns the cumulative count.
  - `WithTestInterval(TimeSpan)` changes `CleanupIntervalMs` on the live options object.
  - `LogConfiguration(ILogger)` writes the current options to the log.
- `LockCleanupWorkerValidation` (`LockCleanupWorkerValidation.cs`): `Validate()`, `IsValid()`, and `EnsureValid()`. See the validation rules below.

### Options (`LockCleanupWorkerOptions`)

| Property | Default | Meaning |
|---|---|---|
| `InitialDelayMs` | 30 000 | Delay before the first sweep |
| `CleanupIntervalMs` | 3 600 000 (1 hour) | Delay between sweeps |
| `BatchSize` | 1000 | Maximum deletion attempts per sweep |
| `VerboseLogging` | `false` | Log every sweep, even when nothing changed |
| `MinimumExpiredDuration` | 5 minutes | Only delete locks that expired at least this long ago |

Validation rules (`LockCleanupWorkerValidation.Validate`):

- `InitialDelayMs` must be non-negative.
- `CleanupIntervalMs` must be positive.
- `BatchSize` must be positive and no more than 100 000.
- `MinimumExpiredDuration` must be positive and no more than 1 day.

Note: the validation helpers read the private `_options` field through reflection, and so do `GetCleanedCount`, `WithTestInterval`, and `LogConfiguration`. If the field is renamed, these helpers break at runtime without a compile error.

---

## LockRenewalWorker

Keeps registered locks alive by calling `ILockService.RenewLockAsync` shortly before they expire. Intended for long-running work that holds a lock longer than its initial lease.

### Registration

- `RegisterForRenewal(lockId, fencingToken, renewalInterval)` adds or replaces a `RenewalSchedule`. The first renewal is due at `UtcNow + renewalInterval`.
- `UnregisterFromRenewal(lockId)` removes it.
- `LockRenewalWorkerExtensions` (`LockRenewalWorkerExtensions.cs`):
  - `TryRegisterForRenewal` returns `false` instead of throwing on an `ArgumentException`.
  - `SafeUnregisterFromRenewal` swallows exceptions.
  - `GetTimeUntilNextRenewal` forwards to the instance method.
  - `TryUpdateRenewalInterval` is documented to change the interval of a registered lock. See Known issues.
- Query methods: `IsRegisteredForRenewal`, `TryGetRenewalSchedule`, `GetTimeUntilNextRenewal`.

### Loop

1. Call `ProcessRenewalsAsync`. It selects every schedule where `NextRenewalTime <= UtcNow` and renews them in parallel with `Task.WhenAll`.
2. Wait `CheckIntervalMs`.
3. Repeat until cancelled.

Each renewal (`RenewLockAsync`):

- Calls `RenewLockAsync(lockId, fencingToken, RenewalInterval)`.
- On success, sets `NextRenewalTime = updatedLock.ExpiresAt - (RenewalInterval + random jitter)`. Jitter is up to `JitterPercentage` percent of the interval, which spreads out renewals.
- On failure, logs at Error and throws `LockRenewalFailedException` with the lock ID.

### Options (`LockRenewalWorkerOptions`)

| Property | Default | Meaning |
|---|---|---|
| `CheckIntervalMs` | 5000 | How often the worker scans for due renewals |
| `RetryDelaySeconds` | 10 | Documented as the retry delay for failed renewals. See Known issues. |
| `JitterPercentage` | 10 | Maximum jitter applied to the renewal time, in percent |

### Failure behavior

- If one renewal fails, `Task.WhenAll` rethrows its exception. The other renewals in that batch still run to completion, but the error propagates to `ExecuteAsync`. There it is logged and the loop continues on the next scan.
- The failed schedule's `NextRenewalTime` is not changed, so it stays due and is retried on the next scan (every `CheckIntervalMs`). There is no backoff.

### Thread safety

Schedules are kept in a `ConcurrentDictionary<string, RenewalSchedule>`. `RenewalSchedule.NextRenewalTime` is a plain property that is written by the renewal task and read by the scan, so the scan may observe a slightly stale value. This is acceptable for scheduling but is not a synchronization guarantee.

---

## HealthMonitoringWorker

Verifies that the repository backend answers requests and keeps the result in an in-memory `HealthStatus`.

### Loop

1. Run `CheckHealthAsync`.
2. Wait `CheckIntervalMs`.
3. Repeat until cancelled.

Each check:

- Creates a linked `CancellationTokenSource` with `CheckTimeout` applied.
- Calls `TestBackendConnectivityAsync`, which calls `ILockRepository.GetByKeyAsync` on a synthetic key (`__health_check_<ticks>`). The lock is expected not to exist. The call is considered successful if it returns without throwing, whatever the result.
- **On success:** sets `IsHealthy = true`, `BackendConnected = true`, updates `LastCheckTime` and `CheckDurationMs`, resets `ConsecutiveFailures` to 0, and clears `LastErrorMessage`.
- **On failure:** sets `IsHealthy = false`, `BackendConnected = false`, records the error message, and logs at Error. If `AlertOnUnhealthy` is on, it calls `AlertUnhealthyStatusAsync`.
- If the host is stopping, the `OperationCanceledException` is rethrown and the loop exits.

`AlertUnhealthyStatusAsync` increments `ConsecutiveFailures`. Once the count reaches `FailureThreshold`, it logs at Critical. Nothing else is sent. There is no webhook, email, or metric emission.

### Public API

- `GetStatus()` returns a copy of the current `HealthStatus`. This is the supported way to read state.
- `ResetFailureCounter()` sets `ConsecutiveFailures` to 0.

### Types

- `HealthStatus`: `IsHealthy`, `BackendConnected`, `LastCheckTime`, `CheckDurationMs`, `ConsecutiveFailures`, `LastErrorMessage`.

### Options (`HealthMonitoringWorkerOptions`)

| Property | Default | Meaning |
|---|---|---|
| `CheckIntervalMs` | 30 000 | Delay between checks |
| `FailureThreshold` | 3 | Consecutive failures before the Critical log |
| `AlertOnUnhealthy` | `true` | Whether failures are counted and escalated |
| `CheckTimeout` | 10 s | Upper bound for one backend call |

Note: `ToString()` reads the internal state object directly and is not synchronized with the check loop. Use `GetStatus()` when you need a consistent view.

---

## MetricsCollectionWorker

Records a `MetricsSnapshot` of cache statistics at a fixed interval and keeps recent snapshots in memory for queries.

### Loop

1. Wait `InitialDelayMs` once.
2. Run `CollectMetricsAsync`. It reads `ILockCacheManager.GetStatistics()`, wraps the result in a snapshot with the current UTC time, adds it to an in-memory list, and removes snapshots older than `SnapshotRetentionSeconds`.
3. Wait `CollectionIntervalMs`.
4. Repeat until cancelled.

Failures inside `CollectMetricsAsync` are logged at Warning and do not affect the loop.

### Public API

- `GetCurrentSnapshot()` returns the newest snapshot, or `null` if none has been taken.
- `GetSnapshots(TimeSpan window)` returns all snapshots newer than `now - window`.
- `GetAverageMetrics(TimeSpan window)` returns a synthetic snapshot in that window. `CachedItems` and `HitRate` are averaged. `Hits` and `Misses` are summed. Returns `null` if the window is empty.

### Types

- `MetricsSnapshot`: `Timestamp`, `CacheStatistics` (`CacheStatistics` from `Caching`), `CustomMetrics` (`Dictionary<string, object>`).

### Options (`MetricsCollectionWorkerOptions`)

| Property | Default | Meaning |
|---|---|---|
| `InitialDelayMs` | 5000 | Delay before the first collection |
| `CollectionIntervalMs` | 60 000 | Delay between collections |
| `SnapshotRetentionSeconds` | 3600 | How long snapshots are kept |
| `VerboseLogging` | `false` | Declared, but no code path reads it. See Known issues. |

Note: snapshots live only in process memory. They are lost on restart and are not shared between instances.

---

## Known issues

These were found while writing this document. The behavior described is the current behavior. None of them were changed here.

1. **`LockRenewalWorkerExtensions.TryUpdateRenewalInterval` never succeeds.** It casts the private dictionary to `ConcurrentDictionary<string, dynamic>`, which does not match the real type `ConcurrentDictionary<string, RenewalSchedule>`. The `is` check fails and the method always returns `false`. The method also assigns to `RenewalInterval`, which is declared `init`-only on `RenewalSchedule`.
2. **`LockRenewalWorkerOptions.RetryDelaySeconds` is unused in the loop.** A failed renewal is retried on the next `CheckIntervalMs` scan, with no delay. The value only appears in `ToString()`.
3. **`HealthMonitoringWorkerOptions.AlertOnUnhealthy` does not alert anywhere.** It only increments a counter and writes a Critical log entry.
4. **`MetricsCollectionWorkerOptions.VerboseLogging` and `MetricsSnapshot.CustomMetrics` are not used** by any code path in this worker.
5. **Reflection-based helpers** (`LockCleanupWorkerExtensions`, `LockCleanupWorkerValidation`) depend on the private field names `_options` and `_cleanedCount`.

## Registration

The workers are plain `BackgroundService` classes. Register them with the standard hosted-service call, for example:

```csharp
services.AddSingleton<LockCleanupWorkerOptions>(new LockCleanupWorkerOptions { BatchSize = 500 });
services.AddHostedService<LockCleanupWorker>();
```

Dependencies by worker:

- `LockCleanupWorker`: `ILockRepository`, `ILogger<LockCleanupWorker>`, optional `LockCleanupWorkerOptions`.
- `LockRenewalWorker`: `ILockService`, `ILogger<LockRenewalWorker>`, optional `LockRenewalWorkerOptions`.
- `HealthMonitoringWorker`: `ILockRepository`, `ILogger<HealthMonitoringWorker>`, optional `HealthMonitoringWorkerOptions`.
- `MetricsCollectionWorker`: `ILockCacheManager`, `ILogger<MetricsCollectionWorker>`, optional `MetricsCollectionWorkerOptions`.

The options parameter is optional in every constructor. When it is not registered in the container, the defaults above apply.
