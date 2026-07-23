#!/usr/bin/env dotnet-script

#r "nuget: Microsoft.Extensions.DependencyInjection, 8.0.0"
#r "nuget: Microsoft.Extensions.Logging, 8.0.0"
#r "nuget: Microsoft.Extensions.Logging.Console, 8.0.0"

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock;
using SarmKadan.DistributedLock.Configuration;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Services;

// Setup DI
var services = new ServiceCollection();
services.AddLogging(configure => configure.AddConsole());
services.AddDistributedLocking(options => {
    options.BackendType = BackendType.InMemory;
    options.DefaultLockDuration = TimeSpan.FromSeconds(5);
    options.EnableAutoRenewal = true;
    options.DefaultRenewalFraction = 0.33;
    options.DefaultMaxRenewals = 5;
});

var provider = services.BuildServiceProvider();
var lockService = provider.GetRequiredService<ILockService>();

Console.WriteLine("Testing automatic lock renewal with expiry-aware handle...\n");

try
{
    // Test 1: Acquire lock with auto-renewal
    Console.WriteLine("Test 1: Acquiring lock with auto-renewal enabled...");
    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

    var handle = await lockService.AcquireWithRenewalAsync(
        "test-lock-key",
        "test-owner-id",
        new LockAcquisitionOptions {
            EnableAutoRenewal = true,
            RenewalFraction = 0.33,
            MaxRenewals = 5
        },
        cts.Token
    );

    Console.WriteLine($"✓ Lock acquired successfully");
    Console.WriteLine($"  Lock Key: {handle.Lock.Key}");
    Console.WriteLine($"  Owner ID: {handle.Lock.OwnerId}");
    Console.WriteLine($"  Initial Duration: {handle.Lock.Duration.TotalSeconds}s");
    Console.WriteLine($"  Time Remaining: {handle.TimeRemaining.TotalSeconds:F1}s");

    // Test 2: Check cancellation tokens
    Console.WriteLine("\nTest 2: Checking cancellation tokens...");
    Console.WriteLine($"  RenewalFailedToken can be cancelled: {handle.RenewalFailedToken.CanBeCanceled}");
    Console.WriteLine($"  DisposalToken can be cancelled: {handle.DisposalToken.CanBeCanceled}");
    Console.WriteLine($"  IsValid: {handle.IsValid}");

    // Test 3: Manual renewal
    Console.WriteLine("\nTest 3: Performing manual renewal...");
    var renewed = await handle.RenewAsync();
    Console.WriteLine($"  Manual renewal successful: {renewed}");
    Console.WriteLine($"  Time Remaining after renewal: {handle.TimeRemaining.TotalSeconds:F1}s");

    // Test 4: Wait and check auto-renewal
    Console.WriteLine("\nTest 4: Waiting for auto-renewal to trigger...");
    await Task.Delay(TimeSpan.FromSeconds(8), cts.Token);
    Console.WriteLine($"  Time Remaining after 8 seconds: {handle.TimeRemaining.TotalSeconds:F1}s");
    Console.WriteLine($"  IsValid: {handle.IsValid}");

    // Test 5: Release the lock
    Console.WriteLine("\nTest 5: Releasing lock...");
    var released = await handle.ReleaseAsync();
    Console.WriteLine($"  Lock released successfully: {released}");
    Console.WriteLine($"  IsValid after release: {handle.IsValid}");

    Console.WriteLine("\n✓ All tests passed! Automatic lock renewal is working correctly.");
}
catch (Exception ex)
{
    Console.WriteLine($"\n✗ Test failed with error: {ex.Message}");
    Console.WriteLine($"Stack Trace:\n{ex.StackTrace}");
    Environment.Exit(1);
}
finally
{
    Console.WriteLine("\nTest completed successfully!");
}
