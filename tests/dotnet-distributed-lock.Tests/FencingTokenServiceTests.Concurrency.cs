#nullable enable

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Services;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Concurrency tests for the <see cref="FencingTokenService"/> class.
/// Tests that token generation is strictly monotonic under high concurrency.
/// </summary>
public sealed class FencingTokenServiceConcurrencyTests
{
    private readonly FencingTokenService _service = new(NullLogger<FencingTokenService>.Instance);

    // -------------------------------------------------------------------------
    // Concurrency Tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests that issuing 100 tokens in parallel produces 100 unique strictly-increasing sequence numbers.
    /// </summary>
    [Fact]
    public void IssueToken_100ParallelRequests_ProducesUniqueStrictlyIncreasingTokens()
    {
        // Arrange
        var lockKey = "resource:concurrent-test";
        var tasks = new Task<FencingToken>[100];
        var results = new long[100];

        // Act - issue 100 tokens in parallel
        for (var i = 0; i < 100; i++)
        {
            var taskIndex = i;
            tasks[i] = Task.Run(() =>
            {
                var token = _service.IssueToken(lockKey);
                results[taskIndex] = token.SequenceNumber;
                return token;
            });
        }

        // Wait for all tasks to complete
        Task.WaitAll(tasks);

        // Assert - all sequence numbers should be unique and strictly increasing
        results.Should().HaveCount(100);
        results.Should().OnlyHaveUniqueItems();
        results.Should().BeInAscendingOrder();

        // Verify the last token has sequence number 100
        var lastToken = tasks[99].Result;
        lastToken.SequenceNumber.Should().Be(100);
    }

    /// <summary>
    /// Tests that issuing tokens for different resources in parallel maintains strict monotonicity.
    /// </summary>
    [Fact]
    public void IssueToken_ParallelForDifferentResources_ProducesStrictlyIncreasingSequence()
    {
        // Arrange
        var resourceKeys = new[] { "resource:A", "resource:B", "resource:C", "resource:D", "resource:E" };
        var tasks = new Task<FencingToken>[50];
        var results = new Dictionary<string, List<long>>(StringComparer.Ordinal);

        foreach (var key in resourceKeys)
        {
            results[key] = new List<long>();
        }

        // Act - issue tokens for different resources in parallel
        for (var i = 0; i < 50; i++)
        {
            var taskIndex = i;
            tasks[i] = Task.Run(() =>
            {
                var resourceKey = resourceKeys[taskIndex % resourceKeys.Length];
                var token = _service.IssueToken(resourceKey);
                lock (results)
                {
                    results[resourceKey].Add(token.SequenceNumber);
                }
                return token;
            });
        }

        // Wait for all tasks to complete
        Task.WaitAll(tasks);

        // Assert - each resource should have strictly increasing sequence numbers
        foreach (var resourceKey in resourceKeys)
        {
            var sequenceNumbers = results[resourceKey];
            sequenceNumbers.Should().HaveCountGreaterThan(0);
            sequenceNumbers.Should().BeInAscendingOrder("Sequence numbers for {0} should be strictly increasing", resourceKey);
        }
    }

    /// <summary>
    /// Tests that IncrementToken is also thread-safe and produces strictly monotonic sequence numbers.
    /// </summary>
    [Fact]
    public void IncrementToken_100ParallelRequests_ProducesUniqueStrictlyIncreasingTokens()
    {
        // Arrange
        var lockKey = "resource:increment-test";
        _service.IssueToken(lockKey); // Initial token

        var tasks = new Task<FencingToken>[100];
        var results = new long[100];

        // Act - increment token 100 times in parallel
        for (var i = 0; i < 100; i++)
        {
            var taskIndex = i;
            tasks[i] = Task.Run(() =>
            {
                var token = _service.IncrementToken(lockKey);
                results[taskIndex] = token.SequenceNumber;
                return token;
            });
        }

        // Wait for all tasks to complete
        Task.WaitAll(tasks);

        // Assert - all sequence numbers should be unique and strictly increasing
        results.Should().HaveCount(100);
        results.Should().OnlyHaveUniqueItems();
        results.Should().BeInAscendingOrder();

        // Verify the last token has sequence number 101 (1 initial + 100 increments)
        var lastToken = tasks[99].Result;
        lastToken.SequenceNumber.Should().Be(101);
    }
}
