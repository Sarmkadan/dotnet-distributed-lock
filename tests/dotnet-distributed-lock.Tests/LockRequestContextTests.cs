using Xunit;
using SarmKadan.DistributedLock.Models;
using SarmKadan.DistributedLock.Enums;
using System;

namespace SarmKadan.DistributedLock.Tests;

public class LockRequestContextTests
{
    [Fact]
    public void Constructor_Defaults_SetsExpectedValues()
    {
        var context = new LockRequestContext();

        Assert.NotEmpty(context.RequestId);
        Assert.Equal(string.Empty, context.LockKey);
        Assert.Equal(string.Empty, context.RequesterId);
        Assert.Equal(AcquisitionMode.Blocking, context.Mode);
        Assert.Equal(TimeSpan.FromSeconds(30), context.RequestedDuration); // Assuming default based on typical constants, or just check it's positive
        Assert.True(context.RequestedAt <= DateTime.UtcNow);
        Assert.False(context.Successful);
        Assert.Null(context.CompletedAt);
        Assert.Equal(0, context.RetryCount);
        Assert.Empty(context.CustomProperties);
    }

    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        var key = "test-lock";
        var requester = "requester-1";
        var mode = AcquisitionMode.Blocking;

        var context = new LockRequestContext(key, requester, mode);

        Assert.Equal(key, context.LockKey);
        Assert.Equal(requester, context.RequesterId);
        Assert.Equal(mode, context.Mode);
        // Ensure defaults from parameterless constructor are still applied
        Assert.NotEmpty(context.RequestId);
    }

    [Fact]
    public void Constructor_WithInvalidParameters_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new LockRequestContext("", "requester"));
        Assert.Throws<ArgumentException>(() => new LockRequestContext(" ", "requester"));
        Assert.Throws<ArgumentException>(() => new LockRequestContext(null!, "requester"));
        
        Assert.Throws<ArgumentException>(() => new LockRequestContext("key", ""));
        Assert.Throws<ArgumentException>(() => new LockRequestContext("key", " "));
        Assert.Throws<ArgumentException>(() => new LockRequestContext("key", null!));
    }

    [Fact]
    public void MarkCompleted_SetsCompletionStatusAndTime()
    {
        var context = new LockRequestContext("key", "req");
        
        Assert.False(context.Successful);
        Assert.Null(context.CompletedAt);

        context.MarkCompleted(true, "All good");

        Assert.True(context.Successful);
        Assert.NotNull(context.CompletedAt);
        Assert.Equal("All good", context.FailureReason);
    }

    [Fact]
    public void MarkCompleted_WithFailure_SetsFailureReason()
    {
        var context = new LockRequestContext("key", "req");
        const string reason = "Lock contention";

        context.MarkCompleted(false, reason);

        Assert.False(context.Successful);
        Assert.Equal(reason, context.FailureReason);
    }

    [Fact]
    public void Duration_CalculatesTimeSpanCorrectly()
    {
        var context = new LockRequestContext("key", "req");
        
        // Allow a tiny delay to ensure duration is measurable
        System.Threading.Thread.Sleep(10);
        
        var durationBeforeCompletion = context.Duration;
        Assert.True(durationBeforeCompletion.TotalMilliseconds > 0);

        context.MarkCompleted(true);
        var durationAfterCompletion = context.Duration;

        Assert.True(durationAfterCompletion >= durationBeforeCompletion);
    }

    [Fact]
    public void CustomProperties_AddAndGet_WorksCorrectly()
    {
        var context = new LockRequestContext("key", "req");

        context.AddProperty("CustomKey", 123);
        context.AddProperty("AnotherKey", "value");

        Assert.Equal(123, context.GetProperty("CustomKey"));
        Assert.Equal("value", context.GetProperty("AnotherKey"));
        Assert.Null(context.GetProperty("NonExistentKey"));
    }

    [Fact]
    public void ContextHelpers_UpdatePropertiesCorrectly()
    {
        var context = new LockRequestContext("key", "req");

        context.SetCorrelationId("corr-123");
        context.SetUserContext("user-456", "sess-789");
        context.IncrementRetryCount();
        context.IncrementRetryCount();

        Assert.Equal("corr-123", context.CorrelationId);
        Assert.Equal("user-456", context.UserId);
        Assert.Equal("sess-789", context.SessionId);
        Assert.Equal(2, context.RetryCount);
    }
}
