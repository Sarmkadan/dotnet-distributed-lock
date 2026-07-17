# Architecture

For the full picture - module layout, backend trade-offs, data flow of a blocking
acquire, DI wiring, extension points and known limitations - see
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md). Short version: `ILockService` orchestrates
lock semantics (retry with backoff, fencing tokens, metrics) on top of a single
`ILockRepository` seam with Redis, PostgreSQL, SQLite and in-memory implementations.

## CacheKeyGenerator

The `CacheKeyGenerator` class provides utility methods for generating consistent, predictable cache keys used throughout the distributed lock system. It ensures consistent key formats across all components for cache coordination, supports pattern matching for bulk operations, and provides methods for extracting information from keys. The generator creates keys for individual locks, lock families, metrics, status, owners, queries, configurations, and tags, with helper methods to identify key types and extract lock IDs.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

// Initialize a distributed cache (e.g., Redis, MemoryCache, etc.)
var cache = new MemoryDistributedCache(new OptionsWrapper<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions()));

// Generate keys for various cache operations
string lockKey = CacheKeyGenerator.GenerateLockKey("user-session-lock-123");
Console.WriteLine($"Lock key: {lockKey}"); // Output: lock:user-session-lock-123

string metricsKey = CacheKeyGenerator.GenerateMetricsKey("user-session-lock-123");
Console.WriteLine($"Metrics key: {metricsKey}"); // Output: metrics:user-session-lock-123

string systemMetricsKey = CacheKeyGenerator.GenerateSystemMetricsKey();
Console.WriteLine($"System metrics key: {systemMetricsKey}"); // Output: metrics:system

string ownerLocksKey = CacheKeyGenerator.GenerateOwnerLocksKey("user-service-42");
Console.WriteLine($"Owner locks key: {ownerLocksKey}"); // Output: lock:owner:user-service-42

string statusKey = CacheKeyGenerator.GenerateStatusKey("user-session-lock-123");
Console.WriteLine($"Status key: {statusKey}"); // Output: status:user-session-lock-123

string configurationKey = CacheKeyGenerator.GenerateConfigurationKey("default-lock-timeout");
Console.WriteLine($"Configuration key: {configurationKey}"); // Output: config:default-lock-timeout

string tagKey = CacheKeyGenerator.GenerateTagKey("session-management", "user-locks");
Console.WriteLine($"Tag key: {tagKey}"); // Output: tag:session-management:user-locks

## LockEventExtensions

The `LockEventExtensions` class provides extension methods for `LockEvent` types to enable common operations such as formatting, validation, and conversion between event types. These utilities simplify working with lock events by providing a consistent API for extracting information, checking event properties, and converting events to other types.


## InvalidFencingTokenExceptionExtensions

The `InvalidFencingTokenExceptionExtensions` class provides utility methods for analyzing and working with `InvalidFencingTokenException` instances. These extensions help determine the relationship between provided and current fencing tokens, create new exception instances with updated tokens, and extract detailed token information for logging and debugging purposes.

### What it does

This extension class provides methods to:

- Check if the provided token differs from the current token (`IsTokenMismatch`)
- Determine if the provided token is older than the current token (`IsTokenSuperseded`)
- Identify if the provided token is newer than the current token (`IsTokenFromFuture`)
- Create a new exception with updated tokens (`WithTokens`)
- Get a formatted string containing both token values for logging (`GetTokenDetails`)

### Usage Example

```csharp
using SarmKadan.DistributedLock.Exceptions;
using System;

try
{
    // Attempt to acquire a distributed lock with an outdated fencing token
    await lockService.AcquireAsync("resource-123", "stale-token-456", TimeSpan.FromSeconds(30));
}
catch (InvalidFencingTokenException ex)
{
    // Check the relationship between tokens
    bool isMismatch = ex.IsTokenMismatch();
    bool isSuperseded = ex.IsTokenSuperseded(); // true if provided token is older
    bool isFromFuture = ex.IsTokenFromFuture(); // true if provided token is newer
    
    Console.WriteLine($"Token mismatch: {isMismatch}");
    Console.WriteLine($"Token superseded: {isSuperseded}");
    Console.WriteLine($"Token from future: {isFromFuture}");
    
    // Get detailed token information for logging
    string tokenDetails = ex.GetTokenDetails();
    Console.WriteLine($"Token details: {tokenDetails}");
    
    // Create a new exception with updated tokens
    var updatedException = ex.WithTokens("new-provided-token-789", "new-current-token-789");
    Console.WriteLine($"Updated exception: {updatedException.GetTokenDetails()}");
}
```

