#nullable enable

// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Tests;

using SarmKadan.DistributedLock.Caching;

using FluentAssertions;
using Xunit;

/// <summary>
/// Tests for the <see cref="CacheKeyGenerator"/> class.
/// </summary>
public class CacheKeyGeneratorTests
{
    /// <summary>
    /// Tests that GenerateLockKey returns the correct key format for a valid lock ID.
    /// </summary>
    [Fact]
    public void GenerateLockKey_WithValidLockId_ReturnsCorrectKey()
    {
        // Arrange
        var lockId = "test-lock-123";
        var expected = "lock:test-lock-123";

        // Act
        var result = CacheKeyGenerator.GenerateLockKey(lockId);

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Tests that GenerateLockKey throws ArgumentException when lockId is empty.
    /// </summary>
    [Fact]
    public void GenerateLockKey_WithEmptyLockId_ThrowsArgumentException()
    {
        // Arrange
        var lockId = string.Empty;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => CacheKeyGenerator.GenerateLockKey(lockId));
    }

    /// <summary>
    /// Tests that GenerateLockKey throws ArgumentException when lockId is null.
    /// </summary>
    [Fact]
    public void GenerateLockKey_WithNullLockId_ThrowsArgumentException()
    {
        // Arrange
        string? lockId = null;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => CacheKeyGenerator.GenerateLockKey(lockId));
    }

    /// <summary>
    /// Tests that GenerateLockKey produces the same output for the same input (deterministic).
    /// </summary>
    [Fact]
    public void GenerateLockKey_Deterministic()
    {
        // Arrange
        var lockId = "consistent-lock-id";

        // Act (call twice)
        var result1 = CacheKeyGenerator.GenerateLockKey(lockId);
        var result2 = CacheKeyGenerator.GenerateLockKey(lockId);

        // Assert
        result1.Should().Be(result2);
    }

    /// <summary>
    /// Tests that GenerateLockNameKey returns the correct key format for a valid lock name.
    /// </summary>
    [Fact]
    public void GenerateLockNameKey_WithValidLockName_ReturnsCorrectKey()
    {
        // Arrange
        var lockName = "resource-name";
        var expected = "lock:name:resource-name";

        // Act
        var result = CacheKeyGenerator.GenerateLockNameKey(lockName);

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Tests that GenerateLockNameKey throws ArgumentException when lockName is empty.
    /// </summary>
    [Fact]
    public void GenerateLockNameKey_WithEmptyLockName_ThrowsArgumentException()
    {
        // Arrange
        var lockName = string.Empty;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => CacheKeyGenerator.GenerateLockNameKey(lockName));
    }

    /// <summary>
    /// Tests that GenerateLockNameKey produces the same output for the same input (deterministic).
    /// </summary>
    [Fact]
    public void GenerateLockNameKey_Deterministic()
    {
        // Arrange
        var lockName = "resource-pattern";

        // Act (call twice)
        var result1 = CacheKeyGenerator.GenerateLockNameKey(lockName);
        var result2 = CacheKeyGenerator.GenerateLockNameKey(lockName);

        // Assert
        result1.Should().Be(result2);
    }

    /// <summary>
    /// Tests that GenerateMetricsKey returns the correct key format for a valid lock ID.
    /// </summary>
    [Fact]
    public void GenerateMetricsKey_WithValidLockId_ReturnsCorrectKey()
    {
        // Arrange
        var lockId = "metrics-lock";
        var expected = "metrics:metrics-lock";

        // Act
        var result = CacheKeyGenerator.GenerateMetricsKey(lockId);

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Tests that GenerateMetricsKey throws ArgumentException when lockId is empty.
    /// </summary>
    [Fact]
    public void GenerateMetricsKey_WithEmptyLockId_ThrowsArgumentException()
    {
        // Arrange
        var lockId = string.Empty;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => CacheKeyGenerator.GenerateMetricsKey(lockId));
    }

    /// <summary>
    /// Tests that GenerateMetricsKey produces the same output for the same input (deterministic).
    /// </summary>
    [Fact]
    public void GenerateMetricsKey_Deterministic()
    {
        // Arrange
        var lockId = "consistent-metrics-lock";

        // Act (call twice)
        var result1 = CacheKeyGenerator.GenerateMetricsKey(lockId);
        var result2 = CacheKeyGenerator.GenerateMetricsKey(lockId);

        // Assert
        result1.Should().Be(result2);
    }

    /// <summary>
    /// Tests that GenerateSystemMetricsKey returns the correct key for system metrics.
    /// </summary>
    [Fact]
    public void GenerateSystemMetricsKey_ReturnsCorrectKey()
    {
        // Arrange
        var expected = "metrics:system";

        // Act
        var result = CacheKeyGenerator.GenerateSystemMetricsKey();

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Tests that GenerateStatusKey returns the correct key format for a valid lock ID.
    /// </summary>
    [Fact]
    public void GenerateStatusKey_WithValidLockId_ReturnsCorrectKey()
    {
        // Arrange
        var lockId = "status-lock-456";
        var expected = "status:status-lock-456";

        // Act
        var result = CacheKeyGenerator.GenerateStatusKey(lockId);

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Tests that GenerateStatusKey throws ArgumentException when lockId is empty.
    /// </summary>
    [Fact]
    public void GenerateStatusKey_WithEmptyLockId_ThrowsArgumentException()
    {
        // Arrange
        var lockId = string.Empty;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => CacheKeyGenerator.GenerateStatusKey(lockId));
    }

    /// <summary>
    /// Tests that GenerateStatusKey produces the same output for the same input (deterministic).
    /// </summary>
    [Fact]
    public void GenerateStatusKey_Deterministic()
    {
        // Arrange
        var lockId = "consistent-status-lock";

        // Act (call twice)
        var result1 = CacheKeyGenerator.GenerateStatusKey(lockId);
        var result2 = CacheKeyGenerator.GenerateStatusKey(lockId);

        // Assert
        result1.Should().Be(result2);
    }

    /// <summary>
    /// Tests that GenerateOwnerLocksKey returns the correct key format for a valid owner ID.
    /// </summary>
    [Fact]
    public void GenerateOwnerLocksKey_WithValidOwnerId_ReturnsCorrectKey()
    {
        // Arrange
        var ownerId = "owner-123";
        var expected = "lock:owner:owner-123";

        // Act
        var result = CacheKeyGenerator.GenerateOwnerLocksKey(ownerId);

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Tests that GenerateOwnerLocksKey throws ArgumentException when ownerId is empty.
    /// </summary>
    [Fact]
    public void GenerateOwnerLocksKey_WithEmptyOwnerId_ThrowsArgumentException()
    {
        // Arrange
        var ownerId = string.Empty;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => CacheKeyGenerator.GenerateOwnerLocksKey(ownerId));
    }

    /// <summary>
    /// Tests that GenerateOwnerLocksKey produces the same output for the same input (deterministic).
    /// </summary>
    [Fact]
    public void GenerateOwnerLocksKey_Deterministic()
    {
        // Arrange
        var ownerId = "consistent-owner";

        // Act (call twice)
        var result1 = CacheKeyGenerator.GenerateOwnerLocksKey(ownerId);
        var result2 = CacheKeyGenerator.GenerateOwnerLocksKey(ownerId);

        // Assert
        result1.Should().Be(result2);
    }

    /// <summary>
    /// Tests that GenerateActiveLockKeysPattern returns the correct pattern for active lock keys.
    /// </summary>
    [Fact]
    public void GenerateActiveLockKeysPattern_ReturnsCorrectPattern()
    {
        // Arrange
        var expected = "lock:active:*";

        // Act
        var result = CacheKeyGenerator.GenerateActiveLockKeysPattern();

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Tests that GenerateQueryKey returns a hashed key with the correct prefix and length when given parameters.
    /// </summary>
    [Fact]
    public void GenerateQueryKey_WithParameters_ReturnsHashedKey()
    {
        // Arrange
        var queryName = "getLocksByOwner";
        var parameters = new object?[] { "owner-123", 42, true };

        // Act
        var result = CacheKeyGenerator.GenerateQueryKey(queryName, parameters);

        // Assert
        result.Should().StartWith("query:");
        result.Should().HaveLength(22); // "query:" + 16 char hash
    }

    /// <summary>
    /// Tests that GenerateQueryKey produces the same output for the same input (deterministic).
    /// </summary>
    [Fact]
    public void GenerateQueryKey_Deterministic()
    {
        // Arrange
        var queryName = "testQuery";
        var parameters = new object?[] { "param1", "param2" };

        // Act (call twice)
        var result1 = CacheKeyGenerator.GenerateQueryKey(queryName, parameters);
        var result2 = CacheKeyGenerator.GenerateQueryKey(queryName, parameters);

        // Assert
        result1.Should().Be(result2);
    }

    /// <summary>
    /// Tests that GenerateQueryKey produces different keys for different parameter values.
    /// </summary>
    [Fact]
    public void GenerateQueryKey_WithDifferentParameters_ProducesDifferentKeys()
    {
        // Arrange
        var queryName = "testQuery";
        var param1 = new object?[] { "value1" };
        var param2 = new object?[] { "value2" };

        // Act
        var result1 = CacheKeyGenerator.GenerateQueryKey(queryName, param1);
        var result2 = CacheKeyGenerator.GenerateQueryKey(queryName, param2);

        // Assert
        result1.Should().NotBe(result2);
    }

    /// <summary>
    /// Tests that GenerateConfigurationKey returns the correct key format for a valid configuration name.
    /// </summary>
    [Fact]
    public void GenerateConfigurationKey_WithValidConfigName_ReturnsCorrectKey()
    {
        // Arrange
        var configName = "timeout-config";
        var expected = "config:timeout-config";

        // Act
        var result = CacheKeyGenerator.GenerateConfigurationKey(configName);

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Tests that GenerateConfigurationKey throws ArgumentException when configName is empty.
    /// </summary>
    [Fact]
    public void GenerateConfigurationKey_WithEmptyConfigName_ThrowsArgumentException()
    {
        // Arrange
        var configName = string.Empty;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => CacheKeyGenerator.GenerateConfigurationKey(configName));
    }

    /// <summary>
    /// Tests that GenerateConfigurationKey produces the same output for the same input (deterministic).
    /// </summary>
    [Fact]
    public void GenerateConfigurationKey_Deterministic()
    {
        // Arrange
        var configName = "consistent-config";

        // Act (call twice)
        var result1 = CacheKeyGenerator.GenerateConfigurationKey(configName);
        var result2 = CacheKeyGenerator.GenerateConfigurationKey(configName);

        // Assert
        result1.Should().Be(result2);
    }

    /// <summary>
    /// Tests that GenerateTagKey returns the correct key format for valid tags.
    /// </summary>
    [Fact]
    public void GenerateTagKey_WithValidTags_ReturnsCorrectKey()
    {
        // Arrange
        var tags = new[] { "tag1", "tag2", "tag3" };
        var expected = "tag:tag1:tag2:tag3";

        // Act
        var result = CacheKeyGenerator.GenerateTagKey(tags);

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Tests that GenerateTagKey returns the correct key format for a single tag.
    /// </summary>
    [Fact]
    public void GenerateTagKey_WithSingleTag_ReturnsCorrectKey()
    {
        // Arrange
        var tags = new[] { "single-tag" };
        var expected = "tag:single-tag";

        // Act
        var result = CacheKeyGenerator.GenerateTagKey(tags);

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Tests that GenerateTagKey throws ArgumentException when tags array is empty.
    /// </summary>
    [Fact]
    public void GenerateTagKey_WithEmptyTags_ThrowsArgumentException()
    {
        // Arrange
        var tags = Array.Empty<string>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => CacheKeyGenerator.GenerateTagKey(tags));
    }

    /// <summary>
    /// Tests that GenerateTagKey throws ArgumentException when tags is null.
    /// </summary>
    [Fact]
    public void GenerateTagKey_WithNullTags_ThrowsArgumentException()
    {
        // Arrange
        string[]? tags = null;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => CacheKeyGenerator.GenerateTagKey(tags));
    }

    /// <summary>
    /// Tests that GenerateTagKey produces the same output for the same input (deterministic).
    /// </summary>
    [Fact]
    public void GenerateTagKey_Deterministic()
    {
        // Arrange
        var tags = new[] { "tag1", "tag2" };

        // Act (call twice)
        var result1 = CacheKeyGenerator.GenerateTagKey(tags);
        var result2 = CacheKeyGenerator.GenerateTagKey(tags);

        // Assert
        result1.Should().Be(result2);
    }

    /// <summary>
    /// Tests that GenerateTagKey produces different keys for different tag sets.
    /// </summary>
    [Fact]
    public void GenerateTagKey_DistinctResourcesProduceDistinctKeys()
    {
        // Arrange
        var tags1 = new[] { "resource", "type1" };
        var tags2 = new[] { "resource", "type2" };

        // Act
        var result1 = CacheKeyGenerator.GenerateTagKey(tags1);
        var result2 = CacheKeyGenerator.GenerateTagKey(tags2);

        // Assert
        result1.Should().NotBe(result2);
    }

    /// <summary>
    /// Tests that ExtractLockIdFromKey returns the lock ID when given a valid lock key.
    /// </summary>
    [Fact]
    public void ExtractLockIdFromKey_WithLockKey_ReturnsLockId()
    {
        // Arrange
        var lockKey = "lock:my-lock-id";
        var expected = "my-lock-id";

        // Act
        var result = CacheKeyGenerator.ExtractLockIdFromKey(lockKey);

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Tests that ExtractLockIdFromKey returns null when given a non-lock key.
    /// </summary>
    [Fact]
    public void ExtractLockIdFromKey_WithNonLockKey_ReturnsNull()
    {
        // Arrange
        var nonLockKey = "metrics:system";

        // Act
        var result = CacheKeyGenerator.ExtractLockIdFromKey(nonLockKey);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Tests that ExtractLockIdFromKey returns null when given an empty string.
    /// </summary>
    [Fact]
    public void ExtractLockIdFromKey_WithEmptyString_ReturnsNull()
    {
        // Arrange
        var emptyKey = string.Empty;

        // Act
        var result = CacheKeyGenerator.ExtractLockIdFromKey(emptyKey);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Tests that IsLockKey returns true when given a valid lock key.
    /// </summary>
    [Fact]
    public void IsLockKey_WithLockKey_ReturnsTrue()
    {
        // Arrange
        var lockKey = "lock:my-lock";

        // Act
        var result = CacheKeyGenerator.IsLockKey(lockKey);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// Tests that IsLockKey returns false when given a metrics key.
    /// </summary>
    [Fact]
    public void IsLockKey_WithMetricsKey_ReturnsFalse()
    {
        // Arrange
        var metricsKey = "metrics:my-lock";

        // Act
        var result = CacheKeyGenerator.IsLockKey(metricsKey);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Tests that IsLockKey returns false when given a name pattern key.
    /// </summary>
    [Fact]
    public void IsLockKey_WithNamePatternKey_ReturnsFalse()
    {
        // Arrange
        var nameKey = "lock:name:resource";

        // Act
        var result = CacheKeyGenerator.IsLockKey(nameKey);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Tests that IsLockKey returns false when given an owner key.
    /// </summary>
    [Fact]
    public void IsLockKey_WithOwnerKey_ReturnsFalse()
    {
        // Arrange
        var ownerKey = "lock:owner:owner123";

        // Act
        var result = CacheKeyGenerator.IsLockKey(ownerKey);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Tests that IsMetricsKey returns true when given a valid metrics key.
    /// </summary>
    [Fact]
    public void IsMetricsKey_WithMetricsKey_ReturnsTrue()
    {
        // Arrange
        var metricsKey = "metrics:my-lock";

        // Act
        var result = CacheKeyGenerator.IsMetricsKey(metricsKey);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// Tests that IsMetricsKey returns true when given the system metrics key.
    /// </summary>
    [Fact]
    public void IsMetricsKey_WithSystemMetricsKey_ReturnsTrue()
    {
        // Arrange
        var systemMetricsKey = "metrics:system";

        // Act
        var result = CacheKeyGenerator.IsMetricsKey(systemMetricsKey);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// Tests that IsMetricsKey returns false when given a non-metrics key.
    /// </summary>
    [Fact]
    public void IsMetricsKey_WithNonMetricsKey_ReturnsFalse()
    {
        // Arrange
        var lockKey = "lock:my-lock";

        // Act
        var result = CacheKeyGenerator.IsMetricsKey(lockKey);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Tests that all cache key pattern constants have the correct values.
    /// </summary>
    [Fact]
    public void CacheKeyPatterns_AllConstants_AreCorrect()
    {
        // Assert
        CacheKeyPatterns.AllLocks.Should().Be("lock:*");
        CacheKeyPatterns.AllMetrics.Should().Be("metrics:*");
        CacheKeyPatterns.AllQueries.Should().Be("query:*");
        CacheKeyPatterns.MetricsPattern.Should().Be("metrics:*");
        CacheKeyPatterns.StatusPattern.Should().Be("status:*");
    }

    /// <summary>
    /// Tests that special characters in resource names are handled correctly when generating keys.
    /// </summary>
    [Fact]
    public void SpecialCharacters_InResourceNames_AreHandledCorrectly()
    {
        // Arrange - test various special characters
        var testCases = new[]
        {
            "lock-with-dashes",
            "lock_with_underscores",
            "lock.with.dots",
            "lock:with:colons",
            "lock/with/slashes",
            "lock with spaces",
            "lock@with#special$chars",
            "lock(parens)",
            "lock[brackets]",
            "lock{braces}",
            "lock123",
            "LOCK-UPPERCASE",
            "lock_123_test"
        };

        // Act & Assert - all should produce valid keys deterministically
        foreach (var testCase in testCases)
        {
            // Generate lock key
            var lockKey = CacheKeyGenerator.GenerateLockKey(testCase);
            lockKey.Should().StartWith("lock:");

            // Generate lock name key
            var nameKey = CacheKeyGenerator.GenerateLockNameKey(testCase);
            nameKey.Should().StartWith("lock:name:");

            // Verify deterministic
            var lockKey2 = CacheKeyGenerator.GenerateLockKey(testCase);
            lockKey.Should().Be(lockKey2);

            var nameKey2 = CacheKeyGenerator.GenerateLockNameKey(testCase);
            nameKey.Should().Be(nameKey2);
        }
    }

    /// <summary>
    /// Tests that distinct resources produce distinct keys.
    /// </summary>
    [Fact]
    public void DistinctResources_ProduceDistinctKeys()
    {
        // Arrange
        var resources = new[]
        {
            "resource-a",
            "resource-b",
            "resource-c",
            "different-resource",
            "lock-1",
            "lock-2",
            "metrics-lock",
            "status-lock"
        };

        var keys = new List<string>();

        // Act - generate keys for all resources
        foreach (var resource in resources)
        {
            keys.Add(CacheKeyGenerator.GenerateLockKey(resource));
        }

        // Assert - all keys should be distinct
        keys.Should().HaveCount(resources.Length);
        keys.Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// Tests that CacheKeySets.GetKeysByAcquisition returns the expected keys for a lock acquisition.
    /// </summary>
    [Fact]
    public void CacheKeySets_GetKeysByAcquisition_ReturnsExpectedKeys()
    {
        // Arrange
        var lockId = "test-lock";
        var ownerId = "test-owner";
        var expectedKeys = new[]
        {
            "lock:test-lock",
            "lock:owner:test-owner",
            "metrics:system",
            "status:test-lock"
        };

        // Act
        var result = CacheKeySets.GetKeysByAcquisition(lockId, ownerId);

        // Assert
        result.Should().BeEquivalentTo(expectedKeys);
    }

    /// <summary>
    /// Tests that CacheKeySets.GetKeysByRelease returns the expected keys for a lock release.
    /// </summary>
    [Fact]
    public void CacheKeySets_GetKeysByRelease_ReturnsExpectedKeys()
    {
        // Arrange
        var lockId = "test-lock";
        var ownerId = "test-owner";
        var expectedKeys = new[]
        {
            "lock:test-lock",
            "lock:owner:test-owner",
            "status:test-lock"
        };

        // Act
        var result = CacheKeySets.GetKeysByRelease(lockId, ownerId);

        // Assert
        result.Should().BeEquivalentTo(expectedKeys);
    }
}