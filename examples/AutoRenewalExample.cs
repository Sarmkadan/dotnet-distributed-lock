#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using SarmKadan.DistributedLock;
using Microsoft.Extensions.DependencyInjection;

namespace DistributedLock.Examples;

/// <summary>
/// Example demonstrating automatic lock renewal for long-running operations.
/// The lock is automatically extended before expiration, allowing operations
/// to continue without manual renewal.
/// </summary>
public class AutoRenewalExample
{
    public static async Task RunAsync()
    {
        var services = new ServiceCollection();

        services.AddDistributedLocking(options =>
        {
            options.BackendType = BackendType.InMemory;
            options.DefaultLockDuration = TimeSpan.FromSeconds(10);
            options.EnableAutoRenewal = true;
        });

        var serviceProvider = services.BuildServiceProvider();
        var lockService = serviceProvider.GetRequiredService<ILockService>();
        var monitor = serviceProvider.GetRequiredService<LockMonitor>();

        Console.WriteLine("=== Lock Auto-Renewal Example ===\n");

        const string lockKey = "long-running-task";
        const string ownerId = "processor-1";

        try
        {
            // Acquire the lock with 10-second duration
            Console.WriteLine($"Acquiring lock with 10-second duration...");
            await lockService.AcquireAsync(lockKey, ownerId, TimeSpan.FromSeconds(10));
            Console.WriteLine("✓ Lock acquired\n");

            // Register for auto-renewal every 5 seconds, extending by 10 seconds each time
            Console.WriteLine("Registering lock for auto-renewal (every 5 seconds)...");
            monitor.RegisterLock(
                lockKey,
                ownerId,
                renewalInterval: TimeSpan.FromSeconds(5),
                lockDuration: TimeSpan.FromSeconds(10)
            );
            Console.WriteLine("✓ Lock registered for auto-renewal\n");

            // Start the monitoring background service
            Console.WriteLine("Starting monitor service...");
            monitor.StartMonitoring(TimeSpan.FromMilliseconds(500));
            Console.WriteLine("✓ Monitor started\n");

            // Perform long-running operation (30 seconds)
            // Without auto-renewal, lock would expire at 10 seconds
            // With auto-renewal, lock is extended every 5 seconds
            Console.WriteLine("Performing 30-second operation:");
            Console.WriteLine("(Lock will be automatically renewed every 5 seconds)\n");

            var operationStart = DateTime.UtcNow;
            var renewalCount = 0;

            for (int i = 0; i < 30; i++)
            {
                var elapsed = (DateTime.UtcNow - operationStart).TotalSeconds;
                var lockInfo = await lockService.GetLockAsync(lockKey);

                if (lockInfo is not null)
                {
                    var timeRemaining = (lockInfo.ExpiresAt - DateTime.UtcNow).TotalSeconds;
                    Console.WriteLine($"[{elapsed:F0}s] Lock status: Held, {timeRemaining:F0}s remaining, Renewals: {lockInfo.RenewalCount}");
                    renewalCount = lockInfo.RenewalCount;
                }

                await Task.Delay(1000);
            }

            Console.WriteLine($"\n✓ Operation completed successfully");
            Console.WriteLine($"  Lock was renewed {renewalCount} times during operation");
        }
        finally
        {
            // Stop monitoring and release the lock
            Console.WriteLine("\nCleaning up...");
            await monitor.StopMonitoringAsync();
            monitor.UnregisterLock(lockKey, ownerId);
            await lockService.ReleaseAsync(lockKey, ownerId);
            Console.WriteLine("✓ Lock released and monitor stopped");
        }
    }
}