### What it does

This extension class provides methods to:

- Check if an event represents a successful acquisition or failure (`IsAcquisitionSuccessful`, `IsFailure`)
- Extract lock identifiers and owner information from various event types (`GetLockId`, `GetOwnerId`)
- Format events for logging purposes (`ToLogString`)
- Convert events to failure notifications (`ToFailureEvent`)
- Check if events occurred within specific time ranges (`IsWithinTimeRange`)
- Calculate durations and expiration times (`GetDuration`, `GetExpirationTime`)
- Determine if events are related to specific locks (`IsRelatedToLock`)
- Extract fencing tokens from acquisition events (`GetFencingToken`)

## FencingTokenExtensions

The `FencingTokenExtensions` class provides extension methods for `FencingToken` to enable common operations such as parsing, string conversion, age calculation, and comparison operations. These utilities simplify working with fencing tokens by providing a consistent API for token manipulation, validation, and relationship checking.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Models;
using System;

// Generate a new fencing token
var newToken = FencingToken.NewToken();
Console.WriteLine($"New token: {newToken}");

// Parse a fencing token from string
string tokenString = "12345";
if (FencingToken.TryParse(tokenString, out var parsedToken))
{
    Console.WriteLine($"Parsed token: {parsedToken}");
}

// Convert token to string representation
string tokenAsString = newToken.ToTokenString();
Console.WriteLine($"Token as string: {tokenAsString}");

// Get the age of a token
TimeSpan age = newToken.GetAge();
Console.WriteLine($"Token age: {age.TotalSeconds} seconds");

// Compare tokens
var olderToken = newToken.WithSequenceNumber(100);
var newerToken = newToken.WithSequenceNumber(200);

bool isLess = newToken.IsLessThan(newerToken);
bool isGreaterOrEqual = newerToken.IsGreaterThanOrEqual(newToken);
bool isLessOrEqual = newToken.IsLessThanOrEqual(newerToken);

Console.WriteLine($"Token comparison - IsLessThan: {isLess}, IsGreaterThanOrEqual: {isGreaterOrEqual}, IsLessThanOrEqual: {isLessOrEqual}");

// Calculate sequence difference
long sequenceDiff = newerToken.SequenceDifference(newToken);
Console.WriteLine($"Sequence difference: {sequenceDiff}");

// Check if tokens are adjacent
bool isAdjacent = newToken.IsAdjacentTo(newerToken.WithSequenceNumber(101));
Console.WriteLine($"Tokens are adjacent: {isAdjacent}");
```

## FencingTokenJsonExtensions

The `FencingTokenJsonExtensions` class provides extension methods for serializing and deserializing `FencingToken` instances to and from JSON strings using System.Text.Json. This enables easy storage and transmission of fencing tokens in distributed systems, with support for both compact and indented JSON formatting.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Models;
using System;

// Create a new fencing token
var token = FencingToken.NewToken();
Console.WriteLine($"Original token: {token}");

// Serialize to JSON (compact format)
string jsonCompact = token.ToJson();
Console.WriteLine($"Compact JSON: {jsonCompact}");
// Output: {"token":"abc123","sequenceNumber":12345,"issuedAt":"2025-07-19T14:30:00Z"}

// Serialize to JSON (indented format for readability)
string jsonIndented = token.ToJson(indented: true);
Console.WriteLine($"Indented JSON:\n{jsonIndented}");

// Deserialize from JSON
FencingToken? deserializedToken = FencingTokenJsonExtensions.FromJson(jsonCompact);
Console.WriteLine($"Deserialized token: {deserializedToken}");

// Try to deserialize with error handling
if (FencingTokenJsonExtensions.TryFromJson(jsonCompact, out var tryToken))
{
    Console.WriteLine($"TryFromJson succeeded: {tryToken}");
}
else
{
    Console.WriteLine("TryFromJson failed");
}

// Deserialize null or empty strings
FencingToken? nullToken = FencingTokenJsonExtensions.FromJson(null);
Console.WriteLine($"FromJson(null): {nullToken}"); // Output: null

FencingToken? emptyToken = FencingTokenJsonExtensions.FromJson("");
Console.WriteLine($"FromJson(empty): {emptyToken}"); // Output: null
```

