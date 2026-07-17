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

		// Validate BackendType using pattern matching
		switch (value.BackendType)
		{
			case BackendType.Redis:
			case BackendType.SQLite:
			case BackendType.PostgreSQL:
			case BackendType.InMemory:
				break;

			default:
				errors.Add($"BackendType must be set to a valid value. Current value: {value.BackendType}");
				break;
		}

		// Validate ConnectionString based on BackendType
		if (string.IsNullOrWhiteSpace(value.ConnectionString))
		{
			errors.Add("ConnectionString must be a non-empty, non-whitespace string.");
		}
		else if (value.ConnectionString.Length > 1024)
		{
			errors.Add($"ConnectionString exceeds maximum length of 1024 characters. Current length: {value.ConnectionString.Length}");
		}
		else
		{
			// Backend-specific connection string validation
			ValidateBackendSpecificConnectionString(value.BackendType, value.ConnectionString, errors);
		}

		return errors.AsReadOnly();
	}

	private static void ValidateBackendSpecificConnectionString(BackendType backendType, string connectionString, List<string> errors)
	{
		// ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
		switch (backendType)
		{
			case BackendType.Redis:
				ValidateRedisConnectionString(connectionString, errors);
				break;

			case BackendType.SQLite:
				ValidateSQLiteConnectionString(connectionString, errors);
				break;

			case BackendType.PostgreSQL:
				ValidatePostgreSQLConnectionString(connectionString, errors);
				break;

			case BackendType.InMemory:
				// In-memory backend doesn't require a connection string
				if (!string.IsNullOrWhiteSpace(connectionString) && !connectionString.Equals("in-memory", StringComparison.OrdinalIgnoreCase))
				{
					errors.Add("InMemory backend should use an empty or 'in-memory' connection string.");
				}
				break;
		}
	}

	private static void ValidateRedisConnectionString(string connectionString, List<string> errors)
	{
		if (!connectionString.StartsWith("redis://", StringComparison.OrdinalIgnoreCase))
		{
			errors.Add("Redis backend requires a connection string starting with 'redis://'.");
		}
	}

	private static void ValidateSQLiteConnectionString(string connectionString, List<string> errors)
	{
		if (!connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) &&
			!connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase))
		{
			errors.Add("SQLite backend requires a connection string containing 'Data Source=' and typically '.db' file reference.");
		}
	}

	private static void ValidatePostgreSQLConnectionString(string connectionString, List<string> errors)
	{
		var requiredKeywords = new[] { "Host=", "Database=", "Username=", "Password=" };
		var missingKeywords = requiredKeywords.Where(keyword => !connectionString.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

		if (missingKeywords.Count > 0)
		{
			errors.Add($"PostgreSQL backend requires connection string with the following keywords: {string.Join(", ", missingKeywords)}.");
		}
	}

	/// <summary>
	/// Determines whether the specified <see cref="BasicBenchmark"/> instance is valid.
	/// </summary>
	/// <param name="value">The benchmark instance to check.</param>
	/// <returns><see langword="true"/> if valid; otherwise, <see langword="false"/>.</returns>
	public static bool IsValid(this BasicBenchmark value) => value.Validate().Count == 0;

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