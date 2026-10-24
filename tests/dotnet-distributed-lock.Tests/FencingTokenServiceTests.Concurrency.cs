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
    public async Task IssueToken_100ParallelRequests_ProducesUniqueStrictlyIncreasingTokens()
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
        await Task.WhenAll(tasks);

        // Assert - results are collected by task index, not completion order, so check the set:
        // every sequence number 1..100 is issued exactly once.
        results.Should().HaveCount(100);
        results.Should().OnlyHaveUniqueItems();
        results.Should().BeEquivalentTo(Enumerable.Range(1, 100).Select(i => (long)i));

        // Verify the highest sequence number issued is 100
        results.Max().Should().Be(100);
    }

    /// <summary>
    /// Tests that issuing tokens for different resources in parallel maintains strict monotonicity.
    /// </summary>
    [Fact]
    public async Task IssueToken_ParallelForDifferentResources_ProducesStrictlyIncreasingSequence()
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
        await Task.WhenAll(tasks);

        // Assert - each resource should issue every sequence number 1..n exactly once.
        // Completion order across threads is not deterministic, so compare as a set.
        foreach (var resourceKey in resourceKeys)
        {
            var sequenceNumbers = results[resourceKey];
            sequenceNumbers.Should().HaveCountGreaterThan(0);
            sequenceNumbers.Should().OnlyHaveUniqueItems("Sequence numbers for {0} should be unique", resourceKey);
            sequenceNumbers.Should().BeEquivalentTo(
                Enumerable.Range(1, sequenceNumbers.Count).Select(i => (long)i),
                "Sequence numbers for {0} should be contiguous from 1", resourceKey);
        }
    }

    /// <summary>
    /// Tests that IncrementToken is also thread-safe and produces strictly monotonic sequence numbers.
    /// </summary>
    [Fact]
    public async Task IncrementToken_100ParallelRequests_ProducesUniqueStrictlyIncreasingTokens()
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
        await Task.WhenAll(tasks);

        // Assert - results are collected by task index, not completion order, so check the set:
        // sequence numbers 2..101 (initial token 1, then 100 increments) are each issued exactly once.
        results.Should().HaveCount(100);
        results.Should().OnlyHaveUniqueItems();
        results.Should().BeEquivalentTo(Enumerable.Range(2, 100).Select(i => (long)i));

        // Verify the highest sequence number issued is 101 (1 initial + 100 increments)
        results.Max().Should().Be(101);
    }
}
