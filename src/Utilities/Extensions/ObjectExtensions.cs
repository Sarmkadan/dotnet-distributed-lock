#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Utilities.Extensions;

using System.Text.Json;

/// <summary>
/// Extension methods for general object operations.
/// Provides utilities for serialization, cloning, and type conversions.
/// </summary>
public static class ObjectExtensions
{
    /// <summary>
    /// Deep clones an object using JSON serialization.
    /// Works with any serializable object, avoiding shallow copy issues.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when JSON serialization or deserialization fails.</exception>
    public static T? DeepClone<T>(this T? obj) where T : class
    {
        if (obj is null)
            return null;

        try
        {
            var json = JsonSerializer.Serialize(obj);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to deep clone object. Ensure the object is JSON serializable.", ex);
        }
    }

    /// <summary>
    /// Serializes an object to JSON string with pretty printing.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the input object is null.</exception>
    public static string ToJsonString<T>(this T obj) where T : class
    {
        ArgumentNullException.ThrowIfNull(obj);

        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            return JsonSerializer.Serialize(obj, options);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to serialize object to JSON string.", ex);
        }
    }

    /// <summary>
    /// Serializes an object to JSON string (compact format).
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the input object is null.</exception>
    public static string ToCompactJsonString<T>(this T obj) where T : class
    {
        ArgumentNullException.ThrowIfNull(obj);

        try
        {
            return JsonSerializer.Serialize(obj);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to serialize object to compact JSON string.", ex);
        }
    }

    /// <summary>
    /// Determines if an object is null or its default value.
    /// </summary>
    public static bool IsNullOrDefault<T>(this T? obj) where T : class
    {
        return obj is null || obj.Equals(default(T));
    }

    /// <summary>
    /// Attempts to cast an object to a target type safely.
    /// Returns true if cast succeeds, false otherwise.
    /// </summary>
    /// <param name="obj">The object to cast.</param>
    /// <param name="result">The cast result, or null if cast fails.</param>
    /// <returns>True if cast succeeds, false otherwise.</returns>
    public static bool TryCast<T>(this object obj, out T? result) where T : class
    {
        result = null;
        if (obj is T casted)
        {
            result = casted;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Creates a hashcode from an object based on specific property values.
    /// Useful for distributed lock identifiers.
    /// </summary>
    /// <param name="obj">The object to compute hash for.</param>
    /// <param name="propertyValues">Property values to include in hash computation.</param>
    /// <returns>The computed hash code.</returns>
    public static int ComputeHash<T>(this T obj, params object?[] propertyValues) where T : class
    {
        ArgumentNullException.ThrowIfNull(obj);
        ArgumentNullException.ThrowIfNull(propertyValues);

        var hash = 17;
        foreach (var value in propertyValues)
        {
            hash = hash * 31 + (value?.GetHashCode() ?? 0);
        }

        return hash;
    }

    /// <summary>
    /// Executes an action on the object and returns the object for chaining.
    /// Allows fluent API style operations.
    /// </summary>
    /// <param name="obj">The object to operate on.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>The original object for method chaining.</returns>
    public static T Tap<T>(this T obj, Action<T> action) where T : class
    {
        ArgumentNullException.ThrowIfNull(obj);
        action?.Invoke(obj);
        return obj;
    }

    /// <summary>
    /// Converts an object to another type using transformation function.
    /// Returns default value if transformation fails.
    /// </summary>
    /// <param name="obj">The object to transform.</param>
    /// <param name="mapper">The transformation function.</param>
    /// <returns>The transformed result, or null if transformation fails.</returns>
    public static TResult? MapTo<T, TResult>(this T obj, Func<T, TResult?> mapper) where T : class where TResult : class
    {
        ArgumentNullException.ThrowIfNull(obj);
        ArgumentNullException.ThrowIfNull(mapper);

        try
        {
            return mapper(obj);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets a string representation of an object's type name.
    /// Strips namespaces and generic parameters for brevity.
    /// </summary>
    /// <returns>The simplified type name.</returns>
    public static string GetSimpleTypeName<T>(this T obj) where T : class
    {
        var typeName = typeof(T).Name;
        var index = typeName.IndexOf('`');
        return index > 0 ? typeName.Substring(0, index) : typeName;
    }

    /// <summary>
    /// Validates an object against a predicate and returns the result.
    /// </summary>
    /// <param name="obj">The object to validate.</param>
    /// <param name="predicate">The validation predicate.</param>
    /// <returns>True if object is not null and predicate returns true, false otherwise.</returns>
    public static bool Validate<T>(this T obj, Func<T, bool> predicate) where T : class
    {
        ArgumentNullException.ThrowIfNull(obj);
        ArgumentNullException.ThrowIfNull(predicate);

        return predicate(obj);
    }
}