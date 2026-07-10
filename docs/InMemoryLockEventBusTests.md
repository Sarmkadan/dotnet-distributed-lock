# InMemoryLockEventBusTests

Unit tests for the in-memory implementation of `ILockEventBus` used to verify lock lifecycle event publishing and subscription behavior in distributed lock scenarios.

## API

### `InMemoryLockEventBusTests`

Public constructor for the test class. Initializes a new instance of the test fixture with required dependencies.

### `Constructor_WithNullLogger_ThrowsArgumentNullException`

Verifies that the constructor throws an `ArgumentNullException` when a null `ILogger<InMemoryLockEventBus>` is provided.

- **Parameters**: None
- **Return value**: None
- **Throws**: `ArgumentNullException` with parameter name `"logger"` when `logger` is `null`

### `PublishAsync_WithNoSubscribers_DoesNotThrow`

Ensures that publishing an event with no registered subscribers completes successfully without throwing exceptions.

- **Parameters**: None
- **Return value**: `Task` representing the asynchronous operation
- **Throws**: None

### `PublishAsync_WithSyncSubscriber_InvokesHandler`

Validates that a synchronous subscriber delegate is invoked when an event is published.

- **Parameters**: None
- **Return value**: `Task` representing the asynchronous operation
- **Throws**: None

### `PublishAsync_WithAsyncSubscriber_InvokesHandler`

Validates that an asynchronous subscriber delegate is invoked when an event is published.

- **Parameters**: None
- **Return value**: `Task` representing the asynchronous operation
- **Throws**: None

### `PublishAsync_WithMultipleSubscribers_InvokesAll`

Confirms that all registered subscribers for a given event type are invoked when the event is published.

- **Parameters**: None
- **Return value**: `Task` representing the asynchronous operation
- **Throws**: None

### `GetSubscriberCount_WithNoSubscribers_ReturnsZero`

Checks that the subscriber count for an event type is zero when no subscriptions exist.

- **Parameters**: None
- **Return value**: `void`
- **Throws**: None

### `GetSubscriberCount_AfterSubscription_ReturnsCorrectCount`

Ensures that the subscriber count reflects the number of active subscriptions after registration.

- **Parameters**: None
- **Return value**: `void`
- **Throws**: None

### `GetSubscriberCount_DifferentEventTypes_AreIndependent`

Verifies that subscriber counts for different event types do not interfere with one another.

- **Parameters**: None
- **Return value**: `void`
- **Throws**: None

### `PublishAsync_WithCorrelationId_PropagatesIt`

Asserts that the `CorrelationId` provided during publishing is propagated to all subscriber invocations.

- **Parameters**: None
- **Return value**: `Task` representing the asynchronous operation
- **Throws**: None

### `PublishAsync_SetsSourceSystemIfNotSet`

Confirms that the `SourceSystem` property is set on the event if it was not previously assigned.

- **Parameters**: None
- **Return value**: `Task` representing the asynchronous operation
- **Throws**: None

### `PublishAsync_WhenSubscriberThrows_ContinuesToPublish`

Ensures that exceptions thrown by subscribers do not prevent other subscribers from being invoked.

- **Parameters**: None
- **Return value**: `Task` representing the asynchronous operation
- **Throws**: None

### `PublishAsync_WithLockAcquiredEvent_Works`

Validates that `LockAcquiredEvent` can be published and handled without errors.

- **Parameters**: None
- **Return value**: `Task` representing the asynchronous operation
- **Throws**: None

### `PublishAsync_WithLockReleasedEvent_Works`

Validates that `LockReleasedEvent` can be published and handled without errors.

- **Parameters**: None
- **Return value**: `Task` representing the asynchronous operation
- **Throws**: None

### `PublishAsync_WithLockRenewedEvent_Works`

Validates that `LockRenewedEvent` can be published and handled without errors.

- **Parameters**: None
- **Return value**: `Task` representing the asynchronous operation
- **Throws**: None

### `PublishAsync_WithLockFailedEvent_Works`

Validates that `LockFailedEvent` can be published and handled without errors.

- **Parameters**: None
- **Return value**: `Task` representing the asynchronous operation
- **Throws**: None

### `PublishAsync_ConcurrentPublishers_AllSucceed`

Ensures that concurrent calls to `PublishAsync` from multiple threads complete successfully without data corruption.

- **Parameters**: None
- **Return value**: `Task` representing the asynchronous operation
- **Throws**: None

### `PublishAsync_ConcurrentSubscribersAndPublishers_MaintainsConsistency`

Validates that concurrent subscription, unsubscription, and publishing operations maintain internal consistency and do not cause race conditions.

- **Parameters**: None
- **Return value**: `Task` representing the asynchronous operation
- **Throws**: None

## Usage

### Example 1: Basic Subscription and Publishing
