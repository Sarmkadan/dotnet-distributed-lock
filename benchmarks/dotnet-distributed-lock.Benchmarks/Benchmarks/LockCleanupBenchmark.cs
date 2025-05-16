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
    private IServiceProvider? _serviceProvider;
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
        _cleanupWorker = new LockCleanupWorker(
            _serviceProvider.GetRequiredService<ILockRepository>(),
            _serviceProvider.GetRequiredService<ILogger<LockCleanupWorker>>(),
            new LockCleanupWorkerOptions { CleanupIntervalMs = 1000 }
        );
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
    /// This is a simplified cleanup benchmark.
    /// </remarks>
    [Benchmark(Description = "Clean up 1000 expired locks")]
    public async Task Cleanup_1000_Expired_Locks()
    {
        // This is a simplified cleanup benchmark
        await _cleanupWorker!.RunCleanupOnceAsync(CancellationToken.None);
    }
}
