#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using SarmKadan.DistributedLock;
using Microsoft.Extensions.DependencyInjection;

namespace DistributedLock.Examples;

/// <summary>
/// Basic usage example for acquiring and releasing a distributed lock.
/// </summary>
public class BasicUsage
{
    public static async Task RunAsync()
    {
        // 1. Dependency Injection setup
        var services = new ServiceCollection();
        services.AddDistributedLocking(options =>
        {
            options.BackendType = BackendType.InMemory;
            options.DefaultLockDuration = TimeSpan.FromSeconds(30);
        });

        var serviceProvider = services.BuildServiceProvider();
        var lockService = serviceProvider.GetRequiredService<ILockService>();

        const string lockKey = "critical-resource";
        const string ownerId = "worker-1";

        // 2. Acquisition and Release
        try
        {
            // Acquire lock (blocks until successful or timeout)
            var @lock = await lockService.AcquireAsync(lockKey, ownerId);
            
            Console.WriteLine($"Lock acquired: {@lock.Key}");
            
            // Perform work
            await Task.Delay(1000);
            
            // Release lock
            await lockService.ReleaseAsync(lockKey, ownerId);
            Console.WriteLine("Lock released successfully.");
        }
        catch (LockAcquisitionException ex)
        {
            Console.WriteLine($"Could not acquire lock: {ex.Message}");
        }
    }
}
