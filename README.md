// existing content ...

## ILockApiClient

The `ILockApiClient` interface defines a contract for interacting with a distributed lock service via HTTP API. It provides methods for acquiring, releasing, renewing, and checking the status of locks.

### Usage Example

```csharp
using System;
using System.Threading.Tasks;
using SarmKadan.DistributedLock.Integration;

var client = new HttpLockApiClient(new HttpClient(), null);

var acquireRequest = new AcquireLockRequest
{
    LockName = "my-lock",
    DurationSeconds = 30,
    AutoRenew = true,
    RenewalIntervalSeconds = 60
};

var lockResponse = await client.AcquireLockAsync(acquireRequest);
if (lockResponse != null && lockResponse.Success)
{
    Console.WriteLine($"Lock acquired: {lockResponse.LockId}");
}
else
{
    Console.WriteLine("Failed to acquire lock");
}

// Release the lock
await client.ReleaseLockAsync(lockResponse.LockId, lockResponse.FencingToken);

// Renew the lock
var renewResponse = await client.RenewLockAsync(lockResponse.LockId, lockResponse.FencingToken);
if (renewResponse != null && renewResponse.Success)
{
    Console.WriteLine($"Lock renewed: {renewResponse.ExpiresAt}");
}
else
{
    Console.WriteLine("Failed to renew lock");
}

// Check the lock status
var statusResponse = await client.GetLockStatusAsync(lockResponse.LockId);
if (statusResponse != null && statusResponse.IsActive)
{
    Console.WriteLine($"Lock active: {statusResponse.LockId}");
}
else
{
    Console.WriteLine("Lock not active");
}
```

## IWebhookPublisher

The `IWebhookPublisher` interface defines a contract for publishing lock-related events to external webhook endpoints. It supports events like lock acquired, released, expired, and renewed, with configurable endpoints, timeouts, and retry policies. The `HttpWebhookPublisher` implementation sends events via HTTP POST to configured endpoints.

### Usage Example

```csharp
using System;
using System.Net.Http;
using System.Threading.Tasks;
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
