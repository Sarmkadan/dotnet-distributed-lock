using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SarmKadan.DistributedLock.Core.Exceptions;
using SarmKadan.DistributedLock.Services;
using SarmKadan.DistributedLock.Workers;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

public class LockRenewalWorkerTests
{
    private static LockRenewalWorker CreateWorker()
    {
        var lockServiceMock = new Mock<ILockService>();
        // The worker only uses the lock service inside RenewLockAsync, which is not exercised
        // in the happy‑path tests for the public API, so a simple mock is sufficient.
        return new LockRenewalWorker(
            lockServiceMock.Object,
            NullLogger<LockRenewalWorker>.Instance);
    }

    [Fact]
    public void Constructor_NullLockService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LockRenewalWorker(
                null!,
                NullLogger<LockRenewalWorker>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var lockServiceMock = new Mock<ILockService>();
        Assert.Throws<ArgumentNullException>(() =>
            new LockRenewalWorker(
                lockServiceMock.Object,
                null!));
    }

    [Fact]
    public void RegisterForRenewal_And_IsRegisteredForRenewal_HappyPath()
    {
        var worker = CreateWorker();

        const string lockId = "lock-123";
        const ulong token = 42;
        var interval = TimeSpan.FromSeconds(30);

        worker.RegisterForRenewal(lockId, token, interval);

        Assert.True(worker.IsRegisteredForRenewal(lockId));
    }

    [Fact]
    public void RegisterForRenewal_NullLockId_ThrowsArgumentNullException()
    {
        var worker = CreateWorker();

        Assert.Throws<ArgumentNullException>(() =>
            worker.RegisterForRenewal(null!, 1, TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void IsRegisteredForRenewal_NullOrWhiteSpace_ThrowsArgumentException()
    {
        var worker = CreateWorker();

        Assert.Throws<ArgumentException>(() => worker.IsRegisteredForRenewal(null!));
        Assert.Throws<ArgumentException>(() => worker.IsRegisteredForRenewal(string.Empty));
        Assert.Throws<ArgumentException>(() => worker.IsRegisteredForRenewal("   "));
    }

    [Fact]
    public void TryGetRenewalSchedule_ReturnsTrueAndCorrectSchedule()
    {
        var worker = CreateWorker();

        const string lockId = "lock-abc";
        const ulong token = 99;
        var interval = TimeSpan.FromSeconds(15);

        worker.RegisterForRenewal(lockId, token, interval);

        var found = worker.TryGetRenewalSchedule(lockId, out var schedule);

        Assert.True(found);
        Assert.NotNull(schedule);
        Assert.Equal(lockId, schedule!.LockId);
        Assert.Equal(token, schedule.FencingToken);
        Assert.Equal(interval, schedule.RenewalInterval);
    }

    [Fact]
    public void TryGetRenewalSchedule_NullOrWhiteSpace_ThrowsArgumentException()
    {
        var worker = CreateWorker();

        Assert.Throws<ArgumentException>(() => worker.TryGetRenewalSchedule(null!, out _));
        Assert.Throws<ArgumentException>(() => worker.TryGetRenewalSchedule(string.Empty, out _));
        Assert.Throws<ArgumentException>(() => worker.TryGetRenewalSchedule("   ", out _));
    }

    [Fact]
    public void GetTimeUntilNextRenewal_ReturnsApproximateInterval()
    {
        var worker = CreateWorker();

        const string lockId = "lock-timer";
        const ulong token = 123;
        var interval = TimeSpan.FromSeconds(5);

        var before = DateTime.UtcNow;
        worker.RegisterForRenewal(lockId, token, interval);
        var after = DateTime.UtcNow;

        var remaining = worker.GetTimeUntilNextRenewal(lockId);
        Assert.NotNull(remaining);

        // The remaining time should be between (interval - elapsed) and interval.
        var elapsed = after - before;
        var minExpected = interval - elapsed;
        Assert.InRange(remaining!.Value, minExpected, interval);
    }

    [Fact]
    public void GetTimeUntilNextRenewal_NullOrWhiteSpace_ThrowsArgumentException()
    {
        var worker = CreateWorker();

        Assert.Throws<ArgumentException>(() => worker.GetTimeUntilNextRenewal(null!));
        Assert.Throws<ArgumentException>(() => worker.GetTimeUntilNextRenewal(string.Empty));
        Assert.Throws<ArgumentException>(() => worker.GetTimeUntilNextRenewal("   "));
    }

    [Fact]
    public void UnregisterFromRenewal_RemovesSchedule()
    {
        var worker = CreateWorker();

        const string lockId = "to-remove";
        worker.RegisterForRenewal(lockId, 1, TimeSpan.FromSeconds(10));

        Assert.True(worker.IsRegisteredForRenewal(lockId));

        worker.UnregisterFromRenewal(lockId);

        Assert.False(worker.IsRegisteredForRenewal(lockId));
    }

    [Fact]
    public async Task StopAsync_CompletesWithoutException()
    {
        var worker = CreateWorker();

        // No need to start the background service; just ensure StopAsync can be called.
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RegisterForRenewal_ThenRenewalFails_ThrowsLockRenewalFailedException()
    {
        // Arrange a mock ILockService that throws when RenewLockAsync is called.
        var lockServiceMock = new Mock<ILockService>();
        lockServiceMock
            .Setup(s => s.RenewLockAsync(
                It.IsAny<string>(),
                It.IsAny<ulong>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("simulated failure"));

        var worker = new LockRenewalWorker(
            lockServiceMock.Object,
            NullLogger<LockRenewalWorker>.Instance,
            new LockRenewalWorkerOptions { CheckIntervalMs = 1 });

        const string lockId = "fail-lock";
        worker.RegisterForRenewal(lockId, 1, TimeSpan.FromMilliseconds(1));

        // Trigger the internal processing loop once.
        // We invoke the private ProcessRenewalsAsync via reflection to keep the test focused.
        var method = typeof(LockRenewalWorker).GetMethod(
            "ProcessRenewalsAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        var ex = await Assert.ThrowsAsync<LockRenewalFailedException>(async () =>
            await (Task)method.Invoke(worker, new object[] { CancellationToken.None })!);

        Assert.Contains(lockId, ex.Message);
    }
}
