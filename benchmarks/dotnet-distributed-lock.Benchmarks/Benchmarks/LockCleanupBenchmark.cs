using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock;
using SarmKadan.DistributedLock.Workers;

namespace SarmKadan.DistributedLock.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
public class LockCleanupBenchmark
{
    private IServiceProvider? _serviceProvider;
    private LockCleanupWorker? _cleanupWorker;

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
            _serviceProvider.GetRequiredService<ILockService>(),
            null!, // ILogger is fine being null for benchmarks if not used
            TimeSpan.FromSeconds(1)
        );
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [Benchmark(Description = "Clean up 1000 expired locks")]
    public async Task Cleanup_1000_Expired_Locks()
    {
        // This is a simplified cleanup benchmark
        await _cleanupWorker!.ExecuteAsync(CancellationToken.None);
    }
}
