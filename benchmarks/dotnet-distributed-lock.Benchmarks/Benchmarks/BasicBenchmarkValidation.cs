using System.Globalization;
using SarmKadan.DistributedLock.Enums;

namespace SarmKadan.DistributedLock.Benchmarks.Benchmarks;

/// <summary>
/// Provides validation helpers for <see cref="BasicBenchmark"/> instances to ensure benchmark configuration is valid before execution.
/// </summary>
public static class BasicBenchmarkValidation
{
    /// <summary>
    /// Validates a <see cref="BasicBenchmark"/> instance and returns a list of human-readable problems.
    /// </summary>
    /// <param name="value">The benchmark instance to validate.</param>
    /// <returns>An immutable list of validation errors; empty if valid.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    public static IReadOnlyList<string> Validate(this BasicBenchmark value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var errors = new List<string>();

        // Validate BackendType
        if (value.BackendType == default)
        {
            errors.Add($"BackendType must be set to a valid value. Current value: {value.BackendType}");
        }
        else if (!Enum.IsDefined(typeof(BackendType), value.BackendType))
        {
            errors.Add($"BackendType '{value.BackendType}' is not a valid enum value.");
        }

        // Validate ConnectionString
        if (string.IsNullOrWhiteSpace(value.ConnectionString))
        {
            errors.Add("ConnectionString must be a non-empty, non-whitespace string.");
        }
        else if (value.ConnectionString.Length > 1024)
        {
            errors.Add($"ConnectionString exceeds maximum length of 1024 characters. Current length: {value.ConnectionString.Length}");
        }

        return errors.AsReadOnly();
    }

    /// <summary>
    /// Determines whether the specified <see cref="BasicBenchmark"/> instance is valid.
    /// </summary>
    /// <param name="value">The benchmark instance to check.</param>
    /// <returns><see langword="true"/> if valid; otherwise, <see langword="false"/>.</returns>
    public static bool IsValid(this BasicBenchmark value)
    {
        return value.Validate().Count == 0;
    }

    /// <summary>
    /// Ensures that the specified <see cref="BasicBenchmark"/> instance is valid, throwing an <see cref="ArgumentException"/> if not.
    /// </summary>
    /// <param name="value">The benchmark instance to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="value"/> is invalid, containing a list of validation errors.</exception>
    public static void EnsureValid(this BasicBenchmark value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var errors = value.Validate();

        if (errors.Count > 0)
        {
            throw new ArgumentException(
                $"BasicBenchmark is invalid. Validation failed with {errors.Count} error(s):{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", errors)}",
                nameof(value));
        }
    }
}
