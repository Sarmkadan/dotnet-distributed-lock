using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock;
using SarmKadan.DistributedLock.Backends;
using SarmKadan.DistributedLock.Configuration;
using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Services;

namespace SarmKadan.DistributedLock.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for measuring throughput and scalability of lock operations
/// </summary>
[MemoryDiagnoser]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class ThroughputBenchmark
{
    [Params(BackendType.InMemory, BackendType.Redis)]
    public BackendType BackendType { get; set; }

    [Params("redis://localhost:6379,allowAdmin=true")]
    public string ConnectionString { get; set; } = "redis://localhost:6379,allowAdmin=true";

    private IServiceProvider? _serviceProvider;
    private ILockService? _lockService;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var services = new ServiceCollection();

        // Configure logging to reduce noise
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddFilter("Microsoft", LogLevel.Warning);
            builder.AddFilter("System", LogLevel.Warning);
            builder.AddFilter("SarmKadan.DistributedLock", LogLevel.Error);
        });

        // Configure the backend
        services.AddDistributedLocking(options =>
        {
            options.BackendType = BackendType;
            options.ConnectionString = ConnectionString;
            options.DefaultLockDuration = TimeSpan.FromSeconds(30);
            options.DefaultAcquisitionTimeout = TimeSpan.FromSeconds(5);
            options.EnableAutoRenewal = false;
            options.EnableMetrics = false;
            options.EnableLogging = false;
        });

        _serviceProvider = services.BuildServiceProvider();
        _lockService = _serviceProvider.GetRequiredService<ILockService>();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [Benchmark(Description = "Acquire 1000 locks sequentially")]
    public async Task Acquire_1000_Locks()
    {
        for (int i = 0; i < 1000; i++)
        {
            var @lock = await _lockService!.AcquireAsync($"throughput-lock-{i}", $"throughput-runner-{i}", TimeSpan.FromSeconds(30));
            await CleanupLockAsync($"throughput-lock-{i}", $"throughput-runner-{i}");
        }
    }

    [Benchmark(Description = "Acquire 100 locks concurrently")]
    public async Task Acquire_100_Locks_Concurrently()
    {
        var tasks = new List<Task>();

        for (int i = 0; i < 100; i++)
        {
            int index = i;
            tasks.Add(Task.Run(async () =>
            {
                var @lock = await _lockService!.AcquireAsync($"throughput-lock-{index}", $"throughput-runner-{index}", TimeSpan.FromSeconds(30));
                await CleanupLockAsync($"throughput-lock-{index}", $"throughput-runner-{index}");
            }));
        }

        await Task.WhenAll(tasks);
    }

    [Benchmark(Description = "Acquire and release 1000 times")]
    public async Task Acquire_And_Release_1000_Times()
    {
        for (int i = 0; i < 1000; i++)
        {
            var @lock = await _lockService!.AcquireAsync("throughput-lock", "throughput-runner", TimeSpan.FromSeconds(1));
            await _lockService.ReleaseAsync("throughput-lock", "throughput-runner");
        }
    }

    [Benchmark(Description = "Renew lock 100 times")]
    public async Task Renew_Lock_100_Times()
    {
        var @lock = await _lockService!.AcquireAsync("throughput-lock", "throughput-runner", TimeSpan.FromSeconds(30));

        try
        {
            for (int i = 0; i < 100; i++)
            {
                await _lockService.RenewAsync("throughput-lock", "throughput-runner", TimeSpan.FromSeconds(30));
            }
        }
        finally
        {
            await CleanupLockAsync("throughput-lock", "throughput-runner");
        }
    }

    private async Task CleanupLockAsync(string lockKey, string ownerId)
    {
        try
        {
            await _lockService!.ReleaseAsync(lockKey, ownerId);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}