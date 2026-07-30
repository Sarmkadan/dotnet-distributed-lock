using System;
using System.Threading.Tasks;
using Xunit;
using SarmKadan.DistributedLock.Utilities.Helpers;

namespace SarmKadan.DistributedLock.Tests;

public class RetryPolicyHelperTests
{
    [Fact]
    public async Task ExecuteWithRetryAsync_HappyPath_ReturnsResult()
    {
        var expected = 42;
        var result = await RetryPolicyHelper.ExecuteWithRetryAsync(
            () => Task.FromResult(expected));

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_RetriesAndSucceeds()
    {
        var attempts = 0;
        var result = await RetryPolicyHelper.ExecuteWithRetryAsync<int>(
            () =>
            {
                attempts++;
                if (attempts < 3)
                    throw new InvalidOperationException("Transient failure");
                return Task.FromResult(99);
            },
            maxRetries: 5,
            initialDelayMs: 1); // keep delay tiny for test speed

        Assert.Equal(99, result);
        Assert.Equal(3, attempts); // two failures then success
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_ExceedsMaxRetries_Throws()
    {
        var attempts = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await RetryPolicyHelper.ExecuteWithRetryAsync<int>(
                () =>
                {
                    attempts++;
                    throw new InvalidOperationException("Always fails");
                },
                maxRetries: 2,
                initialDelayMs: 1);
        });

        // attempts should be maxRetries + 1 (initial try + retries)
        Assert.Equal(3, attempts);
    }

    [Fact]
    public void ExecuteWithRetry_HappyPath_ReturnsResult()
    {
        var expected = "ok";
        var result = RetryPolicyHelper.ExecuteWithRetry(
            () => expected);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExecuteWithRetry_RetriesAndSucceeds()
    {
        var attempts = 0;
        var result = RetryPolicyHelper.ExecuteWithRetry<string>(
            () =>
            {
                attempts++;
                if (attempts < 2)
                    throw new InvalidOperationException();
                return "done";
            },
            maxRetries: 3,
            initialDelayMs: 1);

        Assert.Equal("done", result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public void ExecuteWithRetry_ExceedsMaxRetries_Throws()
    {
        var attempts = 0;
        Assert.Throws<InvalidOperationException>(() =>
        {
            RetryPolicyHelper.ExecuteWithRetry<int>(
                () =>
                {
                    attempts++;
                    throw new InvalidOperationException();
                },
                maxRetries: 1,
                initialDelayMs: 1);
        });

        // initial try + one retry
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ExecuteWithLinearRetryAsync_HappyPath_ReturnsResult()
    {
        var attempts = 0;
        var result = await RetryPolicyHelper.ExecuteWithLinearRetryAsync<int>(
            () =>
            {
                attempts++;
                if (attempts < 2)
                    throw new InvalidOperationException();
                return Task.FromResult(7);
            },
            maxRetries: 3,
            delayIncrementMs: 1);

        Assert.Equal(7, result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public void CreatePolicy_ReturnsConfiguredValues()
    {
        var policy = RetryPolicyHelper.CreatePolicy(maxRetries: 5, initialDelayMs: 250, backoffMultiplier: 1.8);

        Assert.Equal(5, policy.MaxRetries);
        Assert.Equal(250, policy.InitialDelayMs);
        Assert.Equal(1.8, policy.BackoffMultiplier);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_NullOperation_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await RetryPolicyHelper.ExecuteWithRetryAsync<int>(null!);
        });
    }

    [Fact]
    public void ExecuteWithRetry_NullOperation_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            RetryPolicyHelper.ExecuteWithRetry<int>(null!);
        });
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_ShouldRetryPredicateStopsRetry()
    {
        var attempts = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await RetryPolicyHelper.ExecuteWithRetryAsync<int>(
                () =>
                {
                    attempts++;
                    throw new InvalidOperationException("stop");
                },
                maxRetries: 5,
                shouldRetry: ex => ex.Message.Contains("different"));
        });

        // Should stop after first attempt because predicate returns false
        Assert.Equal(1, attempts);
    }
}