## LockRequestContextExtensions

The `LockRequestContextExtensions` class provides extension methods for `LockRequestContext` to enhance functionality for audit trails, diagnostics, and distributed tracing scenarios. These utilities help determine lock request expiration status, calculate remaining time, generate diagnostic reports, check successful completion within duration, and collect standard metrics for monitoring and alerting.

## ValidationHelperValidation

The `ValidationHelperValidation` class provides comprehensive validation utilities for lock configuration parameters through a set of static methods that validate lock names, durations, renewal intervals, fencing tokens, owner IDs, expiration dates, and API keys. It offers three validation approaches: returning error lists for programmatic processing, boolean checks for quick validation, and exception-throwing validations for immediate failure handling. The class supports both individual lock validations and batch validation of multiple configurations.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Utilities.Helpers;
using System;

// Validate a simple lock configuration
var simpleErrors = ValidationHelperValidation.Validate(
    lockName: "user-session-lock-123",
    duration: TimeSpan.FromMinutes(5),
    renewalInterval: TimeSpan.FromMinutes(1)
);

if (simpleErrors.Count > 0)
{
    Console.WriteLine("Validation errors:");
    foreach (var error in simpleErrors)
    {
        Console.WriteLine($"- {error}");
    }
}
else
{
    Console.WriteLine("Lock configuration is valid!");
}

// Validate a complete lock configuration with all parameters
var completeErrors = ValidationHelperValidation.Validate(
    lockName: "payment-processing-lock",
    duration: TimeSpan.FromMinutes(30),
    renewalInterval: TimeSpan.FromMinutes(5),
    fencingToken: 12345UL,
    ownerId: "payment-service-42",
    expiresAt: DateTime.UtcNow.AddMinutes(30),
    apiKey: "sk_live_abc123xyz789"
);

// Use boolean validation for quick checks
bool isValid = ValidationHelperValidation.IsValid(
    lockName: "cache-lock",
    duration: TimeSpan.FromSeconds(30)
);
Console.WriteLine($"Is valid: {isValid}");

// Use EnsureValid for immediate validation with detailed error messages
try
{
    ValidationHelperValidation.EnsureValid(
        lockName: "resource-lock",
        duration: TimeSpan.FromHours(2),
        renewalInterval: TimeSpan.FromMinutes(10)
    );
    Console.WriteLine("Configuration validated successfully!");
}
catch (ArgumentException ex)
{
    Console.WriteLine($"Validation failed: {ex.Message}");
}

// Validate multiple configurations at once
var configurations = new[]
{
    ("user-session-lock-1", TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1)),
    ("user-session-lock-2", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5)),
    ("payment-lock", TimeSpan.FromHours(1), TimeSpan.FromMinutes(15))
};

var batchErrors = ValidationHelperValidation.Validate(configurations);
if (batchErrors.Count > 0)
{
    Console.WriteLine($"Batch validation found {batchErrors.Count} errors:");
    foreach (var error in batchErrors)
    {
        Console.WriteLine(error);
    }
}
```

### Usage Example

```csharp
using SarmKadan.DistributedLock.Models;
using System;
using System.Collections.Generic;

// Create a lock request context
var requestContext = new LockRequestContext(
    requestId: Guid.NewGuid().ToString(),
    lockKey: "user-session-lock-123",
    requesterId: "auth-service-42",
    mode: LockMode.Exclusive,
    requestedDuration: TimeSpan.FromSeconds(30),
    requestedAt: DateTime.UtcNow
)
{
    RequestorName = "Authentication Service",
    UserId = "user-789",
    SessionId = "session-abc-123",
    CustomProperties = new Dictionary<string, object>
    {
        ["priority"] = "high",
        ["operation"] = "user_login"
    }
};

// Check if the lock request has expired
bool hasExpired = requestContext.HasExpired();
Console.WriteLine($"Has expired: {hasExpired}");

