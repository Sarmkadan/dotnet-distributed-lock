// existing content ...

## LockNotOwnedException

The `LockNotOwnedException` exception is thrown when attempting to release or renew a lock that is not owned by the caller. It provides information about the lock key, owner ID, and the provided owner ID.

### Usage Example

```csharp
try
{
    // Attempt to release a lock
    var releaseResponse = await client.ReleaseLockAsync(releaseRequest);
    if (releaseResponse != null && releaseResponse.Success)
    {
        Console.WriteLine($"Lock released: {releaseResponse.LockId}");
    }
    else
    {
        // Handle LockNotOwnedException
        var exception = (LockNotOwnedException)releaseResponse.Exception;
        Console.WriteLine($"Failed to release lock '{exception.LockKey}' because it is owned by '{exception.OwnerId}', not '{exception.ProvidedOwnerId}'.");
    }
}
catch (LockNotOwnedException ex)
{
    Console.WriteLine($"Failed to release lock '{ex.LockKey}' because it is owned by '{ex.OwnerId}', not '{ex.ProvidedOwnerId}'.");
}
```

// existing content ...
