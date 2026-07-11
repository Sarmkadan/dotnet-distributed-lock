#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace SarmKadan.DistributedLock.Backends.Redis
{
    /// <summary>
    /// Provides validation methods for <see cref="RedisLockRepository"/> instances.
    /// </summary>
    public static class RedisLockRepositoryValidation
    {
        /// <summary>
        /// Validates the state of a <see cref="RedisLockRepository"/> instance.
        /// </summary>
        /// <remarks>
        /// Performs a lightweight sanity check by attempting a non-blocking existence check.
        /// The public API of <see cref="RedisLockRepository"/> does not expose internal state,
        /// so validation is limited to observable behavior through its public methods.
        /// </remarks>
        /// <param name="value">The repository instance to validate. Cannot be <see langword="null"/>.</param>
        /// <returns>
        /// A read-only list of human-readable problem descriptions.
        /// Empty if the instance is considered valid or if validation cannot be performed.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="value"/> is <see langword="null"/>.
        /// </exception>
        public static IReadOnlyList<string> Validate(this RedisLockRepository? value)
        {
            ArgumentNullException.ThrowIfNull(value);

            var problems = new List<string>();

            // Perform a lightweight sanity check to ensure the repository can respond to calls.
            // We perform this check without awaiting to keep the method synchronous.
            try
            {
                // Attempt a non-blocking existence check with a dummy key.
                // If the call throws, we capture the exception message as a problem.
                var task = value.ExistsAsync("validation_dummy_key");

                if (task.IsFaulted && task.Exception is not null)
                {
                    problems.Add($"RedisLockRepository threw an exception during a sanity check: {task.Exception.GetBaseException().Message}");
                }
            }
            catch (Exception ex) when (ex is not ArgumentNullException)
            {
                problems.Add($"RedisLockRepository threw an exception during a sanity check: {ex.Message}");
            }

            return problems.AsReadOnly();
        }

        /// <summary>
        /// Determines whether the repository instance passes validation.
        /// </summary>
        /// <param name="value">The repository instance to check.</param>
        /// <returns>
        /// <see langword="true"/> if no validation problems are found; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool IsValid(this RedisLockRepository? value) => value.Validate().Count == 0;

        /// <summary>
        /// Ensures that the repository instance is valid.
        /// </summary>
        /// <remarks>
        /// Throws an <see cref="ArgumentException"/> if validation problems are found.
        /// </remarks>
        /// <param name="value">The repository instance to validate.</param>
        /// <exception cref="ArgumentException">
        /// Thrown if validation problems are found.
        /// </exception>
        public static void EnsureValid(this RedisLockRepository? value)
        {
            var problems = value.Validate();
            if (problems.Count != 0)
            {
                var message = $"RedisLockRepository validation failed: {string.Join("; ", problems)}";
                throw new ArgumentException(message, nameof(value));
            }
        }
    }
}
