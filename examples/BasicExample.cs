// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using SarmKadan.DistributedLock;
using Microsoft.Extensions.DependencyInjection;

namespace DistributedLock.Examples;

/// <summary>
/// Basic example demonstrating fundamental lock acquisition and release.
/// This is the simplest starting point for using the library.
/// </summary>
public class BasicExample
{
    public static async Task RunAsync()
    {
        // Setup dependency injection
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

        Console.WriteLine("=== Basic Lock Acquisition Example ===\n");

        // Example 1: Simple acquire and release
        await SimpleAcquireAndReleaseAsync(lockService, lockKey, ownerId);

        Console.WriteLine();

        // Example 2: Try-acquire (non-blocking)
        await TryAcquireExampleAsync(lockService, lockKey, ownerId);

        Console.WriteLine();

        // Example 3: Acquire with finally block
        await AcquireWithFinallyAsync(lockService, lockKey, ownerId);
    }

    private static async Task SimpleAcquireAndReleaseAsync(
        ILockService lockService,
        string lockKey,
        string ownerId)
    {
        Console.WriteLine("Example 1: Simple Acquire and Release");
        Console.WriteLine("--------------------------------------");

        try
        {
            // Acquire the lock - blocks until successful or times out
            Console.WriteLine("Attempting to acquire lock...");
            var @lock = await lockService.AcquireAsync(lockKey, ownerId);

            Console.WriteLine($"✓ Lock acquired: {@lock.Key}");
            Console.WriteLine($"  Owner: {@lock.OwnerId}");
            Console.WriteLine($"  Expires at: {@lock.ExpiresAt:O}");

            // Simulate some work while holding the lock
            Console.WriteLine("  Performing critical work...");
            await Task.Delay(1000);

            // Release the lock
            await lockService.ReleaseAsync(lockKey, ownerId);
            Console.WriteLine("✓ Lock released");
        }
        catch (LockAcquisitionException ex)
        {
            Console.WriteLine($"✗ Failed to acquire lock: {ex.Message}");
        }
    }

    private static async Task TryAcquireExampleAsync(
        ILockService lockService,
        string lockKey,
        string ownerId)
    {
        Console.WriteLine("Example 2: Non-blocking Try-Acquire");
        Console.WriteLine("------------------------------------");

        // Try to acquire without blocking
        Console.WriteLine("Attempting non-blocking lock acquisition...");
        var @lock = await lockService.TryAcquireAsync(lockKey, ownerId);

        if (@lock != null)
        {
            Console.WriteLine("✓ Lock acquired");

            try
            {
                Console.WriteLine("  Performing work...");
                await Task.Delay(500);
            }
            finally
            {
                await lockService.ReleaseAsync(lockKey, ownerId);
                Console.WriteLine("✓ Lock released");
            }
        }
        else
        {
            Console.WriteLine("✗ Lock is already held by another owner");
        }
    }

    private static async Task AcquireWithFinallyAsync(
        ILockService lockService,
        string lockKey,
        string ownerId)
    {
        Console.WriteLine("Example 3: Using Try-Finally Pattern");
        Console.WriteLine("-------------------------------------");

        try
        {
            Console.WriteLine("Acquiring lock...");
            var @lock = await lockService.AcquireAsync(lockKey, ownerId);
            Console.WriteLine("✓ Lock acquired");

            // If an exception occurs here, the finally block still executes
            Console.WriteLine("  Performing work that might fail...");
            await Task.Delay(500);

            Console.WriteLine("✓ Work completed successfully");
        }
        catch (LockAcquisitionException ex)
        {
            Console.WriteLine($"✗ Lock acquisition failed: {ex.Message}");
        }
        finally
        {
            // Always released, even if work threw an exception
            await lockService.ReleaseAsync(lockKey, ownerId);
            Console.WriteLine("✓ Lock released (in finally block)");
        }
    }
}
