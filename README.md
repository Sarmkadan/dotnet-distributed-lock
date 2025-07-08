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

## IHttpClientFactory

The `IHttpClientFactory` interface provides a contract for creating and managing HTTP clients. It allows for the creation of typed clients with proper connection pooling and resilience, preventing socket exhaustion by reusing client instances across the application.

### Usage Example

```csharp
using System;
using System.Net.Http;
using System.Threading.Tasks;
using SarmKadan.DistributedLock.Integration;

var factory = new DefaultHttpClientFactory(new HttpClient(), null);

var client = factory.CreateClient("my-client");
var response = await client.GetAsync("https://example.com");
response.EnsureSuccessStatusCode();

var timeout = factory.DefaultTimeout;
var maxRetries = factory.MaxRetries;
var automaticDecompression = factory.AutomaticDecompression;
var baseUrl = factory.BaseUrl;
var apiKey = factory.ApiKey;
var defaultHeaders = factory.DefaultHeaders;

var lockServiceClient = new LockServiceHttpClient(client, null, new HttpClientConfiguration());
var lockResponse = await lockServiceClient.GetLockAsync("my-lock");
if (lockResponse != null)
{
    Console.WriteLine($"Lock acquired: {lockResponse}");
}
else
{
    Console.WriteLine("Failed to acquire lock");
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