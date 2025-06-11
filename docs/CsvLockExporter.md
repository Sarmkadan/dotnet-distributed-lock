# CsvLockExporter

The `CsvLockExporter` class provides utilities for exporting distributed lock data to CSV format, enabling analysis, monitoring, and integration with external systems. It supports exporting individual locks, multiple locks, and metrics, with configurable formatting options such as delimiters, headers, metadata, and encoding.

## API

### `public static string ExportLock`

Exports a single lock's data to a CSV-formatted string.

- **Parameters**:
  - `lock`: The `IDistributedLock` instance to export.
- **Returns**: A CSV-formatted string representing the lock's data.
- **Throws**: `ArgumentNullException` if `lock` is `null`.

### `public static string ExportLocks`

Exports a collection of locks to a single CSV-formatted string.

- **Parameters**:
  - `locks`: An `IEnumerable<IDistributedLock>` containing locks to export.
- **Returns**: A CSV-formatted string representing all locks.
- **Throws**: `ArgumentNullException` if `locks` is `null`.

### `public static async Task ExportLocksToStreamAsync`

Asynchronously exports a collection of locks to a writable stream in CSV format.

- **Parameters**:
  - `locks`: An `IEnumerable<IDistributedLock>` containing locks to export.
  - `stream`: The `Stream` to write the CSV data to.
  - `cancellationToken`: Optional `CancellationToken` to observe.
- **Returns**: A `Task` representing the asynchronous operation.
- **Throws**:
  - `ArgumentNullException` if `locks` or `stream` is `null`.
  - `ArgumentException` if `stream` is not writable.
  - `OperationCanceledException` if `cancellationToken` is canceled.

### `public static string ExportMetrics`

Exports aggregated metrics of locks to a CSV-formatted string.

- **Parameters**:
  - `locks`: An `IEnumerable<IDistributedLock>` containing locks to analyze.
- **Returns**: A CSV-formatted string representing metrics such as count, duration, and status.
- **Throws**: `ArgumentNullException` if `locks` is `null`.

### `public bool IncludeHeader`

Gets or sets whether the exported CSV includes a header row.

- **Default**: `true`
- **Type**: `bool`

### `public char Delimiter`

Gets or sets the character used to separate fields in the CSV output.

- **Default**: `,`
- **Type**: `char`

### `public bool IncludeMetadata`

Gets or sets whether to include additional metadata fields (e.g., timestamps, resource identifiers) in the CSV output.

- **Default**: `false`
- **Type**: `bool`

### `public Encoding Encoding`

Gets or sets the text encoding used for the CSV output.

- **Default**: `Encoding.UTF8`
- **Type**: `Encoding`

## Usage

### Example 1: Exporting a single lock to a string
