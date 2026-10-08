# API Middleware Pipeline

This document describes the ASP.NET Core middleware components in `src/Api/Middleware`, the order in which they should run, and how each one is configured.

The middleware is part of the optional HTTP surface (see `docs/ARCHITECTURE.md`, section "src/Api"). A library-only consumer never registers it.

## Important: registration is not in this repository

The repository does not contain a `Program.cs` or any extension method that adds these components to the pipeline. The `UseMiddleware<>` and `app.Use*` calls live in the host application that exposes the API. The order below is therefore the **recommended** order, derived from what each component does. Treat it as the contract for the host, not as something the library enforces.

## Components

| Component | Source file | Responsibility | Short-circuits? |
|---|---|---|---|
| `RequestLoggingMiddleware` | `RequestLoggingMiddleware.cs` | Logs request start and completion with duration and status code | No (rethrows) |
| `ExceptionHandlingMiddleware` | `ExceptionHandlingMiddleware.cs` | Converts exceptions into a JSON error body with a mapped HTTP status | Yes, on exception |
| `RateLimitingMiddleware` | `RateLimitingMiddleware.cs` | Limits requests per client IP over a sliding window | Yes, returns 429 |
| `AuthenticationMiddleware` | `AuthenticationMiddleware.cs` | Checks the `X-API-Key` header | Yes, returns 401 or 403 |

`RateLimitingMiddlewareValidation.cs` is not a middleware. It holds extension methods (`Validate`, `IsValid`, `EnsureValid`) that check a `RateLimitingMiddleware` instance's options.

## Recommended order

```
Request
  -> RequestLoggingMiddleware        (outermost: sees the final status code)
    -> ExceptionHandlingMiddleware   (catches everything below it)
      -> RateLimitingMiddleware      (throttles before any credential check)
        -> AuthenticationMiddleware  (rejects unauthenticated calls)
          -> Routing / Endpoints     (controllers: DistributedLock, Health, Metrics)
Response
```

### Why this order

1. **RequestLogging first.** It wraps everything, so the logged status code is the one the client actually receives, including error responses written by the exception handler.
2. **ExceptionHandling second.** It must sit outside `RateLimitingMiddleware` and `AuthenticationMiddleware`, because both can throw. `RateLimitingMiddleware` logs and rethrows exceptions from downstream code, and only `ExceptionHandlingMiddleware` turns them into responses.
3. **RateLimiting before Authentication.** Unauthenticated traffic (for example, brute-forcing `X-API-Key`) is throttled before the key is checked. Rate limiting also applies to the public health endpoints when they are on the same pipeline, so keep that in mind when setting limits.
4. **Authentication last before endpoints.** Only requests that pass the rate limit and carry a valid key reach the controllers.

Routing and endpoint mapping (`UseRouting`, `MapControllers`, `MapHealthChecks`) must come after all four components, so that the middleware runs for every request.

## Component details

### RequestLoggingMiddleware

- **Purpose:** Structured request timing and status logging.
- **Logs:**
  - `Request started: {RequestId} {Method} {Path}` (Information)
  - `Request completed: {RequestId} {Method} {Path} {StatusCode} {DurationMs}ms` (Information)
  - `Request failed: {RequestId} {Method} {Path} {DurationMs}ms` (Error), then the exception is rethrown.
- **Request ID:** `context.TraceIdentifier`.
- **Configuration:** None. Log levels are controlled by the host's logging configuration.

### ExceptionHandlingMiddleware

- **Purpose:** Prevents stack traces from reaching clients and gives every error a stable `ErrorCode`.
- **Response body:** `ErrorResponseBody` with `Message`, `ErrorCode`, and `Timestamp` (UTC). Content type is `application/json`.
- **Exception mapping** (first matching case wins):

| Exception type | HTTP status | `ErrorCode` |
|---|---|---|
| `LockAcquisitionException` | 409 Conflict | `LOCK_ACQUISITION_FAILED` |
| `LockNotOwnedException` | 403 Forbidden | `LOCK_NOT_OWNED` |
| `LockExpiredException` | 410 Gone | `LOCK_EXPIRED` |
| `InvalidFencingTokenException` | 400 Bad Request | `INVALID_FENCING_TOKEN` |
| `InvalidOperationException` | 400 Bad Request | `INVALID_OPERATION` |
| `ArgumentException` | 400 Bad Request | `INVALID_ARGUMENT` |
| Any other exception | 500 Internal Server Error | `INTERNAL_ERROR` |

- **Message handling:** For the mapped exceptions, `Message` is the exception message. For `INTERNAL_ERROR`, the message is the fixed text `An unexpected error occurred`, so internal details are not exposed. The original exception is logged at Error level (and at Warning level for unmapped types).
- **Configuration:** None. The mapping is fixed in code.

