#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using SarmKadan.DistributedLock;
using Microsoft.Extensions.DependencyInjection;

namespace DistributedLock.Examples;

/// <summary>
/// Example showing how to coordinate database migrations across multiple instances.
/// Only one instance runs migrations at a time, preventing schema conflicts.
/// </summary>
public class DatabaseMigrationExample
{
    private const string MigrationLockKey = "db-migration";

    public static async Task RunAsync()
    {
        var services = new ServiceCollection();

        services.AddDistributedLocking(options =>
        {
            options.BackendType = BackendType.InMemory;
            options.DefaultLockDuration = TimeSpan.FromMinutes(5);
            options.DefaultAcquisitionTimeout = TimeSpan.FromSeconds(10);
        });

        var serviceProvider = services.BuildServiceProvider();
        var lockService = serviceProvider.GetRequiredService<ILockService>();

        Console.WriteLine("=== Database Migration Coordination Example ===\n");

        // Simulate multiple instances trying to run migrations
        var instance1 = RunMigrationAsync(lockService, "instance-1");
        var instance2 = RunMigrationAsync(lockService, "instance-2");
        var instance3 = RunMigrationAsync(lockService, "instance-3");

        await Task.WhenAll(instance1, instance2, instance3);
    }

    private static async Task RunMigrationAsync(ILockService lockService, string instanceId)
    {
        Console.WriteLine($"[{instanceId}] Starting...");

        try
        {
            // Try to acquire the migration lock
            Console.WriteLine($"[{instanceId}] Waiting for migration lock...");
            var @lock = await lockService.AcquireAsync(
                MigrationLockKey,
                instanceId,
                TimeSpan.FromMinutes(5)
            );

            Console.WriteLine($"[{instanceId}] ✓ Lock acquired, running migrations");

            // Run migrations (simulate with delay)
            await ExecuteMigrationsAsync(instanceId);

            Console.WriteLine($"[{instanceId}] ✓ Migrations completed");

            // Release the lock
            await lockService.ReleaseAsync(MigrationLockKey, instanceId);
            Console.WriteLine($"[{instanceId}] ✓ Lock released");
        }
        catch (LockAcquisitionException ex)
        {
            Console.WriteLine($"[{instanceId}] ✗ Could not acquire migration lock within timeout: {ex.Message}");
            Console.WriteLine($"[{instanceId}] Another instance is running migrations. Skipping migration.");
        }
    }

    private static async Task ExecuteMigrationsAsync(string instanceId)
    {
        // Simulate migration steps
        var migrations = new[]
        {
            "Creating tables...",
            "Adding indexes...",
            "Populating seed data...",
            "Verifying integrity..."
        };

        foreach (var migration in migrations)
        {
            Console.WriteLine($"[{instanceId}]   - {migration}");
            await Task.Delay(1000);
        }
    }
}
