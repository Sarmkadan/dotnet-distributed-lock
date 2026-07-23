#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =====================================================================

using Microsoft.Extensions.DependencyInjection;
using SarmKadan.DistributedLock;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Services;

namespace DistributedLock.Examples;

/// <summary>
/// Example demonstrating the RunWithLockAsync extension method for scoped lock execution.
/// This shows how to use the high-level primitive that handles lock acquisition, renewal,
/// and cleanup automatically.
/// </summary>
public class RunWithLockExample
{
    public static async Task RunAsync()
    {
        var services = new ServiceCollection();

        services.AddDistributedLocking(options =>
        {
            options.BackendType = BackendType.InMemory;
            options.DefaultLockDuration = TimeSpan.FromSeconds(15);
            options.EnableAutoRenewal = true;
        });

        var serviceProvider = services.BuildServiceProvider();
        var lockService = serviceProvider.GetRequiredService<ILockService>();

        Console.WriteLine("=== RunWithLockAsync Example ===\n");

        const string lockKey = "critical-operation";
        const string ownerId = "worker-1";

        // Example 1: Using RunWithLockAsync with a typed result
        Console.WriteLine("Example 1: Typed result with automatic renewal");
        Console.WriteLine("-----------------------------------------------");

        var result1 = await lockService.RunWithLockAsync(
            lockKey,
            ownerId,
            async ct =>
            {
                Console.WriteLine("✓ Lock acquired, performing operation...");
                await Task.Delay(2000, ct); // Simulate work
                return "Operation completed successfully";
            }
        );

        switch (result1.Status)
        {
            case LockAcquisitionStatus.Acquired:
                Console.WriteLine($"✓ Success: {result1.Value}");
                break;
            case LockAcquisitionStatus.Contended:
                Console.WriteLine($"✗ Contended: {result1.ErrorMessage}");
                break;
            case LockAcquisitionStatus.Faulted:
                Console.WriteLine($"✗ Faulted: {result1.Exception?.Message}");
                break;
        }

        Console.WriteLine();

        // Example 2: Using RunWithLockAsync with void result
        Console.WriteLine("Example 2: Void result with automatic renewal");
        Console.WriteLine("-------------------------------------------");

        var result2 = await lockService.RunWithLockAsync(
            "another-operation",
            "worker-2",
            async ct =>
            {
                Console.WriteLine("✓ Lock acquired, performing action...");
                await Task.Delay(1000, ct); // Simulate work
                Console.WriteLine("✓ Action completed");
            }
        );

        switch (result2.Status)
        {
            case LockAcquisitionStatus.Acquired:
                Console.WriteLine("✓ Action completed successfully");
                break;
            case LockAcquisitionStatus.Contended:
                Console.WriteLine($"✗ Contended: {result2.ErrorMessage}");
                break;
            case LockAcquisitionStatus.Faulted:
                Console.WriteLine($"✗ Faulted: {result2.Exception?.Message}");
                break;
        }

        Console.WriteLine();

        // Example 3: Using custom lock options with renewal settings
        Console.WriteLine("Example 3: Custom lock options with renewal");
        Console.WriteLine("-----------------------------------------");

        var options = new LockAcquisitionOptions
        {
            EnableAutoRenewal = true,
            RenewalFraction = 0.5, // Renew at half of lock duration
            MaxRenewals = 5
        };

        var result3 = await lockService.RunWithLockAsync(
            "long-operation",
            "worker-3",
            async ct =>
            {
                Console.WriteLine("✓ Lock acquired with custom renewal settings");
                await Task.Delay(8000, ct); // Simulate long-running work
                return 42;
            },
            options
        );

        switch (result3.Status)
        {
            case LockAcquisitionStatus.Acquired:
                Console.WriteLine($"✓ Success: Returned value {result3.Value}");
                break;
            case LockAcquisitionStatus.Contended:
                Console.WriteLine($"✗ Contended: {result3.ErrorMessage}");
                break;
            case LockAcquisitionStatus.Faulted:
                Console.WriteLine($"✗ Faulted: {result3.Exception?.Message}");
                break;
        }

        Console.WriteLine("\n=== All examples completed ===");
    }
}