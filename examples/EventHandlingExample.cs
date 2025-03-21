#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using SarmKadan.DistributedLock;
using Microsoft.Extensions.DependencyInjection;

namespace DistributedLock.Examples;

/// <summary>
/// Example demonstrating event subscription for monitoring and observability.
/// Subscribe to lock lifecycle events (acquired, released, renewed, failed)
/// for logging, metrics, and alerting.
/// </summary>
public class EventHandlingExample
{
    public static async Task RunAsync()
    {
        var services = new ServiceCollection();

        services.AddDistributedLocking(options =>
        {
            options.BackendType = BackendType.InMemory;
            options.DefaultLockDuration = TimeSpan.FromSeconds(5);
        });

        var serviceProvider = services.BuildServiceProvider();
        var lockService = serviceProvider.GetRequiredService<ILockService>();
        var subscriber = serviceProvider.GetRequiredService<LockEventSubscriber>();

        Console.WriteLine("=== Lock Event Handling Example ===\n");

        // Setup event handlers
        Console.WriteLine("Setting up event handlers...\n");

        subscriber.SubscribeToAcquiredEvent(@event =>
        {
            Console.WriteLine($"[EVENT] Lock Acquired");
            Console.WriteLine($"  Key: {@event.LockKey}");
            Console.WriteLine($"  Owner: {@event.OwnerId}");
            Console.WriteLine($"  Time: {@event.Timestamp:O}\n");
        });

        subscriber.SubscribeToReleasedEvent(@event =>
        {
            Console.WriteLine($"[EVENT] Lock Released");
            Console.WriteLine($"  Key: {@event.LockKey}");
            Console.WriteLine($"  Owner: {@event.OwnerId}");
            Console.WriteLine($"  Time: {@event.Timestamp:O}\n");
        });

        subscriber.SubscribeToRenewedEvent(@event =>
        {
            Console.WriteLine($"[EVENT] Lock Renewed");
            Console.WriteLine($"  Key: {@event.LockKey}");
            Console.WriteLine($"  Owner: {@event.OwnerId}");
            Console.WriteLine($"  Time: {@event.Timestamp:O}\n");
        });

        subscriber.SubscribeToFailedEvent(@event =>
        {
            Console.WriteLine($"[EVENT] Lock Acquisition Failed");
            Console.WriteLine($"  Key: {@event.LockKey}");
            Console.WriteLine($"  Reason: {@event.Details}");
            Console.WriteLine($"  Time: {@event.Timestamp:O}\n");
        });

        Console.WriteLine("✓ Event handlers registered\n");
        Console.WriteLine("Triggering lock operations...\n");
        Console.WriteLine("=====================================\n");

        // Scenario 1: Successful acquisition and release
        const string lockKey1 = "resource-1";
        const string owner1 = "worker-1";

        var @lock = await lockService.AcquireAsync(lockKey1, owner1).ConfigureAwait(false);
        await Task.Delay(1000).ConfigureAwait(false);
        await lockService.ReleaseAsync(lockKey1, owner1).ConfigureAwait(false);

        Console.WriteLine("=====================================\n");

        // Scenario 2: Attempted acquisition by second owner (should fail)
        Console.WriteLine("Attempting acquisition by second owner (should fail)...\n");

        var lockKey2 = "resource-2";
        var owner2a = "worker-2a";
        var owner2b = "worker-2b";

        await lockService.AcquireAsync(lockKey2, owner2a).ConfigureAwait(false);

        // Try to acquire same lock with different owner (non-blocking)
        var acquired = await lockService.TryAcquireAsync(lockKey2, owner2b).ConfigureAwait(false);
        if (acquired is null)
        {
            Console.WriteLine("[INFO] Second owner could not acquire lock (as expected)\n");
        }

        await lockService.ReleaseAsync(lockKey2, owner2a).ConfigureAwait(false);

        Console.WriteLine("=====================================\n");

        // Scenario 3: Multiple quick acquisitions
        Console.WriteLine("Performing multiple quick lock operations...\n");

        for (int i = 0; i < 3; i++)
        {
            var key = $"rapid-lock-{i}";
            var owner = $"process-{i}";

            var l = await lockService.AcquireAsync(key, owner).ConfigureAwait(false);
            await Task.Delay(200).ConfigureAwait(false);
            await lockService.ReleaseAsync(key, owner).ConfigureAwait(false);
        }

        Console.WriteLine("=====================================\n");
        Console.WriteLine("Event handling example completed!");
    }
}
