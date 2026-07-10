# LockEventSubscriber
The `LockEventSubscriber` type is designed to handle events related to distributed locking, providing a way to track and monitor lock acquisitions, releases, and contention events. It serves as a base class for various event subscriber implementations, allowing for customization and extension of its functionality.

## API
* `public abstract Task RegisterAsync`: An abstract method that must be implemented by derived classes to register the event subscriber. The purpose of this method is to set up the event subscriber to receive lock events.
* `public LoggingLockEventSubscriber(ILogger<LoggingLockEventSubscriber> logger)`: A constructor for the `LoggingLockEventSubscriber` class, which takes an instance of `ILogger<LoggingLockEventSubscriber>` as a parameter. This constructor is used to create a new instance of the `LoggingLockEventSubscriber` class with the specified logger.
* `public override async Task RegisterAsync`: An override of the `RegisterAsync` method in the `LoggingLockEventSubscriber` class, which registers the event subscriber using the provided logger.
* `public MetricsTrackingEventSubscriber(ILogger<MetricsTrackingEventSubscriber> logger)`: A constructor for the `MetricsTrackingEventSubscriber` class, which takes an instance of `ILogger<MetricsTrackingEventSubscriber>` as a parameter. This constructor is used to create a new instance of the `MetricsTrackingEventSubscriber` class with the specified logger.
* `public override async Task RegisterAsync`: An override of the `RegisterAsync` method in the `MetricsTrackingEventSubscriber` class, which registers the event subscriber using the provided logger.
* `public EventMetrics GetMetrics`: A method that returns an instance of `EventMetrics`, which contains metrics related to lock events, such as acquisitions, releases, failures, and contention events.
* `public long Acquisitions`: A property that returns the number of successful lock acquisitions.
* `public long Releases`: A property that returns the number of successful lock releases.
* `public long Failures`: A property that returns the number of failed lock acquisitions or releases.
* `public long ContentionEvents`: A property that returns the number of contention events, which occur when multiple threads or processes attempt to acquire the same lock simultaneously.
* `public DateTime Timestamp`: A property that returns the timestamp of the last update to the event metrics.
* `public static IServiceCollection AddLockEventSubscribers`: A static method that adds lock event subscribers to the specified `IServiceCollection`.
* `public static async Task InitializeLockEventSubscribersAsync`: A static method that initializes lock event subscribers asynchronously.

## Usage
The following examples demonstrate how to use the `LockEventSubscriber` type:
```csharp
// Example 1: Registering a logging lock event subscriber
var logger = new LoggerFactory().CreateLogger<LoggingLockEventSubscriber>();
var subscriber = new LoggingLockEventSubscriber(logger);
await subscriber.RegisterAsync();

// Example 2: Using metrics tracking event subscriber
var metricsLogger = new LoggerFactory().CreateLogger<MetricsTrackingEventSubscriber>();
var metricsSubscriber = new MetricsTrackingEventSubscriber(metricsLogger);
await metricsSubscriber.RegisterAsync();
var metrics = metricsSubscriber.GetMetrics();
Console.WriteLine($"Acquisitions: {metrics.Acquisitions}, Releases: {metrics.Releases}, Failures: {metrics.Failures}, Contention Events: {metrics.ContentionEvents}");
```

## Notes
When using the `LockEventSubscriber` type, consider the following edge cases and thread-safety remarks:
* The `RegisterAsync` method must be called before attempting to access event metrics.
* The `GetMetrics` method returns a snapshot of the current event metrics, which may not reflect the latest values if the metrics are being updated concurrently.
* The `LockEventSubscriber` type is designed to be thread-safe, but it is still important to ensure that the `RegisterAsync` method is called only once, and that the `GetMetrics` method is not called while the `RegisterAsync` method is still executing.
* The `AddLockEventSubscribers` and `InitializeLockEventSubscribersAsync` methods are designed to be used in a service registration context, and should be used accordingly to avoid conflicts with other service registrations.
