#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using Microsoft.Extensions.DependencyInjection;
using SarmKadan.DistributedLock.Services;

namespace SarmKadan.DistributedLock.Configuration;

/// <summary>
/// Extension methods for registering lock contention tracking and deadlock detection into the
/// dependency injection container.
/// </summary>
public static class ContentionMetricsExtensions
{
    /// <summary>
    /// Registers <see cref="IDeadlockDetector"/> as a singleton service.
    /// </summary>
    /// <remarks>
    /// The detector is registered as a singleton because it must maintain a shared, process-wide
    /// wait-for graph. All callers that interact with the same lock keys must observe the same
    /// ownership and waiter state for deadlock detection to be accurate.
    ///
    /// Typical usage alongside the core locking setup:
    /// <code>
    /// services.AddDistributedLocking(opts => { ... })
    ///         .AddLockContentionTracking();
    /// </code>
    /// </remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, enabling method chaining.</returns>
    public static IServiceCollection AddLockContentionTracking(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IDeadlockDetector, DeadlockDetector>();

        return services;
    }
}
