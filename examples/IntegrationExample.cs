#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using SarmKadan.DistributedLock;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DistributedLock.Examples;

/// <summary>
/// Example of integrating the distributed lock service into an ASP.NET Core-style
/// dependency injection container and background worker.
/// </summary>
public class IntegrationExample
{
    public static void ConfigureServices(IServiceCollection services)
    {
        // 1. Register distributed locking in the container
        services.AddDistributedLocking(options =>
        {
            options.BackendType = BackendType.InMemory;
            options.DefaultLockDuration = TimeSpan.FromSeconds(30);
        });

        // 2. Register background worker
        services.AddHostedService<DistributedWorkWorker>();
    }
}

public class DistributedWorkWorker : BackgroundService
{
    private readonly ILockService _lockService;

    public DistributedWorkWorker(ILockService lockService)
    {
        _lockService = lockService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Execute periodic task using distributed lock for coordination
            await TryRunTaskAsync(stoppingToken);
            
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task TryRunTaskAsync(CancellationToken ct)
    {
        const string lockKey = "worker-task-lock";
        var workerId = Environment.MachineName;

        try
        {
            // Try acquire, return if held by another instance
            var @lock = await _lockService.TryAcquireAsync(lockKey, workerId, TimeSpan.FromSeconds(10), ct);
            if (@lock == null) return;

            Console.WriteLine("Worker instance acquired lock, performing work...");
            await Task.Delay(2000, ct);
            
            await _lockService.ReleaseAsync(lockKey, workerId, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in worker: {ex.Message}");
        }
    }
}
