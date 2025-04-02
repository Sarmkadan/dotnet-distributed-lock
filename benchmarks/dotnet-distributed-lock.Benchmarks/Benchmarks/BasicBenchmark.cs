using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock;
using SarmKadan.DistributedLock.Backends;

namespace SarmKadan.DistributedLock.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for basic lock acquisition and release operations across different backends
/// </summary>
[MemoryDiagnoser]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class BasicBenchmark
{
    [Params(BackendType.InMemory, BackendType.Redis, BackendType.SQLite, BackendType.PostgreSQL)]
    public BackendType BackendType { get; set; }

    [Params("redis://localhost:6379,allowAdmin=true",
            "Host=localhost;Database=locks;Username=postgres;Password=secret;",
            "Data Source=locks.db;")]
    public string ConnectionString { get; set; } = string.Empty;

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

    [Benchmark(Description = "Acquire lock")]
    public async Task AcquireAsync()
    {
        const string lockKey = "basic-acquire-lock";
        const string ownerId = "benchmark-runner";

        var @lock = await _lockService!.AcquireAsync(lockKey, ownerId, TimeSpan.FromSeconds(30));
        await CleanupLockAsync(lockKey, ownerId);
    }

    [Benchmark(Description = "Try acquire lock - success")]
    public async Task TryAcquireAsync_Success()
    {
        const string lockKey = "basic-try-acquire-lock";
        const string ownerId = "benchmark-runner";

        var @lock = await _lockService!.TryAcquireAsync(lockKey, ownerId, TimeSpan.FromSeconds(30));
        if (@lock != null)
        {
            await CleanupLockAsync(lockKey, ownerId);
        }
    }

    [Benchmark(Description = "Try acquire lock - failure")]
    public async Task TryAcquireAsync_Failure()
    {
        const string lockKey = "basic-try-fail-lock";
        const string ownerId = "benchmark-runner";
        const string otherOwnerId = "other-owner";

        // First acquire to make the second one fail
        await _lockService!.AcquireAsync(lockKey, otherOwnerId, TimeSpan.FromSeconds(30));

        try
        {
            var @lock = await _lockService.TryAcquireAsync(lockKey, ownerId, TimeSpan.FromSeconds(1));
            // Should be null since lock is held
        }
        finally
        {
            await CleanupLockAsync(lockKey, otherOwnerId);
        }
    }

    [Benchmark(Description = "Release lock")]
    public async Task ReleaseAsync()
    {
        const string lockKey = "basic-release-lock";
        const string ownerId = "benchmark-runner";

        // Acquire first
        var @lock = await _lockService!.AcquireAsync(lockKey, ownerId, TimeSpan.FromSeconds(30));

        // Then release
        await _lockService.ReleaseAsync(lockKey, ownerId);
    }

    [Benchmark(Description = "Renew lock")]
    public async Task RenewAsync()
    {
        const string lockKey = "basic-renew-lock";
        const string ownerId = "benchmark-runner";

        // Acquire first
        var @lock = await _lockService!.AcquireAsync(lockKey, ownerId, TimeSpan.FromSeconds(30));

        try
        {
            // Then renew
            await _lockService.RenewAsync(lockKey, ownerId, TimeSpan.FromSeconds(30));
        }
        finally
        {
            await CleanupLockAsync(lockKey, ownerId);
        }
    }

    [Benchmark(Description = "Check if locked")]
    public async Task IsLockedAsync()
    {
        const string lockKey = "basic-is-locked-lock";
        const string ownerId = "benchmark-runner";

        // Test with unlocked lock
        bool isUnlocked = await _lockService!.IsLockedAsync(lockKey);

        // Acquire and test again
        await _lockService.AcquireAsync(lockKey, ownerId, TimeSpan.FromSeconds(30));
        bool isLocked = await _lockService.IsLockedAsync(lockKey);

        await CleanupLockAsync(lockKey, ownerId);
    }

    [Benchmark(Description = "Get lock information")]
    public async Task GetLockAsync()
    {
        const string lockKey = "basic-get-lock";
        const string ownerId = "benchmark-runner";

        // Get non-existent lock
        var nullLock = await _lockService!.GetLockAsync("non-existent-lock");

        // Acquire and get info
        await _lockService.AcquireAsync(lockKey, ownerId, TimeSpan.FromSeconds(30));
        var lockInfo = await _lockService.GetLockAsync(lockKey);

        await CleanupLockAsync(lockKey, ownerId);
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