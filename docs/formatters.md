# Formatters

The `SarmKadan.DistributedLock.Formatters` namespace (source in `src/Formatters/`) holds helpers that turn lock data into external formats: CSV export, JSON serialization, and XML serialization. They are meant for API responses, persistence, logs, and integration with other systems.

Important: `src/Formatters/**` is excluded from the root `SarmKadan.DistributedLock.csproj` (`<Compile Remove>`), so these types are not part of the library build or the NuGet package. To use them, copy the files into your own project or build them in a separate project.

## Status

| Type | Status |
|------|--------|
| `CsvLockExporter` | Placeholder. The class body is empty (`// ...`), so it has no members yet. |
| `JsonLockSerializer` | Static helper for lock and metrics JSON, plus a generic `JsonLockSerializer<T>` instance class. |
| `XmlLockSerializer` | Static helper for lock and metrics XML. |

Known issue: as committed, the method bodies in `JsonLockSerializer.cs` and `XmlLockSerializer.cs` are malformed (for example, `try` blocks are missing and some methods reference an undefined `lock` identifier), so these files do not compile in their current state. Because the folder is excluded from the build, the library itself still compiles. The sections below describe the intended behavior.

## CsvLockExporter

Purpose: export lock records to CSV (for spreadsheets, audits, or bulk reporting).

Current state: the class is an empty placeholder in the `SarmKadan.DistributedLock.Formatters` namespace. It uses `System.Text` and `Core.Models` but exposes no methods. Do not call it yet. When it is implemented, document its methods here.

## JsonLockSerializer

Purpose: convert `Lock` objects and `LockMetrics` to and from JSON for API responses, persistence, and transport.

### Formatting rules

- Property names are camelCase.
- Null properties are omitted when writing.
- Enums are written as camelCase strings.
- `DateTime` values are converted to UTC and written in ISO 8601 round-trip format (`"O"`), for example `2026-10-08T12:00:00.0000000Z`. This is done by `UtcDateTimeConverter`.
- Compact output by default. `SerializeLockPretty` produces indented output for debugging and logs.

### Static API

| Method | Input | Returns | Notes |
|--------|-------|---------|-------|
| `SerializeLock(Lock)` | a lock | JSON object string | Throws `ArgumentNullException` for null input. |
| `SerializeLocks(IEnumerable<Lock>)` | a collection | JSON array string | Materializes the collection with `ToList()`. |
| `DeserializeLock(string)` | JSON object | `Lock?` | Throws `ArgumentException` for null or empty input. |
| `DeserializeLocks(string)` | JSON array | `List<Lock>` | Returns an empty list when the result is null. |
| `SerializeMetrics(LockMetrics)` | metrics | JSON string | For reporting. |
| `SerializeLockPretty(Lock)` | a lock | indented JSON string | For debugging and logging. |

Serialization errors are wrapped in `InvalidOperationException` with a message that names the operation. The original `JsonException` is kept as the inner exception.

### Usage

```csharp
using SarmKadan.DistributedLock.Formatters;

// Single lock
string json = JsonLockSerializer.SerializeLock(myLock);
Lock? restored = JsonLockSerializer.DeserializeLock(json);

// Collection
string array = JsonLockSerializer.SerializeLocks(activeLocks);
List<Lock> locks = JsonLockSerializer.DeserializeLocks(array);

// Human-readable output for logs
string pretty = JsonLockSerializer.SerializeLockPretty(myLock);
```

### Generic JsonLockSerializer<T>

`JsonLockSerializer<T>` implements `ILockSerializer<T>` (`T` is a reference type). Use it when you need the same pattern for a type other than `Lock`. It is an instance class with `Serialize(T)` and `Deserialize(string)`.

- It uses camelCase and omits nulls, like the static helper.
- It does not register `UtcDateTimeConverter`, so `DateTime` values use the default `System.Text.Json` format.
- `Serialize(null)` returns `string.Empty`.
- `Deserialize` returns `null` for null, empty, or whitespace input.

```csharp
var serializer = new JsonLockSerializer<Lock>();
string text = serializer.Serialize(myLock);
Lock? copy = serializer.Deserialize(text);
```

## XmlLockSerializer

Purpose: convert locks and metrics to XML for enterprise and legacy integrations.

### Formatting rules

- Output is indented with two spaces and encoded as UTF-8, with an XML declaration.
- Elements use the default namespace `http://sarmkadan.com/distributedlock/2026`.
- `SerializeLock` and `DeserializeLock` use `XmlSerializer` on the `Lock` type.
- `SerializeLocks` wraps the lock elements in a `<Locks>` root with `Count` and `ExportTime` (UTC, ISO 8601) attributes.
- `ExportMetrics` writes a `<LockMetrics>` root with one `<Metric>` element per item, each with `LockId` and numeric or timestamp child elements.

### API

| Method | Input | Returns | Notes |
|--------|-------|---------|-------|
| `SerializeLock(Lock)` | a lock | XML string | Returns `string.Empty` for null input. |
| `SerializeLocks(IEnumerable<Lock>)` | a collection | XML string | Root `<Locks>` with `Count` and `ExportTime`. |
| `DeserializeLock(string)` | XML document | `Lock?` | Returns `null` for null, empty, or whitespace input. |
| `DeserializeLocks(string)` | XML with `<Lock>` elements | `List<Lock>` | Finds every `Lock` element by name, so it works with any root element. |
| `ExportMetrics(IEnumerable<LockMetrics>)` | metrics | XML string | Root `<LockMetrics>`. There is no `SerializeMetrics` in this class. |

Failures are wrapped in `InvalidOperationException`.

### Usage

```csharp
using SarmKadan.DistributedLock.Formatters;

// Single lock
string xml = XmlLockSerializer.SerializeLock(myLock);
Lock? restored = XmlLockSerializer.DeserializeLock(xml);

// Collection export
string export = XmlLockSerializer.SerializeLocks(activeLocks);
List<Lock> imported = XmlLockSerializer.DeserializeLocks(export);

// Metrics export
string metricsXml = XmlLockSerializer.ExportMetrics(metrics);
```

## Choosing a format

- JSON: APIs, storage, and most modern consumers. Use `JsonLockSerializer`.
- XML: systems that require XML or a fixed schema. Use `XmlLockSerializer`.
- CSV: planned. `CsvLockExporter` is not implemented yet.
