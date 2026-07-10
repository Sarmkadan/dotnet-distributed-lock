#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace SarmKadan.DistributedLock.Backends.Redis
{
    /// <summary>
    /// Validation helpers for <see cref="RedisLockRepository"/>.
    /// </summary>
    public static class RedisLockRepositoryValidation
    {
        /// <summary>
        /// Validates the state of a <see cref="RedisLockRepository"/> instance.
        /// Returns a read‑only list of human‑readable problem descriptions.
        /// </summary>
        /// <param name="value">The repository instance to validate.</param>
        /// <returns>A list of validation problems; empty if the instance is considered valid.</returns>
        public static IReadOnlyList<string> Validate(this RedisLockRepository? value)
        {
            var problems = new List<string>();

            if (value is null)
            {
                problems.Add("RedisLockRepository instance is null.");
                // No further checks can be performed if the instance itself is null.
                return problems;
            }

            // The public API does not expose internal state (connection string, logger, etc.).
            // Therefore we can only perform superficial checks based on the observable behavior.
            // A simple sanity check is to ensure that the repository can respond to a lightweight call.
            // We perform this check without awaiting to keep the method synchronous.
            try
            {
                // Attempt a non‑blocking existence check with a dummy key.
                // If the call throws, we capture the exception message as a problem.
                var task = value.ExistsAsync("validation_dummy_key");
                if (!task.IsCompleted)
                {
                    // If the task hasn't completed synchronously, we consider it acceptable.
                    // No additional problem is added.
                }
                else if (task.IsFaulted && task.Exception is not null)
                {
                    problems.Add($"RedisLockRepository threw an exception during a sanity check: {task.Exception.GetBaseException().Message}");
                }
            }
            catch (Exception ex)
            {
                problems.Add($"RedisLockRepository threw an exception during a sanity check: {ex.Message}");
            }

            return problems;
        }

        /// <summary>
        /// Determines whether the repository instance passes validation.
        /// </summary>
        /// <param name="value">The repository instance to check.</param>
        /// <returns><c>true</c> if no validation problems are found; otherwise, <c>false</c>.</returns>
        public static bool IsValid(this RedisLockRepository? value) => !value.Validate().Any();

        /// <summary>
        /// Ensures that the repository instance is valid.
        /// Throws an <see cref="ArgumentException"/> if validation problems are found.
        /// </summary>
        /// <param name="value">The repository instance to validate.</param>
        public static void EnsureValid(this RedisLockRepository? value)
        {
            var problems = value.Validate();
            if (problems.Any())
            {
                var message = $"RedisLockRepository validation failed: {string.Join("; ", problems)}";
                throw new ArgumentException(message, nameof(value));
            }
        }
    }
}