// Get the remaining time before expiration
TimeSpan remainingTime = requestContext.RemainingTime();
Console.WriteLine($"Remaining time: {remainingTime.TotalSeconds:F2} seconds");

// Generate a diagnostic report for logging and debugging
string diagnosticReport = requestContext.ToDiagnosticString();
Console.WriteLine(diagnosticReport);

// Determine if the request was completed successfully within the requested duration
bool isSuccessfulWithinDuration = requestContext.IsSuccessfulWithinDuration();
Console.WriteLine($"Successful within duration: {isSuccessfulWithinDuration}");

// Get standard metrics for monitoring and alerting
var metrics = requestContext.GetStandardMetrics();
foreach (var metric in metrics)
{
    Console.WriteLine($"{metric.Key}: {metric.Value}");
}
```

### What it does

This extension class provides methods to:

- Check if a lock request has expired based on the requested duration (`HasExpired`)
- Calculate the remaining time before expiration (`RemainingTime`)
- Generate a detailed diagnostic report containing request information (`ToDiagnosticString`)
- Determine if a request was completed successfully within the requested duration (`IsSuccessfulWithinDuration`)
- Collect standard metrics for monitoring and alerting (`GetStandardMetrics`)


## XmlLockSerializerValidation

The `XmlLockSerializerValidation` class provides validation utilities for lock data structures that have been serialized or deserialized using XML. It ensures that lock instances maintain semantic invariants after XML serialization/deserialization, helping to detect corruption or data integrity issues in persisted lock state. The class offers three validation approaches: returning error lists for programmatic processing, boolean checks for quick validation, and exception-throwing validations for immediate failure handling.

### Usage Example

```csharp
using SarmKadan.DistributedLock.Formatters;
using SarmKadan.DistributedLock.Models;
using System;
using System.Xml.Serialization;

// Create a lock with proper values
var originalLock = new Lock
{
    Key = "user-session-lock-123",
    OwnerId = "auth-service-42",
    Status = LockStatus.Acquired,
    AcquiredAt = DateTime.UtcNow,
    ExpiresAt = DateTime.UtcNow.AddMinutes(5),
    Duration = TimeSpan.FromMinutes(5),
    RenewedAt = DateTime.UtcNow.AddMinutes(2),
    RenewalCount = 2,
    FencingToken = FencingToken.NewToken()
};

// Serialize to XML
var serializer = new XmlSerializer(typeof(Lock));
string xml;
using (var writer = new StringWriter())
{
    serializer.Serialize(writer, originalLock);
    xml = writer.ToString();
}

Console.WriteLine("Serialized lock XML:");
Console.WriteLine(xml);

// Deserialize from XML
Lock? deserializedLock;
using (var reader = new StringReader(xml))
{
    deserializedLock = (Lock?)serializer.Deserialize(reader);
}

// Validate the deserialized lock using the validation methods
var validationErrors = XmlLockSerializerValidation.Validate(deserializedLock);

if (validationErrors.Count > 0)
{
    Console.WriteLine("Validation errors found:");
    foreach (var error in validationErrors)
    {
        Console.WriteLine($"- {error}");
    }
}
else
{
    Console.WriteLine("Lock is valid after deserialization!");
}

// Use boolean validation for quick checks
bool isValid = XmlLockSerializerValidation.IsValid(deserializedLock);
Console.WriteLine($"Is valid: {isValid}");

// Use EnsureValid for immediate validation with detailed error messages
try
{
    XmlLockSerializerValidation.EnsureValid(deserializedLock);
    Console.WriteLine("Lock validated successfully!");
}
catch (ArgumentException ex)
{
    Console.WriteLine($"Validation failed: {ex.Message}");
}

// Validate the serializer instance itself (always valid as it's stateless)
var serializerErrors = XmlLockSerializerValidation.Validate(serializer);
Console.WriteLine($"Serializer validation errors: {serializerErrors.Count}");

// Validate a FencingToken after deserialization
if (deserializedLock?.FencingToken is not null)
{
    var tokenErrors = XmlLockSerializerValidation.Validate(deserializedLock.FencingToken);
    Console.WriteLine($"FencingToken validation errors: {tokenErrors.Count}");
}
```