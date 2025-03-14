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
    /// Note: This method requires objects to be JSON serializable.
    /// </summary>
    public static T? DeepClone<T>(this T? obj) where T : class
    {
        if (obj == null)
            return null;

        try
        {
            var json = JsonSerializer.Serialize(obj);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Serializes an object to JSON string with pretty printing.
    /// </summary>
    public static string ToJsonString<T>(this T obj) where T : class
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            return JsonSerializer.Serialize(obj, options);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Serializes an object to JSON string (compact format).
    /// </summary>
    public static string ToCompactJsonString<T>(this T obj) where T : class
    {
        try
        {
            return JsonSerializer.Serialize(obj);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Determines if an object is null or its default value.
    /// </summary>
    public static bool IsNullOrDefault<T>(this T? obj) where T : class
    {
        return obj == null || obj.Equals(default(T));
    }

    /// <summary>
    /// Attempts to cast an object to a target type safely.
    /// Returns true if cast succeeds, false otherwise.
    /// </summary>
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
    public static int ComputeHash<T>(this T obj, params object?[] propertyValues) where T : class
    {
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
    public static T Tap<T>(this T obj, Action<T> action) where T : class
    {
        action?.Invoke(obj);
        return obj;
    }

    /// <summary>
    /// Converts an object to another type using transformation function.
    /// Returns default value if transformation fails.
    /// </summary>
    public static TResult? MapTo<T, TResult>(this T obj, Func<T, TResult?> mapper) where T : class where TResult : class
    {
        if (obj == null)
            return null;

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
    public static string GetSimpleTypeName<T>(this T obj) where T : class
    {
        var typeName = typeof(T).Name;
        var index = typeName.IndexOf('`');
        return index > 0 ? typeName.Substring(0, index) : typeName;
    }

    /// <summary>
    /// Validates an object against a predicate and returns the result.
    /// </summary>
    public static bool Validate<T>(this T obj, Func<T, bool> predicate) where T : class
    {
        return obj != null && predicate(obj);
    }
}
