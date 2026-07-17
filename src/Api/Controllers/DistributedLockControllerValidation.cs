#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Api.Controllers;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Provides validation helpers for <see cref="DistributedLockController"/> instances.
/// Validates controller dependencies and state.
/// </summary>
public static class DistributedLockControllerValidation
{
    /// <summary>
    /// Validates the specified <see cref="DistributedLockController"/> instance and its dependencies.
    /// </summary>
    /// <param name="value">The controller instance to validate.</param>
    /// <returns>A list of validation problems; empty if the instance is valid.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    public static IReadOnlyList<string> Validate([NotNull] this DistributedLockController? value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = new List<string>();

        // Validate controller dependencies
        if (value._lockService is null)
        {
            problems.Add("LockService dependency is not initialized");
        }

        if (value._logger is null)
        {
            problems.Add("Logger dependency is not initialized");
        }

        return problems.AsReadOnly();
    }

    /// <summary>
    /// Determines whether the specified <see cref="DistributedLockController"/> instance is valid.
    /// </summary>
    /// <param name="value">The controller instance to check.</param>
    /// <returns><see langword="true"/> if the instance is valid; otherwise, <see langword="false"/>.</returns>
    public static bool IsValid([NotNullWhen(true)] this DistributedLockController? value)
    {
        return value?.Validate().Count == 0;
    }

    /// <summary>
    /// Ensures that the specified <see cref="DistributedLockController"/> instance is valid.
    /// </summary>
    /// <param name="value">The controller instance to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the controller has validation problems.</exception>
    public static void EnsureValid([NotNull] this DistributedLockController? value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var problems = value.Validate();
        if (problems.Count > 0)
        {
            throw new ArgumentException(
                $"DistributedLockController validation failed:{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", problems)}");
        }
    }
}