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
/// Benchmarks for fencing token operations
/// </summary>
[MemoryDiagnoser]
[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class FencingTokenBenchmark
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

    [Benchmark(Description = "Issue fencing token")]
    public void IssueToken()
    {
        if (_lockService is LockService concreteService)
        {
            var tokenService = _serviceProvider!.GetRequiredService<FencingTokenService>();
            var token = tokenService.IssueToken("fencing-resource");
        }
    }

    [Benchmark(Description = "Validate fencing token - valid")]
    public void ValidateToken_Valid()
    {
        if (_lockService is LockService concreteService)
        {
            var tokenService = _serviceProvider!.GetRequiredService<FencingTokenService>();
            var token = tokenService.IssueToken("fencing-resource");
            bool isValid = tokenService.ValidateToken("fencing-resource", token);
        }
    }

    [Benchmark(Description = "Validate fencing token - invalid")]
    public void ValidateToken_Invalid()
    {
        if (_lockService is LockService concreteService)
        {
            var tokenService = _serviceProvider!.GetRequiredService<FencingTokenService>();
            var token = tokenService.IssueToken("fencing-resource");
            bool isValid = tokenService.ValidateToken("different-resource", token);
        }
    }

    [Benchmark(Description = "Check if resource is locked")]
    public void IsResourceLocked()
    {
        if (_lockService is LockService concreteService)
        {
            var tokenService = _serviceProvider!.GetRequiredService<FencingTokenService>();
            bool isLocked = tokenService.IsResourceLocked("fencing-resource");
        }
    }
}