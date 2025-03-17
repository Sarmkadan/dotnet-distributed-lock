// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using SarmKadan.DistributedLock;
using Microsoft.Extensions.DependencyInjection;

namespace DistributedLock.Examples;

/// <summary>
/// Example demonstrating fencing tokens to prevent zombie writes.
/// Fencing tokens prevent processes from writing to shared resources
/// after their lock has expired (zombie writes).
/// </summary>
public class FencingTokenExample
{
    public static async Task RunAsync()
    {
        var services = new ServiceCollection();

        services.AddDistributedLocking(options =>
        {
            options.BackendType = BackendType.InMemory;
            options.DefaultLockDuration = TimeSpan.FromSeconds(3);
            options.UseFencingTokens = true;
        });

        var serviceProvider = services.BuildServiceProvider();
        var lockService = serviceProvider.GetRequiredService<ILockService>();
        var tokenService = serviceProvider.GetRequiredService<FencingTokenService>();

        Console.WriteLine("=== Fencing Token Example ===\n");

        Console.WriteLine("Scenario: Preventing zombie writes after lock expiration\n");

        const string resourceId = "shared-data-store";
        const string ownerId = "worker-1";

        // Acquire lock with short 3-second duration
        Console.WriteLine("1. Worker acquires lock (3-second duration)...");
        await lockService.AcquireAsync(resourceId, ownerId, TimeSpan.FromSeconds(3));
        Console.WriteLine("   ✓ Lock acquired\n");

        // Issue a fencing token
        Console.WriteLine("2. Worker issues fencing token...");
        var token = tokenService.IssueToken(resourceId);
        Console.WriteLine($"   ✓ Token issued: {token.Token}\n");

        // Validate token immediately (should succeed)
        Console.WriteLine("3. Validating token immediately...");
        if (tokenService.ValidateToken(resourceId, token))
        {
            Console.WriteLine("   ✓ Token is valid - safe to write\n");
        }

        // Simulate work, then wait for lock to expire
        Console.WriteLine("4. Doing work for 5 seconds...");
        Console.WriteLine("   (Lock expires after 3 seconds)\n");

        for (int i = 0; i < 5; i++)
        {
            var isLocked = await lockService.IsLockedAsync(resourceId);
            var lockInfo = await lockService.GetLockAsync(resourceId);

            if (lockInfo != null)
            {
                var timeRemaining = (lockInfo.ExpiresAt - DateTime.UtcNow).TotalSeconds;
                Console.WriteLine($"   [{i}s] Lock still held, {timeRemaining:F1}s remaining");
            }
            else
            {
                Console.WriteLine($"   [{i}s] Lock has expired!");
            }

            await Task.Delay(1000);
        }

        Console.WriteLine();

        // Try to validate token after lock expiration (should fail)
        Console.WriteLine("5. Validating token after lock expiration...");
        if (tokenService.ValidateToken(resourceId, token))
        {
            Console.WriteLine("   ✓ Token is still valid (unexpected!)\n");
        }
        else
        {
            Console.WriteLine("   ✗ Token is invalid - write would be prevented\n");
            Console.WriteLine("   This prevents zombie writes!\n");
        }

        // Demonstrate safe write pattern
        Console.WriteLine("6. Safe write pattern with token validation:\n");

        // Re-acquire lock for new work
        await lockService.AcquireAsync(resourceId, ownerId, TimeSpan.FromSeconds(5));
        var newToken = tokenService.IssueToken(resourceId);

        Console.WriteLine("   Attempting write operation...");

        if (tokenService.ValidateToken(resourceId, newToken))
        {
            Console.WriteLine("   ✓ Token validated, writing data...");
            await SimulateWriteAsync();
            Console.WriteLine("   ✓ Write completed safely\n");
        }
        else
        {
            Console.WriteLine("   ✗ Token invalid, aborting write (lock expired)\n");
        }

        await lockService.ReleaseAsync(resourceId, ownerId);
    }

    private static Task SimulateWriteAsync()
    {
        // Simulate writing to a database or file
        return Task.Delay(100);
    }
}
