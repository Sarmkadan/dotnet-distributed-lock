#nullable enable
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
        return collection is null || !collection.Any();
    }

    /// <summary>
    /// Checks if a collection has any elements.
    /// </summary>
    public static bool HasElements<T>(this IEnumerable<T>? collection)
    {
        return collection is not null && collection.Any();
    }

    /// <summary>
    /// Groups a collection into batches of specified size.
    /// Useful for batch processing of lock operations.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="batchSize"/> is less than or equal to zero</exception>
    public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> items, int batchSize)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than zero");

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
    /// <exception cref="ArgumentNullException"><paramref name="dictionary"/> is <see langword="null"/></exception>
    public static TValue? GetValueOrDefault<TKey, TValue>(
        this IDictionary<TKey, TValue>? dictionary,
        TKey? key,
        TValue? defaultValue = default) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(dictionary);

        if (key is null)
            return defaultValue;

        return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// Adds an item to a dictionary only if the key doesn't already exist.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="dictionary"/> is <see langword="null"/></exception>
    public static void AddIfNotExists<TKey, TValue>(
        this Dictionary<TKey, TValue> dictionary,
        TKey key,
        TValue value) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(dictionary);

        if (!dictionary.ContainsKey(key))
            dictionary[key] = value;
    }

    /// <summary>
    /// Merges multiple dictionaries into a new dictionary.
    /// Later dictionaries override earlier ones for duplicate keys.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="dictionaries"/> is <see langword="null"/></exception>
    public static Dictionary<TKey, TValue> Merge<TKey, TValue>(
        this IEnumerable<IDictionary<TKey, TValue>> dictionaries) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(dictionaries);

        var result = new Dictionary<TKey, TValue>();

        foreach (var dict in dictionaries)
        {
            if (dict is null)
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
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/></exception>
    public static IEnumerable<T> ForEach<T>(this IEnumerable<T> items, Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(action);

        foreach (var item in items)
        {
            action(item);
            yield return item;
        }
    }

    /// <summary>
    /// Converts a collection to a HashSet for efficient lookups.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/></exception>
    public static HashSet<T> ToHashSet<T>(this IEnumerable<T>? items, IEqualityComparer<T>? comparer = null)
    {
        if (items is null)
            return new HashSet<T>(comparer);

        return comparer is not null
            ? new HashSet<T>(items, comparer)
            : new HashSet<T>(items);
    }

    /// <summary>
    /// Partitions a collection into two based on a predicate.
    /// Returns (matching, nonMatching) tuples.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/></exception>
    public static (List<T> matching, List<T> nonMatching) Partition<T>(
        this IEnumerable<T> items,
        Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(predicate);

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
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/></exception>
    public static T? MostFrequent<T>(this IEnumerable<T> items) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(items);

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
    /// <exception cref="ArgumentNullException"><paramref name="list"/> is <see langword="null"/></exception>
    public static T? SafeGetAt<T>(this IList<T>? list, int index)
    {
        if (list is null || index < 0 || index >= list.Count)
            return default;

        return list[index];
    }
}
