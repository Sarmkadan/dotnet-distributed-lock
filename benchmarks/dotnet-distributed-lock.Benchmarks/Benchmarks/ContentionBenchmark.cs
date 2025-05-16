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
/// Benchmarks for measuring performance under contention scenarios
/// </summary>
[MemoryDiagnoser]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class ContentionBenchmark
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

    [Benchmark(Description = "Single lock acquisition under high contention")]
    public async Task HighContention_Acquire()
    {
        const string lockKey = "contention-lock";
        const string ownerId = "contention-runner";
        const string existingOwnerId = "existing-owner";

        // Pre-acquire the lock to create contention
        var existingLock = await _lockService!.AcquireAsync(lockKey, existingOwnerId, TimeSpan.FromSeconds(30));

        try
        {
            // Try to acquire the same lock (should fail)
            var @lock = await _lockService.TryAcquireAsync(lockKey, ownerId, TimeSpan.FromSeconds(1));
        }
        finally
        {
            await CleanupLockAsync(lockKey, existingOwnerId);
        }
    }

    [Benchmark(Description = "Multiple sequential acquisitions with same key")]
    public async Task Sequential_Acquisitions_Same_Key()
    {
        const string lockKey = "contention-seq-lock";
        const string ownerIdPrefix = "contention-runner";

        for (int i = 0; i < 50; i++)
        {
            var @lock = await _lockService!.AcquireAsync(lockKey, $"{ownerIdPrefix}-{i}", TimeSpan.FromSeconds(1));
            await CleanupLockAsync(lockKey, $"{ownerIdPrefix}-{i}");
        }
    }

    [Benchmark(Description = "Check if locked operation")]
    public async Task IsLocked_Operation()
    {
        const string lockKey = "contention-is-locked";
        const string ownerId = "contention-runner";

        // Test with locked lock
        await _lockService!.AcquireAsync(lockKey, ownerId, TimeSpan.FromSeconds(30));

        try
        {
            bool isLocked = await _lockService.IsLockedAsync(lockKey);
            // Should be true
        }
        finally
        {
            await CleanupLockAsync(lockKey, ownerId);
        }

        // Test with unlocked lock
        bool isUnlocked = await _lockService.IsLockedAsync(lockKey);
        // Should be false
    }

    [Benchmark(Description = "Get lock information")]
    public async Task GetLock_Information()
    {
        const string lockKey = "contention-get-lock";
        const string ownerId = "contention-runner";

        await _lockService!.AcquireAsync(lockKey, ownerId, TimeSpan.FromSeconds(30));

        try
        {
            var lockInfo = await _lockService.GetLockAsync(lockKey);
            // Should not be null
        }
        finally
        {
            await CleanupLockAsync(lockKey, ownerId);
        }

        var nullLockInfo = await _lockService.GetLockAsync("non-existent-lock");
        // Should be null
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