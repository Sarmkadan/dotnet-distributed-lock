# LockRequestContext

`LockRequestContext` represents the full lifecycle state of a single distributed lock acquisition attempt. It captures the request parameters, tracks progress through retries, records the final outcome, and provides an extensible property bag for custom metadata. Instances are created by the distributed lock infrastructure and surfaced to callers for inspection, logging, or correlation purposes.

## API

### Constructors

```csharp
public LockRequestContext(string lockKey, string requesterId, AcquisitionMode mode, TimeSpan requestedDuration)
```
Initializes a new context with a unique `RequestId` (generated internally), the current UTC timestamp in `RequestedAt`, and `RetryCount` set to zero. All other outcome-related fields remain `null` or `false` until `MarkCompleted` is called.

```csharp
public LockRequestContext(string lockKey, string requesterId, AcquisitionMode mode, TimeSpan requestedDuration, string? requestorName, string? correlationId, string? userId, string? sessionId)
```
Extended constructor that additionally populates `RequestorName`, `CorrelationId`, `UserId`, and `SessionId` at creation time. All other behaviour is identical to the primary constructor.

### Properties

| Member | Type | Description |
|---|---|---|
| `RequestId` | `string` | Unique identifier for this lock request, generated once at construction. |
| `LockKey` | `string` | The logical resource key this request targets. |
| `RequesterId` | `string` | Identity of the requester (e.g., instance ID, pod name). |
| `RequestorName` | `string?` | Optional human-readable name for the requester. |
| `Mode` | `AcquisitionMode` | The acquisition strategy (e.g., blocking, non-blocking). |
| `RequestedDuration` | `TimeSpan` | The lease duration originally requested. |
| `RequestedAt` | `DateTime` | UTC timestamp when the context was created. |
| `CompletedAt` | `DateTime?` | UTC timestamp set by `MarkCompleted`; `null` while the request is in flight. |
| `Successful` | `bool` | `true` if the lock was acquired, `false` otherwise. Meaningful only after `MarkCompleted`. |
| `FailureReason` | `string?` | Human-readable description of why acquisition failed; `null` on success or before completion. |
| `RetryCount` | `int` | Number of retries attempted before the final outcome. Incremented externally by the lock acquisition loop. |
| `CustomProperties` | `Dictionary<string, object>` | Extensible key-value store for arbitrary metadata. Thread-safe for concurrent reads and writes via the provided methods. |
| `CorrelationId` | `string?` | Optional identifier for correlating this request across services. |
| `UserId` | `string?` | Optional end-user identity associated with the request. |
| `SessionId` | `string?` | Optional session identifier associated with the request. |

### Methods

```csharp
public void MarkCompleted(bool successful, string? failureReason)
```
Finalises the request by setting `CompletedAt` to the current UTC time, `Successful` to the given value, and `FailureReason` to the provided reason (which should be `null` when `successful` is `true`). This method should be called exactly once per context; subsequent calls will overwrite the previous outcome without throwing.

```csharp
public void AddProperty(string key, object value)
```
Adds or overwrites an entry in `CustomProperties` with the given key. If the key already exists, its value is replaced. The underlying dictionary operations are thread-safe.

```csharp
public object? GetProperty(string key)
```
Returns the value associated with the given key in `CustomProperties`, or `null` if the key is not present. The underlying dictionary read is thread-safe.

## Usage

### Example 1: Inspecting a Completed Request

```csharp
LockRequestContext context = new LockRequestContext(
    lockKey: "inventory:product-42",
    requesterId: "order-service-01",
    mode: AcquisitionMode.Blocking,
    requestedDuration: TimeSpan.FromSeconds(30),
    requestorName: "OrderProcessing",
    correlationId: "corr-abc123",
    userId: "user-789",
    sessionId: null
);

// Simulate acquisition loop
context.RetryCount = 2;
context.MarkCompleted(successful: true, failureReason: null);

Console.WriteLine($"Request {context.RequestId} for '{context.LockKey}'");
Console.WriteLine($"  Outcome: {(context.Successful ? "Acquired" : "Failed")}");
Console.WriteLine($"  Retries: {context.RetryCount}");
Console.WriteLine($"  Duration: {context.CompletedAt - context.RequestedAt}");
Console.WriteLine($"  Correlation: {context.CorrelationId}");
```

### Example 2: Attaching Custom Diagnostics

```csharp
LockRequestContext context = new LockRequestContext(
    lockKey: "cache:rebuild",
    requesterId: "worker-node-05",
    mode: AcquisitionMode.NonBlocking,
    requestedDuration: TimeSpan.FromMinutes(2)
);

context.AddProperty("attemptedBackends", new[] { "redis-01", "redis-02" });
context.AddProperty("lastError", "connection refused on redis-01");

// Later, during failure handling:
context.MarkCompleted(successful: false, failureReason: "All backends exhausted");

var attemptedBackends = context.GetProperty("attemptedBackends") as string[];
var lastError = context.GetProperty("lastError") as string;

Console.WriteLine($"Lock failed after trying: {string.Join(", ", attemptedBackends ?? Array.Empty<string>())}");
Console.WriteLine($"Last backend error: {lastError}");
```

## Notes

- **Idempotency of `MarkCompleted`**: Calling `MarkCompleted` more than once overwrites `CompletedAt`, `Successful`, and `FailureReason` without error. Callers should guard against double-completion if the final state must be immutable.
- **Thread safety**: `AddProperty` and `GetProperty` use a `ConcurrentDictionary`-backed store and are safe for concurrent access. Other properties are not synchronised; external coordination is required if multiple threads mutate `RetryCount` or call `MarkCompleted` concurrently.
- **`RetryCount` management**: The context does not increment `RetryCount` itself. The lock acquisition loop is responsible for updating this value before calling `MarkCompleted`. Setting it to a negative value is not prevented but would be semantically invalid.
- **`FailureReason` contract**: When `MarkCompleted` is called with `successful: true`, the `failureReason` argument should be `null`. Passing a non-null reason alongside success creates a contradictory state that downstream consumers must interpret defensively.
- **Default values**: Before `MarkCompleted` is called, `Successful` defaults to `false` and `CompletedAt` is `null`. Code inspecting a context must check `CompletedAt.HasValue` to determine whether the request has been finalised, rather than relying on `Successful` alone.
- **Extensibility**: `CustomProperties` accepts any object as a value. Consumers retrieving values with `GetProperty` should perform null checks and type casts appropriate to the expected data.
