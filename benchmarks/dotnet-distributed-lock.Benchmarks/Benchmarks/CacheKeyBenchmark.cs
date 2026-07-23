using BenchmarkDotNet.Attributes;
using SarmKadan.DistributedLock.Caching;

namespace SarmKadan.DistributedLock.Benchmarks;

[MemoryDiagnoser]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class CacheKeyBenchmark
{
    private const string TestLockId = "test-lock-with-many-characters-12345";
    private const string TestOwnerId = "owner-abc-def-456";
    private const string TestConfigName = "distributed-lock-timeout";
    private const string TestQueryName = "getLocksByOwner";
    private static readonly object?[] TestParameters = { "owner-123", 42, true };
    private static readonly string[] TestTags = { "locks", "performance", "cache" };

    [Benchmark(Baseline = true)]
    public string GenerateLockKey_String()
    {
        return CacheKeyGenerator.GenerateLockKey(TestLockId);
    }

    [Benchmark]
    public string GenerateLockKey_Span()
    {
        return CacheKeyGenerator.GenerateLockKey(TestLockId.AsSpan());
    }

    [Benchmark(Baseline = true)]
    public string GenerateLockNameKey_String()
    {
        return CacheKeyGenerator.GenerateLockNameKey("resource-pattern-name");
    }

    [Benchmark]
    public string GenerateLockNameKey_Span()
    {
        return CacheKeyGenerator.GenerateLockNameKey("resource-pattern-name".AsSpan());
    }

    [Benchmark(Baseline = true)]
    public string GenerateMetricsKey_String()
    {
        return CacheKeyGenerator.GenerateMetricsKey(TestLockId);
    }

    [Benchmark]
    public string GenerateMetricsKey_Span()
    {
        return CacheKeyGenerator.GenerateMetricsKey(TestLockId.AsSpan());
    }

    [Benchmark(Baseline = true)]
    public string GenerateStatusKey_String()
    {
        return CacheKeyGenerator.GenerateStatusKey(TestLockId);
    }

    [Benchmark]
    public string GenerateStatusKey_Span()
    {
        return CacheKeyGenerator.GenerateStatusKey(TestLockId.AsSpan());
    }

    [Benchmark(Baseline = true)]
    public string GenerateOwnerLocksKey_String()
    {
        return CacheKeyGenerator.GenerateOwnerLocksKey(TestOwnerId);
    }

    [Benchmark]
    public string GenerateOwnerLocksKey_Span()
    {
        return CacheKeyGenerator.GenerateOwnerLocksKey(TestOwnerId.AsSpan());
    }

    [Benchmark(Baseline = true)]
    public string GenerateConfigurationKey_String()
    {
        return CacheKeyGenerator.GenerateConfigurationKey(TestConfigName);
    }

    [Benchmark]
    public string GenerateConfigurationKey_Span()
    {
        return CacheKeyGenerator.GenerateConfigurationKey(TestConfigName.AsSpan());
    }

    [Benchmark(Baseline = true)]
    public string GenerateTagKey_String()
    {
        return CacheKeyGenerator.GenerateTagKey(TestTags);
    }

    [Benchmark]
    public string GenerateQueryKey_String()
    {
        return CacheKeyGenerator.GenerateQueryKey(TestQueryName, TestParameters);
    }

    [Benchmark]
    public string GenerateQueryKey_Span()
    {
        return CacheKeyGenerator.GenerateQueryKey(TestQueryName.AsSpan(), TestParameters.AsSpan());
    }

    [Benchmark]
    public string GenerateSystemMetricsKey()
    {
        return CacheKeyGenerator.GenerateSystemMetricsKey();
    }

    [Benchmark]
    public string GenerateActiveLockKeysPattern()
    {
        return CacheKeyGenerator.GenerateActiveLockKeysPattern();
    }

    [Benchmark]
    public string[] GetKeysByAcquisition()
    {
        return CacheKeySets.GetKeysByAcquisition(TestLockId, TestOwnerId);
    }

    [Benchmark]
    public string[] GetKeysByRelease()
    {
        return CacheKeySets.GetKeysByRelease(TestLockId, TestOwnerId);
    }

    [Benchmark]
    public string? ExtractLockIdFromKey()
    {
        return CacheKeyGenerator.ExtractLockIdFromKey("lock:my-test-lock-123");
    }

    [Benchmark]
    public ReadOnlySpan<char> ExtractLockIdFromKey_Span()
    {
        return CacheKeyGenerator.ExtractLockIdFromKey("lock:my-test-lock-123".AsSpan());
    }

    [Benchmark]
    public bool IsLockKey_True()
    {
        return CacheKeyGenerator.IsLockKey("lock:my-test-lock");
    }

    [Benchmark]
    public bool IsLockKey_False()
    {
        return CacheKeyGenerator.IsLockKey("metrics:my-test-lock");
    }

    [Benchmark]
    public bool IsLockKey_True_Span()
    {
        return CacheKeyGenerator.IsLockKey("lock:my-test-lock".AsSpan());
    }

    [Benchmark]
    public bool IsLockKey_False_Span()
    {
        return CacheKeyGenerator.IsLockKey("metrics:my-test-lock".AsSpan());
    }

    [Benchmark]
    public bool IsMetricsKey_True()
    {
        return CacheKeyGenerator.IsMetricsKey("metrics:my-test-lock");
    }

    [Benchmark]
    public bool IsMetricsKey_False()
    {
        return CacheKeyGenerator.IsMetricsKey("lock:my-test-lock");
    }

    [Benchmark]
    public bool IsMetricsKey_True_Span()
    {
        return CacheKeyGenerator.IsMetricsKey("metrics:my-test-lock".AsSpan());
    }

    [Benchmark]
    public bool IsMetricsKey_False_Span()
    {
        return CacheKeyGenerator.IsMetricsKey("lock:my-test-lock".AsSpan());
    }
}