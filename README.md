// existing content ...

## LockEvent

The `LockEvent` class serves as a base class for all lock-related events in the distributed lock system. It provides common properties for tracking event source and timing, such as `EventId`, `OccurredAt`, `SourceSystem`, and `CorrelationId`. 

### Usage Example

```csharp
var acquiredEvent = new LockAcquiredEvent
{
    LockId = "order-processing-123",
    LockName = "order-processing",
    OwnerId = "payment-service-01",
    ExpiresAt = DateTime.UtcNow.AddMinutes(5),
    FencingToken = 12345,
    Duration = TimeSpan.FromMinutes(5),
    Status = LockStatus.Held
};

Console.WriteLine(acquiredEvent.ToString()); // Output: LockAcquiredEvent [ID: {EventId}, Time: {OccurredAt:O}]

// Accessing properties
Console.WriteLine($"Event ID: {acquiredEvent.EventId}");
Console.WriteLine($"Occurred At: {acquiredEvent.OccurredAt:O}");
Console.WriteLine($"Source System: {acquiredEvent.SourceSystem ?? "Unknown"}");
Console.WriteLine($"Correlation ID: {acquiredEvent.CorrelationId ?? "Not set"}");
Console.WriteLine($"Lock ID: {acquiredEvent.LockId}");
Console.WriteLine($"Lock Name: {acquiredEvent.LockName}");
Console.WriteLine($"Owner ID: {acquiredEvent.OwnerId}");
Console.WriteLine($"Expires At: {acquiredEvent.ExpiresAt:O}");
Console.WriteLine($"Fencing Token: {acquiredEvent.FencingToken}");
Console.WriteLine($"Duration: {acquiredEvent.Duration}");
Console.WriteLine($"Status: {acquiredEvent.Status}");
```
