#nullable enable

namespace SarmKadan.DistributedLock.Enums;

/// <summary>
/// Extension methods for <see cref="BackendType"/>.
/// </summary>
public static class BackendTypeExtensions
{
    /// <summary>
    /// Returns a human-readable display name for the specified backend type.
    /// </summary>
    /// <param name="backendType">The backend type to get the display name for.</param>
    /// <returns>
    /// A user-facing name such as "Redis", "SQLite", "PostgreSQL" or "In-Memory".
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="backendType"/> is not a defined <see cref="BackendType"/> value.
    /// </exception>
    public static string ToDisplayName(this BackendType backendType) => backendType switch
    {
        BackendType.Redis => "Redis",
        BackendType.SQLite => "SQLite",
        BackendType.PostgreSQL => "PostgreSQL",
        BackendType.InMemory => "In-Memory",
        _ => throw new ArgumentOutOfRangeException(nameof(backendType), backendType, "Unknown backend type.")
    };
}
