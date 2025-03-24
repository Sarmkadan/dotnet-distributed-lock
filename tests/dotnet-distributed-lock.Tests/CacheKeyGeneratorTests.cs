// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using FluentAssertions;
using SarmKadan.DistributedLock.Caching;

namespace SarmKadan.DistributedLock.Tests;

public class CacheKeyGeneratorTests
{
    [Fact]
    public void GenerateLockKey_ValidId_ReturnsCorrectFormat()
    {
        CacheKeyGenerator.GenerateLockKey("lock-001").Should().Be("lock:lock-001");
    }

    [Fact]
    public void GenerateLockKey_EmptyId_ThrowsArgumentException()
    {
        var act = () => CacheKeyGenerator.GenerateLockKey("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateLockKey_NullId_ThrowsArgumentException()
    {
        var act = () => CacheKeyGenerator.GenerateLockKey(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateLockNameKey_ValidName_ReturnsCorrectFormat()
    {
        CacheKeyGenerator.GenerateLockNameKey("my-lock").Should().Be("lock:name:my-lock");
    }

    [Fact]
    public void GenerateLockNameKey_EmptyName_ThrowsArgumentException()
    {
        var act = () => CacheKeyGenerator.GenerateLockNameKey("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateMetricsKey_ValidId_ReturnsCorrectFormat()
    {
        CacheKeyGenerator.GenerateMetricsKey("lock-001").Should().Be("metrics:lock-001");
    }

    [Fact]
    public void GenerateMetricsKey_EmptyId_ThrowsArgumentException()
    {
        var act = () => CacheKeyGenerator.GenerateMetricsKey("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateSystemMetricsKey_ReturnsStaticKey()
    {
        var key = CacheKeyGenerator.GenerateSystemMetricsKey();
        key.Should().NotBeNullOrWhiteSpace();
        key.Should().Contain("metrics");
    }

    [Fact]
    public void DifferentLockIds_ProduceDifferentKeys()
    {
        var key1 = CacheKeyGenerator.GenerateLockKey("a");
        var key2 = CacheKeyGenerator.GenerateLockKey("b");
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void LockKey_And_MetricsKey_AreDifferent()
    {
        var lockKey = CacheKeyGenerator.GenerateLockKey("id-1");
        var metricsKey = CacheKeyGenerator.GenerateMetricsKey("id-1");
        lockKey.Should().NotBe(metricsKey);
    }
}
