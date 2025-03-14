// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Utilities.Extensions;

/// <summary>
/// Extension methods for collections and enumerables.
/// Provides utilities for batch processing, grouping, and safe access patterns.
/// </summary>
public static class CollectionExtensions
{
    /// <summary>
    /// Checks if a collection is null or empty.
    /// </summary>
    public static bool IsNullOrEmpty<T>(this IEnumerable<T>? collection)
    {
        return collection == null || !collection.Any();
    }

    /// <summary>
    /// Checks if a collection has any elements.
    /// </summary>
    public static bool HasElements<T>(this IEnumerable<T>? collection)
    {
        return collection != null && collection.Any();
    }

    /// <summary>
    /// Groups a collection into batches of specified size.
    /// Useful for batch processing of lock operations.
    /// </summary>
    public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> items, int batchSize)
    {
        if (batchSize <= 0)
            throw new ArgumentException("Batch size must be greater than zero", nameof(batchSize));

        var batch = new List<T>(batchSize);

        foreach (var item in items)
        {
            batch.Add(item);

            if (batch.Count >= batchSize)
            {
                yield return batch;
                batch = new List<T>(batchSize);
            }
        }

        if (batch.Count > 0)
            yield return batch;
    }

    /// <summary>
    /// Gets a value from a dictionary with a default fallback.
    /// Null-safe alternative to TryGetValue.
    /// </summary>
    public static TValue? GetValueOrDefault<TKey, TValue>(
        this IDictionary<TKey, TValue>? dictionary,
        TKey? key,
        TValue? defaultValue = default) where TKey : notnull
    {
        if (dictionary == null || key == null)
            return defaultValue;

        return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// Adds an item to a dictionary only if the key doesn't already exist.
    /// </summary>
    public static void AddIfNotExists<TKey, TValue>(
        this Dictionary<TKey, TValue> dictionary,
        TKey key,
        TValue value) where TKey : notnull
    {
        if (!dictionary.ContainsKey(key))
            dictionary[key] = value;
    }

    /// <summary>
    /// Merges multiple dictionaries into a new dictionary.
    /// Later dictionaries override earlier ones for duplicate keys.
    /// </summary>
    public static Dictionary<TKey, TValue> Merge<TKey, TValue>(
        this IEnumerable<IDictionary<TKey, TValue>> dictionaries) where TKey : notnull
    {
        var result = new Dictionary<TKey, TValue>();

        foreach (var dict in dictionaries)
        {
            if (dict == null)
                continue;

            foreach (var kvp in dict)
                result[kvp.Key] = kvp.Value;
        }

        return result;
    }

    /// <summary>
    /// Performs an action on each element in a collection.
    /// Similar to foreach but chainable.
    /// </summary>
    public static IEnumerable<T> ForEach<T>(this IEnumerable<T> items, Action<T> action)
    {
        foreach (var item in items)
        {
            action(item);
            yield return item;
        }
    }

    /// <summary>
    /// Converts a collection to a HashSet for efficient lookups.
    /// </summary>
    public static HashSet<T> ToHashSet<T>(this IEnumerable<T>? items, IEqualityComparer<T>? comparer = null)
    {
        if (items == null)
            return new HashSet<T>(comparer);

        return comparer != null
            ? new HashSet<T>(items, comparer)
            : new HashSet<T>(items);
    }

    /// <summary>
    /// Partitions a collection into two based on a predicate.
    /// Returns (matching, nonMatching) tuples.
    /// </summary>
    public static (List<T> matching, List<T> nonMatching) Partition<T>(
        this IEnumerable<T> items,
        Func<T, bool> predicate)
    {
        var matching = new List<T>();
        var nonMatching = new List<T>();

        foreach (var item in items)
        {
            if (predicate(item))
                matching.Add(item);
            else
                nonMatching.Add(item);
        }

        return (matching, nonMatching);
    }

    /// <summary>
    /// Gets the most frequent item in a collection.
    /// Returns null if collection is empty.
    /// </summary>
    public static T? MostFrequent<T>(this IEnumerable<T> items) where T : notnull
    {
        return items
            .GroupBy(x => x)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();
    }

    /// <summary>
    /// Safely gets an item at index with bounds checking.
    /// Returns null if index is out of bounds.
    /// </summary>
    public static T? SafeGetAt<T>(this IList<T>? list, int index) where T : class
    {
        if (list == null || index < 0 || index >= list.Count)
            return null;

        return list[index];
    }
}
