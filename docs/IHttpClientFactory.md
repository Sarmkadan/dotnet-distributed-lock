# IHttpClientFactory

A factory abstraction for creating and managing `HttpClient` instances with built-in retry logic, default timeouts, and lock service integration for distributed locking scenarios.

## API

### `DefaultHttpClientFactory`
A concrete implementation of `IHttpClientFactory` that provides configurable HTTP clients with retry policies and lock service integration.

### `HttpClient CreateClient()`
Creates a new `HttpClient` instance with default configuration.

- **Parameters**: None
- **Return value**: A new `HttpClient` instance configured with default timeout, headers, and retry settings.
- **Exceptions**: Throws `InvalidOperationException` if the factory is disposed or misconfigured.

### `HttpClient GetClient()`
Retrieves or creates a shared `HttpClient` instance with default configuration.

- **Parameters**: None
- **Return value**: A shared `HttpClient` instance configured with default timeout, headers, and retry settings.
- **Exceptions**: Throws `InvalidOperationException` if the factory is disposed or misconfigured.

### `TimeSpan DefaultTimeout`
Gets or sets the default timeout for HTTP requests.

- **Type**: `TimeSpan`
- **Default**: 30 seconds
- **Remarks**: Changing this value affects all subsequently created clients.

### `int MaxRetries`
Gets or sets the maximum number of retry attempts for failed requests.

- **Type**: `int`
- **Default**: 3
- **Remarks**: Retries are only attempted for transient errors (e.g., 5xx responses, network failures).

### `bool AutomaticDecompression`
Gets or sets whether automatic response decompression (e.g., gzip, deflate) is enabled.

- **Type**: `bool`
- **Default**: `true`
- **Remarks**: When `true`, the client automatically decompresses responses with `Content-Encoding: gzip` or `deflate`.

### `string? BaseUrl`
Gets or sets the base URL for HTTP requests.

- **Type**: `string?`
- **Default**: `null`
- **Remarks**: If `null`, requests must use absolute URIs. If set, relative URIs are resolved against this base.

### `string? ApiKey`
Gets or sets the API key used for authentication in requests.

- **Type**: `string?`
- **Default**: `null`
- **Remarks**: If set, the key is added to the `DefaultHeaders` under the `Authorization` header with scheme `Bearer`.

### `Dictionary<string, string> DefaultHeaders`
Gets the collection of default headers added to every request.

- **Type**: `Dictionary<string, string>`
- **Default**: Empty
- **Remarks**: Modifying this collection affects all subsequently created clients.

### `LockServiceHttpClient`
A specialized `HttpClient` for interacting with the distributed lock service API.

- **Type**: `HttpClient`
- **Remarks**: Pre-configured with lock service endpoints and authentication.

### `async Task<string?> GetLockAsync(string lockId)`
Retrieves the status of a lock.

- **Parameters**:
  - `lockId` (string): The unique identifier of the lock.
- **Return value**: The lock status as a string (e.g., `"Acquired"`, `"Released"`), or `null` if the lock does not exist.
- **Exceptions**: Throws `HttpRequestException` on network failures or `InvalidOperationException` if the lock service is unreachable.
- **Remarks**: Requires `BaseUrl` and `ApiKey` to be set.

### `async Task<string?> AcquireLockAsync(string lockId, TimeSpan? timeout = null)`
Attempts to acquire a distributed lock.

- **Parameters**:
  - `lockId` (string): The unique identifier of the lock.
  - `timeout` (TimeSpan?, optional): The maximum time to wait for the lock. Defaults to `DefaultTimeout`.
- **Return value**: The lock identifier if acquired, or `null` if the lock could not be acquired within the timeout.
- **Exceptions**: Throws `HttpRequestException` on network failures or `InvalidOperationException` if the lock service is unreachable.
- **Remarks**: The lock is automatically released after the configured timeout unless explicitly released via `ReleaseLockAsync`.

### `async Task<string?> ReleaseLockAsync(string lockId)`
Releases an acquired distributed lock.

- **Parameters**:
  - `lockId` (string): The unique identifier of the lock.
- **Return value**: The lock identifier if successfully released, or `null` if the lock did not exist.
- **Exceptions**: Throws `HttpRequestException` on network failures or `InvalidOperationException` if the lock service is unreachable.

## Usage

### Basic HTTP Client Usage
