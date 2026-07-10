# LockAcquisition
The `LockAcquisition` type represents the process of acquiring a distributed lock, providing information about the lock request, its current state, and any attempts made to acquire the lock. It is used to manage and track the acquisition of locks in a distributed system, allowing for more efficient and reliable locking mechanisms.

## API
### Properties
* `Id`: A unique identifier for the lock acquisition.
* `LockKey`: The key of the lock being acquired.
* `RequesterId`: The identifier of the entity requesting the lock.
* `Mode`: The acquisition mode of the lock.
* `RequestedAt`: The time at which the lock was requested.
* `AcquiredAt`: The time at which the lock was acquired, or `null` if the lock has not been acquired.
* `Timeout`: The time span after which the lock acquisition will timeout.
* `AttemptCount`: The number of attempts made to acquire the lock.
* `MaxRetries`: The maximum number of retries allowed for acquiring the lock.
* `Attempts`: A list of attempts made to acquire the lock, where each attempt is represented by an `AcquisitionAttempt` object.
* `IsSuccessful`: A boolean indicating whether the lock acquisition was successful.
* `ErrorMessage`: An error message if the lock acquisition failed, or `null` if it was successful.

### Constructors
* `LockAcquisition()`: Initializes a new instance of the `LockAcquisition` class.
* `LockAcquisition()`: Initializes a new instance of the `LockAcquisition` class with default values.

### Methods
* `RecordAttempt(AcquisitionAttempt attempt)`: Records an attempt to acquire the lock.
* `ToString()`: Returns a string representation of the lock acquisition.

### AcquisitionAttempt Properties
* `AttemptNumber`: The number of the attempt.
* `Succeeded`: A boolean indicating whether the attempt was successful.
* `AttemptedAt`: The time at which the attempt was made.
* `ElapsedTime`: The time span elapsed during the attempt.

## Usage
```csharp
// Example 1: Creating a new LockAcquisition instance
var lockAcquisition = new LockAcquisition();
lockAcquisition.LockKey = "my-lock-key";
lockAcquisition.RequesterId = "my-requester-id";
lockAcquisition.Mode = AcquisitionMode.Exclusive;

// Example 2: Recording an attempt to acquire the lock
var attempt = new AcquisitionAttempt {
    AttemptNumber = 1,
    Succeeded = true,
    AttemptedAt = DateTime.Now,
    ElapsedTime = TimeSpan.FromMilliseconds(100)
};
lockAcquisition.RecordAttempt(attempt);
```

## Notes
The `LockAcquisition` class is designed to be thread-safe, allowing multiple threads to access and update its properties concurrently. However, it is still important to ensure that the `LockAcquisition` instance is properly synchronized when accessing its properties from multiple threads.

When using the `LockAcquisition` class, it is essential to consider edge cases such as lock timeouts, retries, and failures. The `Timeout` property can be used to specify a timeout period, after which the lock acquisition will be considered failed. The `MaxRetries` property can be used to limit the number of attempts made to acquire the lock.

In cases where the lock acquisition fails, the `ErrorMessage` property can be used to retrieve an error message indicating the reason for the failure. Additionally, the `Attempts` property can be used to retrieve a list of attempts made to acquire the lock, which can be useful for debugging and logging purposes.
