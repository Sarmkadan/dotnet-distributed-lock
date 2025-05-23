# IWebhookPublisher

The `IWebhookPublisher` interface and its concrete implementation `HttpWebhookPublisher` provide a mechanism for publishing distributed lock lifecycle events (acquisition, release, expiration, and renewal) to configured HTTP endpoints via webhooks. This enables external systems to react to lock state changes in real-time, facilitating integration with monitoring, auditing, or workflow orchestration tools.

## API

### `HttpWebhookPublisher`
