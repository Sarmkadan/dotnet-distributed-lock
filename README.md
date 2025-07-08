// existing content ...

## LockAcquisitionException

The `LockAcquisitionException` exception is thrown when a lock cannot be acquired within the specified timeout. It provides additional information about the lock key, timeout, and retry count.

### Usage Example

```csharp
try
{
    // Attempt to acquire a lock
    var lockResponse = await client.AcquireLockAsync(acquireRequest);
    if (lockResponse != null && lockResponse.Success)
    {
        Console.WriteLine($"Lock acquired: {lockResponse.LockId}");
    }
    else
    {
        // Handle LockAcquisitionException
        var exception = (LockAcquisitionException)lockResponse.Exception;
        Console.WriteLine($"Failed to acquire lock '{exception.LockKey}' within {exception.Timeout.TotalSeconds}s after {exception.RetryCount} retries.");
    }
}
catch (LockAcquisitionException ex)
{
    Console.WriteLine($"Failed to acquire lock '{ex.LockKey}' within {ex.Timeout.TotalSeconds}s after {ex.RetryCount} retries.");
}
```

// existing content ...
