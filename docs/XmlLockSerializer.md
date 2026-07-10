# XmlLockSerializer

The `XmlLockSerializer` class provides a static utility interface for serializing and deserializing distributed lock objects to and from XML string representations. It facilitates the persistence and transmission of lock state within the `dotnet-distributed-lock` ecosystem, supporting both single lock instances and collections of locks, while also offering a mechanism to export internal operational metrics in XML format.

## API

### `SerializeLock`
```csharp
public static string SerializeLock(Lock lockObject)
```
Converts a single `Lock` instance into its XML string representation.
*   **Parameters**: `lockObject` – The `Lock` instance to serialize.
*   **Returns**: A `string` containing the XML data.
*   **Throws**: Throws an exception if `lockObject` is null or if the object contains invalid data that cannot be represented in XML.

### `SerializeLocks`
```csharp
public static string SerializeLocks(List<Lock> locks)
```
Converts a list of `Lock` instances into a single XML string representation.
*   **Parameters**: `locks` – The `List<Lock>` to serialize.
*   **Returns**: A `string` containing the XML data representing the collection.
*   **Throws**: Throws an exception if the list is null or if any element within the list is invalid for serialization.

### `DeserializeLock`
```csharp
public static Lock? DeserializeLock(string xml)
```
Parses an XML string to reconstruct a single `Lock` object.
*   **Parameters**: `xml` – The XML string to parse.
*   **Returns**: A `Lock` object if parsing is successful; `null` if the input is empty or represents a null value.
*   **Throws**: Throws an exception if the XML format is malformed or does not match the expected schema for a `Lock`.

### `DeserializeLocks`
```csharp
public static List<Lock> DeserializeLocks(string xml)
```
Parses an XML string to reconstruct a list of `Lock` objects.
*   **Parameters**: `xml` – The XML string containing the collection data.
*   **Returns**: A `List<Lock>` containing the deserialized objects. Returns an empty list if the XML represents an empty collection.
*   **Throws**: Throws an exception if the XML format is malformed or contains elements that cannot be mapped to `Lock` objects.

### `ExportMetrics`
```csharp
public static string ExportMetrics()
```
Generates an XML string containing current serialization performance metrics or statistical data.
*   **Parameters**: None.
*   **Returns**: A `string` containing the metrics in XML format.
*   **Throws**: Unlikely to throw under normal conditions unless internal metric counters are in an invalid state.

## Usage

### Serializing and Deserializing a Single Lock
The following example demonstrates creating a lock, serializing it to an XML string for storage or transmission, and subsequently deserializing it back into an object.

```csharp
using DistributedLock;

// Create a new lock instance
var myLock = new Lock("resource-id-123", TimeSpan.FromSeconds(30));

// Serialize the lock to XML
string xmlData = XmlLockSerializer.SerializeLock(myLock);

// Simulate retrieval from storage
string retrievedXml = xmlData; 

// Deserialize back to a Lock object
Lock? restoredLock = XmlLockSerializer.DeserializeLock(retrievedXml);

if (restoredLock != null)
{
    Console.WriteLine($"Restored lock for resource: {restoredLock.ResourceId}");
}
```

### Handling Collections and Metrics
This example illustrates batch processing of multiple locks and exporting system metrics for monitoring purposes.

```csharp
using System.Collections.Generic;
using DistributedLock;

// Prepare a collection of locks
var locks = new List<Lock>
{
    new Lock("resource-a", TimeSpan.FromMinutes(1)),
    new Lock("resource-b", TimeSpan.FromMinutes(2))
};

// Serialize the entire collection
string batchXml = XmlLockSerializer.SerializeLocks(locks);

// Deserialize the collection
List<Lock> restoredLocks = XmlLockSerializer.DeserializeLocks(batchXml);

// Export current serializer metrics
string metricsXml = XmlLockSerializer.ExportMetrics();
System.IO.File.WriteAllText("serializer_metrics.xml", metricsXml);
```

## Notes

*   **Null Handling**: The `DeserializeLock` method explicitly returns `null` for empty or null-equivalent XML inputs, whereas `DeserializeLocks` returns an empty `List<Lock>` rather than null for empty collections. Callers should handle these return values accordingly to avoid `NullReferenceException`.
*   **Thread Safety**: As `XmlLockSerializer` exposes only static methods and relies on immutable string inputs/outputs, it is inherently thread-safe for concurrent read/write operations provided the underlying `Lock` objects passed in are not modified concurrently during the serialization call.
*   **XML Schema Compatibility**: The serializer expects XML strictly conforming to the internal schema defined by the `Lock` type. Passing XML generated from external sources or modified manually may result in runtime exceptions during deserialization.
*   **Exception Propagation**: All methods propagate underlying XML parsing or formatting exceptions directly. Implementations calling these methods should wrap calls in try-catch blocks to handle potential format errors gracefully.
