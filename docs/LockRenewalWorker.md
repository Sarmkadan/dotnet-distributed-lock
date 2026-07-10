# LockRenewalWorker
The `LockRenewalWorker` class is designed to manage the renewal of distributed locks, ensuring that locks are periodically renewed to prevent expiration and maintain exclusive access to shared resources. This class provides a mechanism for registering and unregistering locks for renewal, as well as configuring the renewal interval and other settings.

## API
* `public LockRenewalWorker`: The constructor for the `LockRenewalWorker` class, which initializes a new instance.
* `public void RegisterForRenewal`: Registers a lock for periodic renewal. This method does not take any parameters and does not return a value. It may throw an exception if the lock is already registered or if there is an issue with the lock's configuration.
* `public void UnregisterFromRenewal`: Unregisters a lock from periodic renewal. This method does not take any parameters and does not return a value. It may throw an exception if the lock is not registered or if there is an issue with the lock's configuration.
* `public override async Task StopAsync`: Stops the lock renewal worker asynchronously. This method does not take any parameters and returns a `Task` that represents the asynchronous operation. It may throw an exception if there is an issue with stopping the worker.
* `public required string LockId`: The unique identifier for the lock being managed. This property is required and must be set before using the `LockRenewalWorker`.
* `public required ulong FencingToken`: The fencing token for the lock being managed. This property is required and must be set before using the `LockRenewalWorker`.
* `public required TimeSpan RenewalInterval`: The interval at which the lock should be renewed. This property is required and must be set before using the `LockRenewalWorker`.
* `public DateTime NextRenewalTime`: The time at which the lock will next be renewed. This property is read-only and is updated automatically by the `LockRenewalWorker`.
* `public int CheckIntervalMs`: The interval at which the lock renewal worker checks for locks that need to be renewed, in milliseconds. This property can be set to adjust the frequency of renewal checks.
* `public int RetryDelaySeconds`: The delay, in seconds, between retry attempts if a lock renewal fails. This property can be set to adjust the retry behavior.
* `public double JitterPercentage`: The percentage of randomness to apply to the renewal interval to prevent concurrent renewals. This property can be set to adjust the jitter behavior.

## Usage
```csharp
// Example 1: Basic usage
var lockRenewalWorker = new LockRenewalWorker
{
    LockId = "my-lock",
    FencingToken = 123,
    RenewalInterval = TimeSpan.FromSeconds(30)
};
lockRenewalWorker.RegisterForRenewal();
await lockRenewalWorker.StopAsync();

// Example 2: Configuring renewal settings
var lockRenewalWorker = new LockRenewalWorker
{
    LockId = "my-lock",
    FencingToken = 123,
    RenewalInterval = TimeSpan.FromSeconds(30),
    CheckIntervalMs = 1000,
    RetryDelaySeconds = 5,
    JitterPercentage = 0.1
};
lockRenewalWorker.RegisterForRenewal();
await lockRenewalWorker.StopAsync();
```

## Notes
The `LockRenewalWorker` class is designed to be thread-safe, and its methods can be called from multiple threads concurrently. However, it is recommended to avoid concurrent calls to `RegisterForRenewal` and `UnregisterFromRenewal` for the same lock, as this may lead to unpredictable behavior. Additionally, the `NextRenewalTime` property may not be updated immediately after a renewal attempt, as the update may be delayed by the `CheckIntervalMs` setting. It is also important to note that the `JitterPercentage` setting can affect the timing of renewal attempts, and should be adjusted carefully to avoid concurrent renewals.
