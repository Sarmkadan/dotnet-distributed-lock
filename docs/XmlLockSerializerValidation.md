# XmlLockSerializerValidation

Static utility class that provides validation helpers for the XML lock serializer used in distributed locking scenarios. The class contains only static members and does not maintain state, making it safe to call from multiple threads concurrently.

## API

### Validate overloads
- **Validate(...) : IReadOnlyList<string>**  
  *Purpose:* Checks the supplied XML lock serializer configuration and returns any validation errors. An empty list indicates that the configuration is valid.  
  *Parameters:* Varies by overload (see source code for exact signatures).  
  *Return value:* A read‑only list of error messages; empty when validation passes.  
  *Throws:* `ArgumentNullException` if a required argument is `null`. Individual overloads may throw additional exceptions (e.g., `InvalidOperationException`) when the configuration cannot be processed.

- **Validate(...) : IReadOnlyList<string>**  
  *Purpose:* Same as above; different parameter set.  
  *Parameters:* Varies by overload.  
  *Return value:* Read‑only list of validation error messages.  
  *Throws:* `ArgumentNullException` for null arguments; other overload‑specific exceptions may apply.

- **Validate(...) : IReadOnlyList<string>**  
  *Purpose:* Same as above; different parameter set.  
  *Parameters:* Varies by overload.  
  *Return value:* Read‑only list of validation error messages.  
  *Throws:* `ArgumentNullException` for null arguments; other overload‑specific exceptions may apply.

- **Validate(...) : IReadOnlyList<string>**  
  *Purpose:* Same as above; different parameter set.  
  *Parameters:* Varies by overload.  
  *Return value:* Read‑only list of validation error messages.  
  *Throws:* `ArgumentNullException` for null arguments; other overload‑specific exceptions may apply.

### IsValid overloads
- **IsValid(...) : bool**  
  *Purpose:* Determines whether the supplied XML lock serializer configuration is valid without returning detailed error messages.  
  *Parameters:* Varies by overload.  
  *Return value:* `true` if the configuration passes validation; otherwise `false`.  
  *Throws:* `ArgumentNullException` if a required argument is `null`.

- **IsValid(...) : bool**  
  *Purpose:* Same as above; different parameter set.  
  *Parameters:* Varies by overload.  
  *Return value:* `true` if valid, `false` otherwise.  
  *Throws:* `ArgumentNullException` for null arguments.

- **IsValid(...) : bool**  
  *Purpose:* Same as above; different parameter set.  
  *Parameters:* Varies by overload.  
  *Return value:* `true` if valid, `false` otherwise.  
  *Throws:* `ArgumentNullException` for null arguments.

- **IsValid(...) : bool**  
  *Purpose:* Same as above; different parameter set.  
  *Parameters:* Varies by overload.  
  *Return value:* `true` if valid, `false` otherwise.  
  *Throws:* `ArgumentNullException` for null arguments.

### EnsureValid overloads
- **EnsureValid(...) : void**  
  *Purpose:* Validates the supplied XML lock serializer configuration and throws an exception if it is invalid. Useful for asserting correctness at runtime.  
  *Parameters:* Varies by overload.  
  *Return value:* None.  
  *Throws:* `ArgumentNullException` if a required argument is `null`; `InvalidOperationException` (or a derived type) containing the validation error messages when the configuration fails validation.

- **EnsureValid(...) : void**  
  *Purpose:* Same as above; different parameter set.  
  *Parameters:* Varies by overload.  
  *Return value:* None.  
  *Throws:* `ArgumentNullException` for null arguments; `InvalidOperationException` (or derived) with error details on failure.

- **EnsureValid(...) : void**  
  *Purpose:* Same as above; different parameter set.  
  *Parameters:* Varies by overload.  
  *Return value:* None.  
  *Throws:* `ArgumentNullException` for null arguments; `InvalidOperationException` (or derived) with error details on failure.

- **EnsureValid(...) : void**  
  *Purpose:* Same as above; different parameter set.  
  *Parameters:* Varies by overload.  
  *Return value:* None.  
  *Throws:* `ArgumentNullException` for null arguments; `InvalidOperationException` (or derived) with error details on failure.

## Usage

```csharp
using System.Collections.Generic;
using System.Xml.Serialization;
using DotNetDistributedLock.Xml;

// Example 1: Validate an XmlSerializer instance and act on the result.
XmlSerializer serializer = new XmlSerializer(typeof(MyLockData));
IReadOnlyList<string> errors = XmlLockSerializerValidation.Validate(serializer);
if (errors.Count > 0)
{
    // Log or handle validation problems.
    foreach (var err in errors)
    {
        Console.WriteLine($"Validation error: {err}");
    }
}
else
{
    // Proceed with using the serializer.
}
```

```csharp
using System.Xml;
using DotNetDistributedLock.Xml;

// Example 2: Ensure that an XmlReader configuration is valid; throws on failure.
using XmlReader reader = XmlReader.Create("lockConfig.xml");
XmlLockSerializerValidation.EnsureValid(reader);
// If no exception is thrown, the configuration is considered valid.
```

## Notes

- All members are **static** and the class contains no instance state; therefore the methods are thread‑safe and can be invoked concurrently from multiple threads without additional synchronization.
- Passing `null` for any argument that is required by a particular overload will result in an `ArgumentNullException`. Consult the specific overload signatures in the source code for details on which parameters may be nullable.
- The validation logic does not modify the supplied objects; it only inspects them for correctness.
- If `EnsureValid` throws an `InvalidOperationException`, the exception’s message typically aggregates the validation errors that would be returned by the corresponding `Validate` overload. This allows callers to choose between a boolean/check‑list approach (`Validate`/`IsValid`) and an assert‑style approach (`EnsureValid`).
