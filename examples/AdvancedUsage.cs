#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using SarmKadan.DistributedLock;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DistributedLock.Examples;

/// <summary>
/// Advanced usage example demonstrating custom configuration, auto-renewal, and error handling.
/// </summary>
public class AdvancedUsage
{
    public static async Task RunAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(configure => configure.AddConsole());

        // Configuration with advanced settings
        services.AddDistributedLocking(options =>
        {
            options.BackendType = BackendType.InMemory;
            options.EnableAutoRenewal = true;
            options.DefaultLockDuration = TimeSpan.FromSeconds(60);
            
            // Retry policy configuration
            options.RetryPolicyMaxRetries = 5;
            options.RetryPolicyInitialDelayMs = 200;
        });

        var serviceProvider = services.BuildServiceProvider();
        var lockService = serviceProvider.GetRequiredService<ILockService>();
        var monitor = serviceProvider.GetRequiredService<LockMonitor>();

        const string lockKey = "advanced-resource";
        const string ownerId = "advanced-worker";

        try
        {
            // Acquire with timeout handling
            var @lock = await lockService.AcquireAsync(lockKey, ownerId, TimeSpan.FromSeconds(10));
            
            // Register for auto-renewal
            monitor.RegisterLock(lockKey, ownerId, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60));
            
            Console.WriteLine("Lock acquired and auto-renewal registered.");
            
            // Perform long-running work
            await Task.Delay(TimeSpan.FromSeconds(90));
            
            await lockService.ReleaseAsync(lockKey, ownerId);
            Console.WriteLine("Work completed and lock released.");
        }
        catch (LockAcquisitionException ex)
        {
            Console.Error.WriteLine($"Acquisition error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
        }
        finally
        {
            // Always unregister monitor if it was registered
            monitor.UnregisterLock(lockKey, ownerId);
        }
    }
}
