# CollectionExtensions

Provides a set of static extension methods that simplify common operations on collections, dictionaries, and sequences. The methods handle null‑safety, batching, merging, and querying without requiring explicit null checks or loops in calling code.

## API

### `public static bool IsNullOrEmpty<T>(this IEnumerable<T> source)`
- **Purpose**: Determines whether the supplied sequence is `null` or contains no elements.
- **Parameters**:
  - `source`: The sequence to test.
- **Return value**: `true` if `source` is `null` or empty; otherwise `false`.
- **Exceptions**: None.

### `public static bool HasElements<T>(this IEnumerable<T> source)`
- **Purpose**: Determines whether the supplied sequence is non‑null and contains at least one element.
- **Parameters**:
  - `source`: The sequence to test.
- **Return value**: `true` if `source` is not `null` and yields at least one element; otherwise `false`.
- **Exceptions**: None.

### `public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> source, int size)`
- **Purpose**: Splits the source sequence into consecutive batches of the specified size.
- **Parameters**:
  - `source`: The sequence to batch.
  - `size`: The maximum number of elements per batch; must be greater than zero.
- **Return value**: An `IEnumerable<IEnumerable<T>>` where each inner sequence represents a batch. The final batch may contain fewer than `size` elements if the source length is not a multiple of `size`.
- **Exceptions**:
  - `ArgumentNullException` if `source` is `null`.
  - `ArgumentOutOfRangeException` if `size` is less than or equal to zero.

### `public static TValue? GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)`
- **Purpose**: Retrieves the value associated with `key` from the dictionary, or returns the default value for `TValue` if the key is absent.
- **Parameters**:
  - `dictionary`: The dictionary to query.
  - `key`: The key whose value is to be obtained.
- **Return value**: The value associated with `key` if present; otherwise `default(TValue)`.
- **Exceptions**:
  - `ArgumentNullException` if `dictionary` is `null`.

### `public static void AddIfNotExists<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)`
- **Purpose**: Adds `key`/`value` to the dictionary only when the key does not already exist.
- **Parameters**:
  - `dictionary`: The dictionary to update.
  - `key`: The key to add.
  - `value`: The value to associate with `key`.
- **Return value**: None.
- **Exceptions**:
  - `ArgumentNullException` if `dictionary` is `null`.

### `public static Dictionary<TKey, TValue> Merge<TKey, TValue>(this IDictionary<TKey, TValue> first, IDictionary<TKey, TValue> second)`
- **Purpose**: Creates a new dictionary containing all entries from `first` and `second`. If a key exists in both dictionaries, the value from `second`second`second`` takes precedence.
- **Parameters**:
  - `first`: The primary dictionary.
  - `second`: The dictionary whose entries overwrite those from `first` on key conflicts.
- **Return value**: A new `Dictionary<TKey, TValue>` containing the merged contents.
- **Exceptions**:
  - `ArgumentNullException` if either `first` or `second` is `null`.

### `public static IEnumerable<T> ForEach<T>(this IEnumerable<T> source, Action<T> action)`
- **Purpose**: Executes `action` for each element in `source` and returns the original sequence to allow chaining.
- **Parameters**:
  - `source`: The sequence to iterate.
  - `action`: The delegate to invoke for each element.
- **Return value**: The original `source` sequence.
- **Exceptions**:
  - `ArgumentNullException` if `source` or `action` is `null`.

### `public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source)`
- **Purpose**: Creates a `HashSet<T>` containing the distinct elements of `source`.
- **Parameters**:
  - `source`: The sequence to convert.
- **Return value**: A new `HashSet<T>` with the elements from `source`.
- **Exceptions**:
  - `ArgumentNullException` if `source` is `null`.

### `public static (List<T> matching, List<T> nonMatching) Partition<T>(this IEnumerable<T> source, Func<T, bool> predicate)`
- **Purpose**: Splits the source sequence into two lists based on a predicate: one containing elements for which the predicate returns `true`, the other containing the remaining elements.
- **Parameters**:
  - `source`: The sequence to partition.
  - `predicate`: A function that returns `true` for elements that belong to the `matching` list.
- **Return value**: A value tuple where `matching` holds elements satisfying the predicate and `nonMatching` holds the rest.
- **Exceptions**:
  - `ArgumentNullException` if `source` or `predicate` is `null`.

### `public static T? MostFrequent<T>(this IEnumerable<T> source)`
- **Purpose**: Returns the element that occurs most frequently in `source`. If multiple elements share the highest frequency, the one encountered first is returned. Returns `default(T)` when `source` is empty or `null`.
- **Parameters**:
  - `source`: The sequence to analyze.
- **Return value**: The most frequent element, or `default(T)` if no elements exist.
- **Exceptions**: None.

### `public static T? SafeGetAt<T>(this IList<T> list, int index)`
- **Purpose**: Retrieves the element at `index` if the index is within the bounds of the list; otherwise returns `default(T)`.
- **Parameters**:
  - `list`: The list to query.
  - `index`: The zero‑based position to retrieve.
- **Return value**: The element at `index` if `0 ≤ index < list.Count`; otherwise `default(T)`.
- **Exceptions**:
  - `ArgumentNullException` if `list` is `null`.

## Usage

### Example 1: Batching and processing items
```csharp
var numbers = Enumerable.Range(1, 23);
foreach (var batch in numbers.Batch(5))
{
    var sum = batch.Sum();
    Console.WriteLine($"Batch sum: {sum}");
}
```
*Output*:
```
Batch sum: 15
Batch sum: 40
Batch sum: 65
Batch sum: 90
Batch sum: 23
```

### Example 2: Safe dictionary lookup and update
```csharp
var counts = new Dictionary<string, int>();
wordList.ForEach(w =>
{
    var current = counts.GetValueOrDefault(w, 0);
    counts[w] = current + 1;
});

// Ensure a default entry exists without overwriting
counts.AddIfNotExists("total", wordList.Count);
```

## Notes
- All extension methods are **pure** with respect to the static class; they do not retain any internal state. Thread safety therefore depends solely on the mutability of the arguments passed to them. If the source collection is not modified concurrently by other threads, the methods can be safely called from multiple threads.
- `IsNullOrEmpty` and `HasElements` accept `IEnumerable<T>`; enumerating the source may cause side effects if the underlying enumerator is not repeatable or is not thread‑safe.
- `Batch` buffers elements internally to form each batch; the enumeration is lazy, but each batch is materialized as it is yielded.
- `MostFrequent` requires the element type `T` to be usable as a key in a hash‑based lookup (i.e., it must correctly implement `GetHashCode` and `Equals`). For reference types, the default equality comparer is used.
- `SafeGetAt` returns `default(T)` for out‑of‑range indices rather than throwing; callers should verify the result against `default(T)` when `T` is a value type that could be a legitimate list element.
- The methods do not allocate unnecessary collections beyond what is required to fulfill their contract (e.g., `ToHashSet` creates a new `HashSet<T>`, `Partition` creates two `List<T>`).
