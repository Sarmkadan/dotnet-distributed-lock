#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using SarmKadan.DistributedLock;
using Microsoft.Extensions.DependencyInjection;

namespace DistributedLock.Examples;

/// <summary>
/// Example demonstrating metrics collection and monitoring.
/// Track lock acquisition success rates, timing, and contention.
/// </summary>
public class MetricsExample
{
    public static async Task RunAsync()
    {
        var services = new ServiceCollection();

        services.AddDistributedLocking(options =>
        {
            options.BackendType = BackendType.InMemory;
            options.DefaultLockDuration = TimeSpan.FromSeconds(10);
            options.EnableMetrics = true;
        });

        var serviceProvider = services.BuildServiceProvider();
        var lockService = serviceProvider.GetRequiredService<ILockService>();

        Console.WriteLine("=== Lock Metrics Monitoring Example ===\n");

        // Ensure lockService is concrete type for metrics
        if (lockService is not LockService concrete)
        {
            Console.WriteLine("Error: ILockService must be LockService type for metrics access");
            return;
        }

        // Simulate lock operations
        Console.WriteLine("Simulating lock operations...\n");
        await SimulateLockOperationsAsync(lockService).ConfigureAwait(false);

        Console.WriteLine("\nGenerating metrics report...\n");

        // Collect and display metrics
        var metrics = concrete.GetMetrics();
        DisplayMetrics(metrics);
    }

    private static async Task SimulateLockOperationsAsync(ILockService lockService)
    {
        var random = new Random(42);
        var owners = new[] { "worker-1", "worker-2", "worker-3" };

        // Simulate successful acquisitions
        Console.WriteLine("Processing successful lock acquisitions:");
        for (int i = 0; i < 15; i++)
        {
            var key = $"resource-{i % 5}";
            var owner = owners[random.Next(owners.Length)];

            try
            {
                var @lock = await lockService.AcquireAsync(
                    key,
                    owner,
                    TimeSpan.FromSeconds(5)
                );
                Console.WriteLine($"  ✓ Lock acquired: {key} by {owner}");
                await lockService.ReleaseAsync(key, owner).ConfigureAwait(false);
            }
            catch (LockAcquisitionException)
            {
                Console.WriteLine($"  ✗ Lock already held: {key}");
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        Console.WriteLine();

        // Simulate contention
        Console.WriteLine("Simulating lock contention:");
        var contestedKey = "contested-resource";
        var owner1 = "worker-1";

        // Owner 1 holds the lock
        await lockService.AcquireAsync(contestedKey, owner1).ConfigureAwait(false);
        Console.WriteLine($"  ✓ Lock held by {owner1}");

        // Other owners try to acquire (will fail)
        foreach (var owner in new[] { "worker-2", "worker-3", "worker-4" })
        {
            var acquired = await lockService.TryAcquireAsync(contestedKey, owner).ConfigureAwait(false);
            if (acquired is null)
            {
                Console.WriteLine($"  ✗ {owner} could not acquire (already held)");
            }
        }

        // Release the lock
        await lockService.ReleaseAsync(contestedKey, owner1).ConfigureAwait(false);
        Console.WriteLine($"  ✓ Lock released by {owner1}");
    }

    private static void DisplayMetrics(LockMetrics metrics)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           Distributed Lock Performance Metrics         ║");
        Console.WriteLine("╠════════════════════════════════════════════════════════╣");

        Console.WriteLine($"║ Total Acquisition Attempts              {metrics.TotalAcquisitionAttempts,-20}║");
        Console.WriteLine($"║ Successful Acquisitions                 {metrics.SuccessfulAcquisitions,-20}║");
        Console.WriteLine($"║ Failed Acquisitions                     {metrics.FailedAcquisitions,-20}║");

        Console.WriteLine("╠════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║ Success Rate                            {metrics.AcquisitionSuccessRate,19:P1}║");
        Console.WriteLine($"║ Average Acquisition Time                {metrics.AverageAcquisitionTimeMs,15:F2} ms  ║");

        Console.WriteLine("╠════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║ Current Active Locks                    {metrics.CurrentActiveLocks,-20}║");
        Console.WriteLine($"║ Total Locks Created                     {metrics.TotalLocksCreated,-20}║");
        Console.WriteLine($"║ Total Locks Released                    {metrics.TotalLocksReleased,-20}║");

        Console.WriteLine("╚════════════════════════════════════════════════════════╝");

        // Additional analysis
        Console.WriteLine("\nMetrics Analysis:");
        Console.WriteLine("----------------");

        if (metrics.AcquisitionSuccessRate >= 90)
        {
            Console.WriteLine("✓ Excellent success rate - low contention");
        }
        else if (metrics.AcquisitionSuccessRate >= 70)
        {
            Console.WriteLine("⚠ Good success rate - moderate contention");
        }
        else
        {
            Console.WriteLine("✗ Poor success rate - high contention");
        }

        if (metrics.AverageAcquisitionTimeMs < 10)
        {
            Console.WriteLine("✓ Fast acquisition time - good performance");
        }
        else if (metrics.AverageAcquisitionTimeMs < 50)
        {
            Console.WriteLine("⚠ Moderate acquisition time - acceptable performance");
        }
        else
        {
            Console.WriteLine("✗ Slow acquisition time - consider optimization");
        }

        var lockHoldRate = (double)metrics.TotalLocksReleased / metrics.TotalLocksCreated;
        Console.WriteLine($"✓ Lock release rate: {lockHoldRate:P1} - proper cleanup");

        if (metrics.CurrentActiveLocks == 0)
        {
            Console.WriteLine("✓ No active locks - all locks released properly");
        }
        else
        {
            Console.WriteLine($"⚠ {metrics.CurrentActiveLocks} active locks - monitor for orphaned locks");
        }
    }
}
