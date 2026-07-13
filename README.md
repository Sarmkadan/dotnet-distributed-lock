// existing content ...

## LockRenewalFailedExceptionJsonExtensions

The `LockRenewalFailedExceptionJsonExtensions` class provides utility methods to serialize and deserialize `LockRenewalFailedException` instances to and from JSON. This is useful for logging or storing exception details in a database.

### Usage Example

```csharp
var json = LockRenewalFailedException.ToJson(
    new LockRenewalFailedException("Lock renewal failed")
);
Console.WriteLine(json);

var exception = LockRenewalFailedException.FromJson(json);
if (exception != null)
{
    Console.WriteLine(exception.Message);
}

if (LockRenewalFailedException.TryFromJson(json, out var deserializedException))
{
    Console.WriteLine(deserializedException.Message);
}

// Using JsonSerializer with converters
var options = new JsonSerializerOptions();
options.Converters.Add(new LockRenewalFailedExceptionJsonConverter());

var json2 = JsonSerializer.Serialize(
    new LockRenewalFailedException("Test"),
    options
);

var exception2 = JsonSerializer.Deserialize<LockRenewalFailedException>(json2, options);
if (exception2 != null)
{
    Console.WriteLine(exception2.Message);
}
```

// existing content ...
