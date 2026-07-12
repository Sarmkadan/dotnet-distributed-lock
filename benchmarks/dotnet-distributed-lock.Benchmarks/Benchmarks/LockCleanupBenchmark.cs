using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock;
using SarmKadan.DistributedLock.Configuration;
using SarmKadan.DistributedLock.Enums;
using SarmKadan.DistributedLock.Repository;
using SarmKadan.DistributedLock.Workers;

namespace SarmKadan.DistributedLock.Benchmarks.Benchmarks;

/// <summary>
/// Benchmark for lock cleanup operations.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class LockCleanupBenchmark
{
    private const int ExpiredLockCount = 1000;

    private IServiceProvider? _serviceProvider;
    private ILockRepository? _repository;
    private LockCleanupWorker? _cleanupWorker;

    /// <summary>
    /// Sets up the benchmark by creating a service provider and a lock cleanup worker.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
        });

        // Configure with in-memory for fast cleanup benchmarking
        services.AddDistributedLocking(options =>
        {
            options.BackendType = BackendType.InMemory;
        });

        _serviceProvider = services.BuildServiceProvider();
        _repository = _serviceProvider.GetRequiredService<ILockRepository>();
        _cleanupWorker = new LockCleanupWorker(
            _repository,
            _serviceProvider.GetRequiredService<ILogger<LockCleanupWorker>>(),
            new LockCleanupWorkerOptions { CleanupIntervalMs = 1000 }
        );
    }

    /// <summary>
    /// Seeds the repository with expired locks before each iteration so the sweep has real work to do.
    /// </summary>
    [IterationSetup]
    public void IterationSetup()
    {
        var now = DateTime.UtcNow;

        for (var i = 0; i < ExpiredLockCount; i++)
        {
            var expiredLock = new Models.Lock
            {
                Key = $"cleanup-benchmark-{i}",
                OwnerId = "benchmark-owner",
                AcquiredAt = now.AddMinutes(-10),
                ExpiresAt = now.AddMinutes(-5),
                Duration = TimeSpan.FromMinutes(5)
            };

            _repository!.AcquireAsync(expiredLock, CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Cleans up the service provider and any other resources used by the benchmark.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <summary>
    /// Benchmark for cleaning up 1000 expired locks.
    /// </summary>
    /// <remarks>
    /// Each iteration is seeded with <see cref="ExpiredLockCount"/> expired locks
    /// so the sweep measures a real repository delete pass.
    /// </remarks>
    [Benchmark(Description = "Clean up 1000 expired locks")]
    public async Task Cleanup_1000_Expired_Locks()
    {
        await _cleanupWorker!.RunCleanupOnceAsync(CancellationToken.None);
    }
}
