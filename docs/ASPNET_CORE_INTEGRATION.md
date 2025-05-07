# ASP.NET Core Hosted Services Integration

This guide provides instructions and examples for integrating the distributed locking library with ASP.NET Core `IHostedService` and `BackgroundService`. This is a common pattern for running long-running background tasks in ASP.NET Core applications that might require distributed coordination.

## Key Considerations

When using distributed locks with hosted services, keep the following in mind:

1.  **Dependency Injection (DI)**: The `ILockService` and other components should be registered with the ASP.NET Core DI container.
2.  **Graceful Shutdown**: Handle application shutdown (`IHostApplicationLifetime.ApplicationStopping`) to ensure locks are released promptly and cleanly. `OperationCanceledException` is your friend here.
3.  **Lock Scope and Lifetime**: Carefully manage when locks are acquired and released. For recurring tasks, acquire the lock for each iteration of the task.
4.  **Error Handling and Retries**: Implement robust error handling and retry policies for lock acquisition and renewal, especially in scenarios with high contention or transient network issues.
5.  **Auto-Renewal**: For long-running tasks, enable auto-renewal to automatically extend the lock's lifetime as long as the task is active.

## Example: Critical Background Task

The following example demonstrates a `BackgroundService` that acquires a distributed lock before performing a critical operation. It includes error handling, cancellation token management, and graceful shutdown considerations.

To run this example:
1.  Ensure you have the distributed lock library configured in your ASP.NET Core application.
2.  Add the `CriticalBackgroundTask` to your `Startup.cs` or `Program.cs`.

```csharp
#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock.Core.Configuration;
using SarmKadan.DistributedLock.Core.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SarmKadan.DistributedLock.Examples;

/// <summary>
/// Demonstrates how to use the distributed lock library within an ASP.NET Core
/// IHostedService / BackgroundService to protect critical background tasks.
/// </summary>
public class HostedServiceExample
{
    public static async Task Run(string[] args)
    {
        var builder = Host.CreateDefaultBuilder(args);

        builder.ConfigureServices((hostContext, services) =>
        {
            // 1. Configure and register the distributed lock service
            // This example uses an in-memory backend for simplicity, but in a real scenario
            // you would configure Redis, PostgreSQL, or SQLite.
            services.AddDistributedLock(options =>
            {
                options.BackendType = Core.Enums.BackendType.InMemory;
                options.DefaultLockDuration = TimeSpan.FromSeconds(30);
                options.DefaultAcquisitionTimeout = TimeSpan.FromSeconds(5);
                options.EnableAutoRenewal = true;
                options.UseFencingTokens = true;
            });

            // 2. Register the custom BackgroundService
            services.AddHostedService<CriticalBackgroundTask>();
        });

        var host = builder.Build();
        await host.RunAsync();
    }
}

/// <summary>
/// A background service that performs a critical operation requiring a distributed lock.
/// This service demonstrates safe lock acquisition, renewal, and release in an async context.
/// </summary>
public class CriticalBackgroundTask : BackgroundService
{
    private readonly ILogger<CriticalBackgroundTask> _logger;
    private readonly ILockService _lockService;
    private readonly IHostApplicationLifetime _appLifetime;

    public CriticalBackgroundTask(
        ILogger<CriticalBackgroundTask> logger,
        ILockService lockService,
        IHostApplicationLifetime appLifetime)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));
        _appLifetime = appLifetime ?? throw new ArgumentNullException(nameof(appLifetime));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CriticalBackgroundTask is starting.");

        // Register a callback for when the application is stopping.
        // This is crucial for handling graceful shutdown while holding a lock.
        _appLifetime.ApplicationStopping.Register(() =>
        {
            _logger.LogInformation("Application stopping, requesting task cancellation.");
        });

        // Simulate some startup delay
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Attempting to acquire distributed lock for critical operation...");

                // Attempt to acquire a distributed lock for a critical section
                // We use TryAcquireAsync with a short timeout.
                var (success, @lock, errorMessage) = await _lockService.TryAcquireAsync(
                    "critical-operation-lock",
                    "CriticalBackgroundTaskWorker",
                    TimeSpan.FromSeconds(20), // Lock duration
                    stoppingToken: stoppingToken);

                if (success && @lock is not null)
                {
                    _logger.LogInformation("Distributed lock acquired: {LockKey}. Starting critical operation...", @lock.Key);

                    try
                    {
                        // Simulate a critical operation
                        await PerformCriticalOperationAsync(stoppingToken);
                    }
                    finally
                    {
                        // Ensure the lock is released when the critical operation is done or fails.
                        // This uses the owner ID to ensure only this worker can release its lock.
                        _logger.LogInformation("Releasing distributed lock: {LockKey}...", @lock.Key);
                        var released = await _lockService.ReleaseAsync(@lock.Key, @lock.OwnerId, stoppingToken);
                        if (released)
                        {
                            _logger.LogInformation("Distributed lock {LockKey} released successfully.", @lock.Key);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to release distributed lock {LockKey}.", @lock.Key);
                            // Depending on the scenario, you might want to log this as an error
                            // or trigger an alert.
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to acquire distributed lock: {ErrorMessage}. Retrying soon...", errorMessage);
                    // Implement back-off or retry logic here
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("CriticalBackgroundTask is stopping due to cancellation request.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CriticalBackgroundTask encountered an unhandled exception.");
                // Depending on the error, you might want to stop the application or implement
                // more robust error handling and retry mechanisms.
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); // Prevent busy loop on continuous errors
            }

            // After a successful operation or a failed acquisition, wait before next attempt.
            if (!stoppingToken.IsCancellationRequested && success)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        _logger.LogInformation("CriticalBackgroundTask has stopped.");
    }

    private async Task PerformCriticalOperationAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Performing critical operation...");
        // Simulate work that takes time and might be interrupted
        for (int i = 0; i < 5; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("Critical operation step {Step}", i + 1);
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }
        _logger.LogInformation("Critical operation completed.");
    }
}
```