#nullable enable
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SarmKadan.DistributedLock.Api.Controllers;
using SarmKadan.DistributedLock.Configuration;
using SarmKadan.DistributedLock.Services;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Unit tests for <see cref="MetricsController"/> covering the response shape of each GET action,
/// the not-found and validation rules of the lock-scoped action, and the 503 contract when the metrics store fails.
/// </summary>
public class MetricsControllerTests
{
    private delegate bool TryGetLockMetricsHandler(string lockName, out LockMetricsSnapshot? metrics);

    private readonly Mock<IMetricsStore> _store = new(MockBehavior.Strict);

    /// <summary>
    /// The system-wide endpoint aggregates every tracked lock: totals are summed, the success rate is computed
    /// from those totals, and the hold time is the mean of the per-lock averages.
    /// </summary>
    [Fact]
    public void GetSystemMetrics_WithTrackedLocks_ReturnsAggregatedResponse()
    {
        SetupAllLocks(
            ("orders", Snapshot(attempts: 10, successes: 8, averageHoldMs: 100)),
            ("billing", Snapshot(attempts: 5, successes: 5, averageHoldMs: 50)));

        var result = CreateController().GetSystemMetrics();

        var body = OkValue<SystemMetricsResponse>(result);
        body.TotalLockOperations.Should().Be(15);
        body.SuccessfulAcquisitions.Should().Be(13);
        body.FailedAcquisitions.Should().Be(2);
        body.SuccessRate.Should().BeApproximately(86.67, 0.01);
        body.AverageHoldTimeMs.Should().BeApproximately(75, 0.0001);
        body.ActiveLocks.Should().Be(2);
        body.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// With no tracked locks there is nothing to divide by, so every figure is zero rather than NaN.
    /// </summary>
    [Fact]
    public void GetSystemMetrics_NoTrackedLocks_ReturnsZeroedResponse()
    {
        SetupAllLocks();

        var result = CreateController().GetSystemMetrics();

        var body = OkValue<SystemMetricsResponse>(result);
        body.TotalLockOperations.Should().Be(0);
        body.SuccessfulAcquisitions.Should().Be(0);
        body.FailedAcquisitions.Should().Be(0);
        body.SuccessRate.Should().Be(0);
        body.AverageHoldTimeMs.Should().Be(0);
        body.ActiveLocks.Should().Be(0);
    }

    /// <summary>
    /// A failing store must surface as 503 with a message, not as an exception escaping the action.
    /// </summary>
    [Fact]
    public void GetSystemMetrics_StoreFails_Returns503()
    {
        _store.Setup(s => s.GetAllLockMetrics()).Throws(new InvalidOperationException("store offline"));

        var result = CreateController().GetSystemMetrics();

        AssertServiceUnavailable(result.Result);
    }

    /// <summary>
    /// The performance endpoint reports the mean, median, 95th/99th percentiles, and maximum across
    /// per-lock hold times, regardless of the order the store returns them in.
    /// </summary>
    [Fact]
    public void GetPerformanceMetrics_WithTrackedLocks_ReturnsPercentilesFromSortedHoldTimes()
    {
        // Nine locks, supplied unsorted. Sorted: 10 20 30 40 50 60 70 80 90.
        SetupAllLocks(
            ("l90", Snapshot(averageHoldMs: 90)),
            ("l10", Snapshot(averageHoldMs: 10)),
            ("l50", Snapshot(averageHoldMs: 50)),
            ("l30", Snapshot(averageHoldMs: 30)),
            ("l70", Snapshot(averageHoldMs: 70)),
            ("l20", Snapshot(averageHoldMs: 20)),
            ("l80", Snapshot(averageHoldMs: 80)),
            ("l40", Snapshot(averageHoldMs: 40)),
            ("l60", Snapshot(averageHoldMs: 60)));

        var result = CreateController().GetPerformanceMetrics();

        var body = OkValue<PerformanceMetricsResponse>(result);
        body.AverageAcquisitionTimeMs.Should().BeApproximately(50, 0.0001);
        body.MedianAcquisitionTimeMs.Should().Be(50);
        body.P95AcquisitionTimeMs.Should().Be(90);
        body.P99AcquisitionTimeMs.Should().Be(90);
        body.MaxAcquisitionTimeMs.Should().Be(90);
        body.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// With no tracked locks the performance figures are all zero.
    /// </summary>
    [Fact]
    public void GetPerformanceMetrics_NoTrackedLocks_ReturnsZeroedResponse()
    {
        SetupAllLocks();

        var result = CreateController().GetPerformanceMetrics();

        var body = OkValue<PerformanceMetricsResponse>(result);
        body.AverageAcquisitionTimeMs.Should().Be(0);
        body.MedianAcquisitionTimeMs.Should().Be(0);
        body.P95AcquisitionTimeMs.Should().Be(0);
        body.P99AcquisitionTimeMs.Should().Be(0);
        body.MaxAcquisitionTimeMs.Should().Be(0);
    }

    /// <summary>
    /// A failing store must surface as 503 with a message, not as an exception escaping the action.
    /// </summary>
    [Fact]
    public void GetPerformanceMetrics_StoreFails_Returns503()
    {
        _store.Setup(s => s.GetAllLockMetrics()).Throws(new InvalidOperationException("store offline"));

        var result = CreateController().GetPerformanceMetrics();

        AssertServiceUnavailable(result.Result);
    }

    /// <summary>
    /// A known lock is echoed back by name with its counters, derived success rate, hold-time figures,
    /// contention count, and last acquisition time.
    /// </summary>
    [Fact]
    public void GetLockMetrics_KnownLock_ReturnsPopulatedResponse()
    {
        var lastAcquired = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        SetupLock("orders", Snapshot(
            attempts: 8,
            successes: 6,
            averageHoldMs: 42.5,
            maxHoldMs: 300,
            contentionCount: 3,
            lastAcquired: lastAcquired));

        var result = CreateController().GetLockMetrics("orders");

        var body = OkValue<LockMetricsResponse>(result);
        body.LockName.Should().Be("orders");
        body.AcquisitionAttempts.Should().Be(8);
        body.SuccessfulAcquisitions.Should().Be(6);
        body.FailedAcquisitions.Should().Be(2);
        body.SuccessRate.Should().BeApproximately(75, 0.0001);
        body.AverageHoldTimeMs.Should().BeApproximately(42.5, 0.0001);
        body.MaxHoldTimeMs.Should().Be(300);
        body.TotalContentionEvents.Should().Be(3);
        body.LastAcquiredAt.Should().Be(lastAcquired);
        body.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// A lock that has been registered but never attempted reports a zero success rate rather than dividing by zero.
    /// </summary>
    [Fact]
    public void GetLockMetrics_LockWithNoAttempts_ReturnsZeroSuccessRate()
    {
        SetupLock("idle", Snapshot(attempts: 0, successes: 0));

        var result = CreateController().GetLockMetrics("idle");

        var body = OkValue<LockMetricsResponse>(result);
        body.SuccessRate.Should().Be(0);
        body.LastAcquiredAt.Should().BeNull();
    }

    /// <summary>
    /// A lock name the store has never seen is answered with 404 rather than an empty or zeroed body,
    /// so callers can tell "no such lock" apart from "a lock with no activity".
    /// </summary>
    [Fact]
    public void GetLockMetrics_UnknownLock_Returns404()
    {
        SetupLock("missing", null);

        var result = CreateController().GetLockMetrics("missing");

        var notFound = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.Value.Should().Be("No metrics found for lock: missing");
    }

    /// <summary>
    /// Names that fail the lock-name rule are rejected with 400 before the store is consulted.
    /// </summary>
    /// <param name="lockName">A route value that is not a valid lock name.</param>
    [Theory]
    [InlineData("lock name")]
    [InlineData("lock$name")]
    [InlineData("lock+name")]
    public void GetLockMetrics_InvalidLockName_Returns400WithoutQueryingStore(string lockName)
    {
        var result = CreateController().GetLockMetrics(lockName);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _store.Verify(
            s => s.TryGetLockMetrics(It.IsAny<string>(), out It.Ref<LockMetricsSnapshot?>.IsAny),
            Times.Never);
    }

    /// <summary>
    /// The 256-character limit is enforced: a 257-character name is rejected with 400.
    /// </summary>
    [Fact]
    public void GetLockMetrics_LockNameOverMaxLength_Returns400()
    {
        var result = CreateController().GetLockMetrics(new string('a', 257));

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    /// <summary>
    /// A failing store must surface as 503 with a message, not as an exception escaping the action.
    /// </summary>
    [Fact]
    public void GetLockMetrics_StoreFails_Returns503()
    {
        _store.Setup(s => s.TryGetLockMetrics("orders", out It.Ref<LockMetricsSnapshot?>.IsAny))
            .Throws(new InvalidOperationException("store offline"));

        var result = CreateController().GetLockMetrics("orders");

        AssertServiceUnavailable(result.Result);
    }

    private MetricsController CreateController() =>
        new(_store.Object, NullLogger<MetricsController>.Instance, new DistributedLockOptions());

    private void SetupAllLocks(params (string Name, LockMetricsSnapshot Snapshot)[] locks) =>
        _store.Setup(s => s.GetAllLockMetrics())
            .Returns(locks.ToDictionary(l => l.Name, l => l.Snapshot));

    private void SetupLock(string lockName, LockMetricsSnapshot? snapshot) =>
        _store.Setup(s => s.TryGetLockMetrics(lockName, out It.Ref<LockMetricsSnapshot?>.IsAny))
            .Returns(new TryGetLockMetricsHandler((string _, out LockMetricsSnapshot? metrics) =>
            {
                metrics = snapshot;
                return snapshot is not null;
            }));

    private static T OkValue<T>(ActionResult<T> result) =>
        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<T>().Which;

    private static void AssertServiceUnavailable(IActionResult? result)
    {
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    private static LockMetricsSnapshot Snapshot(
        long attempts = 0,
        long successes = 0,
        double averageHoldMs = 0,
        long maxHoldMs = 0,
        long contentionCount = 0,
        DateTime? lastAcquired = null) => new()
    {
        AcquisitionAttempts = attempts,
        SuccessfulAcquisitions = successes,
        FailedAcquisitions = attempts - successes,
        AverageHoldTimeMs = averageHoldMs,
        MaxHoldTimeMs = maxHoldMs,
        ContentionCount = contentionCount,
        LastAcquisitionTime = lastAcquired
    };
}