Note: `LockNotOwnedException` is mapped to 403, and `AuthenticationMiddleware` also uses 403 for an invalid key. Clients should use `ErrorCode` rather than the status alone to tell these apart.

### RateLimitingMiddleware

- **Purpose:** Limits the number of requests per client in a sliding window to reduce abuse and denial-of-service risk on lock endpoints.
- **Client identity:**
  1. The first value of the `X-Forwarded-For` header, if present.
  2. Otherwise `context.Connection.RemoteIpAddress`.
  3. Otherwise `"unknown"`.
- **Behavior:** Each request timestamp is recorded per client. Timestamps older than `WindowSizeSeconds` are removed. If the count has reached `MaxRequestsPerWindow`, the request is rejected.
- **Rejected response:** HTTP 429 with `application/json`:
  ```json
  { "message": "Rate limit exceeded. Please try again later.", "retryAfterSeconds": 60 }
  ```
  Note that `retryAfterSeconds` is set to `WindowSizeSeconds`. This is the full window, not the time until the next slot opens. The response does not set the standard `Retry-After` header.
- **Storage:** A `static` dictionary shared by all instances in the process. It is not shared across replicas. Each instance of the API keeps its own counts.
- **Cleanup:** When the dictionary holds more than 10,000 clients, entries with no remaining timestamps are removed.

#### Configuration: `RateLimitingOptions`

| Property | Default | Meaning |
|---|---|---|
| `MaxRequestsPerWindow` | `100` | Maximum requests per client within the window. |
| `WindowSizeSeconds` | `60` | Length of the sliding window in seconds. |

Options are passed through the optional `options` parameter of the constructor. If none is supplied, the defaults above are used. Set them through the host's `UseMiddleware<RateLimitingMiddleware>(...)` registration or your DI setup.

#### Validation

`RateLimitingMiddlewareValidation` checks the options at runtime:

- `Validate()` returns a list of problems. Both values must be greater than zero.
- `IsValid()` returns `true` when there are no problems.
- `EnsureValid()` throws `ArgumentException` listing every problem.

The validation reads the private `_options` field by reflection. If the field name changes in `RateLimitingMiddleware`, validation throws `InvalidOperationException`.

Call `EnsureValid()` at startup to fail fast on invalid settings such as `MaxRequestsPerWindow = 0`.

#### Security note on `X-Forwarded-For`

The middleware trusts `X-Forwarded-For` without checking that the request came from a known proxy. A client that connects directly can send any value and get a fresh counter on each request. Only rely on this component behind a reverse proxy that overwrites the header, or change `GetClientIp` to use the proxy's trusted value.

### AuthenticationMiddleware

- **Purpose:** Requires an API key in the `X-API-Key` header on protected endpoints.
- **Public endpoints (no key needed):** Matched with `StartsWith` against the request path:
  - `/api/health/live`
  - `/api/health/ready`
  - `/swagger`
  - `/swagger/index.html`
- **Responses:**
  - Header missing: HTTP 401, body `{ "message": "API key is required" }`.
  - Key invalid: HTTP 403, body `{ "message": "Invalid API key" }`.
- **On success:** Adds `ApiKey` and `AuthenticatedAt` (UTC) to `context.Items` for downstream code.

#### Key validation

1. The key must be at least 20 characters and not whitespace.
2. The valid key list comes from the environment variable `VALID_API_KEYS`, separated by `;`.
   - If the variable is set, the key must be an exact match with one of the entries.
   - If it is not set or empty, **development mode** applies: any key of 20 or more characters made only of letters, digits, `-` and `_` is accepted.

Development mode accepts any well-formed key. Do not run production without setting `VALID_API_KEYS`.

#### Configuration

| Setting | Source | Required in production |
|---|---|---|
| `VALID_API_KEYS` | Environment variable, `;`-separated | Yes |

Keys are compared as plain strings and are not logged by this middleware. Logs record only the path.

## Example: minimal host wiring

This is an illustration for a host application. It is not present in this repository.

```csharp
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();
app.UseMiddleware<AuthenticationMiddleware>();

app.UseRouting();
app.MapControllers();
app.MapHealthChecks("/api/health/ready");
```

Replace the health path in the example with the one your host exposes. Check that `AuthenticationMiddleware` public paths match your routes.

## Checklist for a new host

- [ ] Register the four components in the order above.
- [ ] Set `VALID_API_KEYS` in the environment.
- [ ] Set `RateLimitingOptions` and call `EnsureValid()` at startup.
- [ ] Put a reverse proxy in front that overwrites `X-Forwarded-For`, or accept the spoofing risk.
- [ ] Confirm that `/api/health/live`, `/api/health/ready`, and `/swagger` are the paths you expose.
- [ ] Put routing and endpoint mapping after the middleware.
