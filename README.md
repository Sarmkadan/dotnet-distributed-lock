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

## IWebhookPublisher

The `IWebhookPublisher` interface defines a contract for publishing lock-related events to external webhook endpoints. It supports events like lock acquired, released, expired, and renewed, with configurable endpoints, timeouts, and retry policies. The `HttpWebhookPublisher` implementation sends events via HTTP POST to configured endpoints.

### Usage Example

```csharp
using System;
using System.Net.Http;
using System.Threading.Tasks;
using SarmKadan.DistributedLock.Core.Models;
using SarmKadan.DistributedLock.Integration;

var config = new WebhookConfig
{
    Endpoints = new List<string> { "https://webhook.example.com/locks" },
    EnableAcquiredEvent = true,
    EnableReleasedEvent = false,
    EnableExpiredEvent = true,
    EnableRenewedEvent = false,
    TimeoutMs = 3000,
    MaxRetries = 2
};

var httpClient = new HttpClient();
var publisher = new HttpWebhookPublisher(httpClient, null, config);

var lockData = new Lock
{
    LockKey = "order-processing",
    RequesterId = "payment-service-01"
};

await publisher.PublishLockAcquiredAsync(lockData);
```

In this example:
- The `WebhookConfig` enables only `Acquired` and `Expired` events.
- The `HttpWebhookPublisher` is configured with a timeout of 3 seconds and 2 retries.
- The `PublishLockAcquiredAsync` method sends a webhook with the lock data to the configured endpoint.
