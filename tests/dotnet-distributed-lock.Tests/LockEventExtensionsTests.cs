#nullable enable
using FluentAssertions;
using SarmKadan.DistributedLock.Events;
using SarmKadan.DistributedLock.Enums;
using Xunit;

namespace SarmKadan.DistributedLock.Tests;

/// <summary>
/// Unit tests for <see cref="LockEventExtensions"/> class.
/// Tests all extension methods for LockEvent types.
/// </summary>
public class LockEventExtensionsTests
{
    // Test data
    private const string TestLockId = "test-lock-123";
    private const string TestLockName = "test-lock-name";
    private const string TestOwnerId = "test-owner-456";
    private const string TestReason = "Test failure reason";
    private const string TestRequesterId = "test-requester-789";

    // Helper methods to create test events
    private static LockAcquiredEvent CreateLockAcquiredEvent(LockStatus status = LockStatus.Held)
    {
        return new LockAcquiredEvent
        {
            LockId = TestLockId,
            LockName = TestLockName,
            OwnerId = TestOwnerId,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            FencingToken = 12345,
            Duration = TimeSpan.FromSeconds(30),
            Status = status
        };
    }

    private static LockReleasedEvent CreateLockReleasedEvent()
    {
        var acquiredAt = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(5));
        return new LockReleasedEvent
        {
            LockId = TestLockId,
            LockName = TestLockName,
            OwnerId = TestOwnerId,
            AcquiredAt = acquiredAt,
            ReleasedAt = DateTime.UtcNow,
            HeldDuration = TimeSpan.FromMinutes(5)
        };
    }

    private static LockExpiredEvent CreateLockExpiredEvent()
    {
        return new LockExpiredEvent
        {
            LockId = TestLockId,
            LockName = TestLockName,
            OwnerId = TestOwnerId,
            ExpiredAt = DateTime.UtcNow,
            TotalDuration = TimeSpan.FromHours(2)
        };
    }

    private static LockRenewedEvent CreateLockRenewedEvent()
    {
        var previousExpiresAt = DateTime.UtcNow.Subtract(TimeSpan.FromHours(1));
        return new LockRenewedEvent
        {
            LockId = TestLockId,
            LockName = TestLockName,
            OwnerId = TestOwnerId,
            PreviousExpiresAt = previousExpiresAt,
            NewExpiresAt = DateTime.UtcNow.AddHours(1),
            RenewedDuration = TimeSpan.FromMinutes(10)
        };
    }

    private static LockFailedEvent CreateLockFailedEvent()
    {
        return new LockFailedEvent(TestLockId, TestOwnerId, TestReason);
    }

    private static LockAcquisitionFailedEvent CreateLockAcquisitionFailedEvent()
    {
        return new LockAcquisitionFailedEvent
        {
            LockName = TestLockName,
            RequesterId = TestRequesterId,
            Reason = TestReason
        };
    }

    private static LockContentionEvent CreateLockContentionEvent()
    {
        return new LockContentionEvent
        {
            LockName = TestLockName,
            ContentionLevel = 3,
            ContentionDetectedAt = DateTime.UtcNow,
            CompetingParties = new List<string> { "party1", "party2" }
        };
    }

    private static LockPerformanceEvent CreateLockPerformanceEvent()
    {
        return new LockPerformanceEvent
        {
            LockId = TestLockId,
            OperationType = "Acquire",
            DurationMs = 1500,
            ThresholdMs = 1000
        };
    }

    private static LockErrorEvent CreateLockErrorEvent()
    {
        return new LockErrorEvent
        {
            LockId = TestLockId,
            OperationType = "Acquire",
            ErrorMessage = "Test error"
        };
    }

    // IsAcquisitionSuccessful Tests
    [Fact]
    public void IsAcquisitionSuccessful_ReturnsTrue_ForAcquiredLock()
    {
        // Arrange
        var @event = CreateLockAcquiredEvent(LockStatus.Acquired);

        // Act
        var result = @event.IsAcquisitionSuccessful();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAcquisitionSuccessful_ReturnsFalse_ForHeldLock()
    {
        // Arrange
        var @event = CreateLockAcquiredEvent(LockStatus.Held);

        // Act
        var result = @event.IsAcquisitionSuccessful();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsAcquisitionSuccessful_ReturnsFalse_ForNonAcquisitionEvents()
    {
        // Arrange
        var @event = CreateLockReleasedEvent();

        // Act
        var result = @event.IsAcquisitionSuccessful();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsAcquisitionSuccessful_ThrowsArgumentNullException_ForNullEvent()
    {
        // Arrange
        LockEvent? @event = null;

        // Act
        Action act = () => @event!.IsAcquisitionSuccessful();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // IsFailure Tests
    [Fact]
    public void IsFailure_ReturnsTrue_ForLockFailedEvent()
    {
        // Arrange
        var @event = CreateLockFailedEvent();

        // Act
        var result = @event.IsFailure();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsFailure_ReturnsTrue_ForLockAcquisitionFailedEvent()
    {
        // Arrange
        var @event = CreateLockAcquisitionFailedEvent();

        // Act
        var result = @event.IsFailure();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsFailure_ReturnsFalse_ForSuccessfulEvents()
    {
        // Arrange
        var @event = CreateLockAcquiredEvent();

        // Act
        var result = @event.IsFailure();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsFailure_ThrowsArgumentNullException_ForNullEvent()
    {
        // Arrange
        LockEvent? @event = null;

        // Act
        Action act = () => @event!.IsFailure();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // GetLockId Tests
    [Fact]
    public void GetLockId_ReturnsLockId_ForLockAcquiredEvent()
    {
        // Arrange
        var @event = CreateLockAcquiredEvent();

        // Act
        var result = @event.GetLockId();

        // Assert
        result.Should().Be(TestLockId);
    }

    [Fact]
    public void GetLockId_ReturnsLockId_ForLockReleasedEvent()
    {
        // Arrange
        var @event = CreateLockReleasedEvent();

        // Act
        var result = @event.GetLockId();

        // Assert
        result.Should().Be(TestLockId);
    }

    [Fact]
    public void GetLockId_ReturnsLockName_ForLockAcquisitionFailedEvent()
    {
        // Arrange
        var @event = CreateLockAcquisitionFailedEvent();

        // Act
        var result = @event.GetLockId();

        // Assert
        result.Should().Be(TestLockName);
    }

    [Fact]
    public void GetLockId_ReturnsNull_ForUnsupportedEventTypes()
    {
        // Arrange
        var @event = new LockPerformanceEvent
        {
            LockId = TestLockId,
            OperationType = "Test",
            DurationMs = 100,
            ThresholdMs = 50
        };

        // Act
        var result = @event.GetLockId();

        // Assert
        result.Should().Be(TestLockId);
    }

    [Fact]
    public void GetLockId_ThrowsArgumentNullException_ForNullEvent()
    {
        // Arrange
        LockEvent? @event = null;

        // Act
        Action act = () => @event!.GetLockId();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // GetOwnerId Tests
    [Fact]
    public void GetOwnerId_ReturnsOwnerId_ForLockAcquiredEvent()
    {
        // Arrange
        var @event = CreateLockAcquiredEvent();

        // Act
        var result = @event.GetOwnerId();

        // Assert
        result.Should().Be(TestOwnerId);
    }

    [Fact]
    public void GetOwnerId_ReturnsOwnerId_ForLockReleasedEvent()
    {
        // Arrange
        var @event = CreateLockReleasedEvent();

        // Act
        var result = @event.GetOwnerId();

        // Assert
        result.Should().Be(TestOwnerId);
    }

    [Fact]
    public void GetOwnerId_ReturnsRequesterId_ForLockAcquisitionFailedEvent()
    {
        // Arrange
        var @event = CreateLockAcquisitionFailedEvent();

        // Act
        var result = @event.GetOwnerId();

        // Assert
        result.Should().Be(TestRequesterId);
    }

    [Fact]
    public void GetOwnerId_ReturnsFirstCompetingParty_ForLockContentionEvent()
    {
        // Arrange
        var @event = CreateLockContentionEvent();

        // Act
        var result = @event.GetOwnerId();

        // Assert
        result.Should().Be("party1");
    }

    [Fact]
    public void GetOwnerId_ReturnsNull_ForEmptyCompetingParties_List()
    {
        // Arrange
        var @event = new LockContentionEvent
        {
            LockName = TestLockName,
            ContentionLevel = 0,
            ContentionDetectedAt = DateTime.UtcNow,
            CompetingParties = new List<string>()
        };

        // Act
        var result = @event.GetOwnerId();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetOwnerId_ThrowsArgumentNullException_ForNullEvent()
    {
        // Arrange
        LockEvent? @event = null;

        // Act
        Action act = () => @event!.GetOwnerId();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // ToLogString Tests
    [Fact]
    public void ToLogString_IncludesTimestamp_ByDefault()
    {
        // Arrange
        var @event = CreateLockAcquiredEvent();
        var fixedTime = new DateTime(2026, 07, 22, 10, 30, 0, DateTimeKind.Utc);
        @event.OccurredAt = fixedTime;

        // Act
        var result = @event.ToLogString();

        // Assert
        result.Should().Contain($"Timestamp={fixedTime:O}");
        result.Should().Contain("EventType=LockAcquiredEvent");
        result.Should().Contain($"EventId={@event.EventId}");
    }

    [Fact]
    public void ToLogString_ExcludesTimestamp_WhenIncludeTimestampFalse()
    {
        // Arrange
        var @event = CreateLockAcquiredEvent();

        // Act
        var result = @event.ToLogString(includeTimestamp: false);

        // Assert
        result.Should().NotContain("Timestamp=");
        result.Should().Contain("EventType=LockAcquiredEvent");
        result.Should().Contain($"EventId={@event.EventId}");
    }

    [Fact]
    public void ToLogString_IncludesSourceSystem_WhenPresent()
    {
        // Arrange
        var @event = CreateLockAcquiredEvent();
        @event.SourceSystem = "test-system";

        // Act
        var result = @event.ToLogString();

        // Assert
        result.Should().Contain("SourceSystem=test-system");
    }

    [Fact]
    public void ToLogString_ExcludesSourceSystem_WhenNull()
    {
        // Arrange
        var @event = CreateLockAcquiredEvent();
        @event.SourceSystem = null!;

        // Act
        var result = @event.ToLogString();

        // Assert
        result.Should().NotContain("SourceSystem=");
    }

    [Fact]
    public void ToLogString_IncludesCorrelationId_WhenPresent()
    {
        // Arrange
        var @event = CreateLockAcquiredEvent();
        @event.CorrelationId = "test-correlation-123";

        // Act
        var result = @event.ToLogString();

        // Assert
        result.Should().Contain("CorrelationId=test-correlation-123");
    }

    [Fact]
    public void ToLogString_ExcludesCorrelationId_WhenNull()
    {
        // Arrange
        var @event = CreateLockAcquiredEvent();
        @event.CorrelationId = null!;

        // Act
        var result = @event.ToLogString();

        // Assert
        result.Should().NotContain("CorrelationId=");
    }

    [Fact]
    public void ToLogString_ThrowsArgumentNullException_ForNullEvent()
    {
        // Arrange
        LockEvent? @event = null;

        // Act
        Action act = () => @event!.ToLogString();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // ToFailureEvent Tests
    [Fact]
    public void ToFailureEvent_CreatesLockFailedEvent_WithCorrectProperties()
    {
        // Arrange
        var @event = CreateLockAcquiredEvent();
        const string reason = "Test failure";

        // Act
        var failureEvent = @event.ToFailureEvent(reason);

        // Assert
        failureEvent.Should().NotBeNull();
        failureEvent.LockId.Should().Be(TestLockId);
        failureEvent.OwnerId.Should().Be(TestOwnerId);
        failureEvent.Reason.Should().Be(reason);
        failureEvent.SourceSystem.Should().Be(@event.SourceSystem);
        failureEvent.CorrelationId.Should().Be(@event.CorrelationId);
        failureEvent.OccurredAt.Should().Be(@event.OccurredAt);
    }

    [Fact]
    public void ToFailureEvent_UsesEmptyStrings_WhenLockIdOrOwnerIdNull()
    {
        // Arrange
        var @event = CreateLockAcquisitionFailedEvent(); // Uses LockName instead of LockId
        const string reason = "Test failure";

        // Act
        var failureEvent = @event.ToFailureEvent(reason);

        // Assert
        failureEvent.Should().NotBeNull();
        failureEvent.LockId.Should().Be(TestLockName); // From GetLockId for acquisition failed
        failureEvent.OwnerId.Should().Be(TestRequesterId); // From GetOwnerId for acquisition failed
        failureEvent.Reason.Should().Be(reason);
    }

    [Fact]
    public void ToFailureEvent_ThrowsArgumentNullException_ForNullEvent()
    {
        // Arrange
        LockEvent? @event = null;
        const string reason = "Test failure";

        // Act
        Action act = () => @event!.ToFailureEvent(reason);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToFailureEvent_ThrowsArgumentException_ForNullOrEmptyReason()
    {
        // Arrange
        var @event = CreateLockAcquiredEvent();

        // Act & Assert
        Action act = () => @event.ToFailureEvent(null!);
        act.Should().Throw<ArgumentException>();

        act = () => @event.ToFailureEvent(string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    // IsWithinTimeRange Tests
    [Fact]
    public void IsWithinTimeRange_ReturnsTrue_WhenEventInRange()
    {
        // Arrange
        var @event = CreateLockAcquiredEvent();
        var eventTime = @event.OccurredAt;
        var startTime = eventTime.Subtract(TimeSpan.FromMinutes(5));
        var endTime = eventTime.Add(TimeSpan.FromMinutes(5));

        // Act
        var result = @event.IsWithinTimeRange(startTime, endTime);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsWithinTimeRange_ReturnsFalse_WhenEventBeforeRange()
    {
        // Arrange
        var @event = CreateLockAcquiredEvent();
        var eventTime = @event.OccurredAt;
        var startTime = eventTime.Add(TimeSpan.FromMinutes(5));
        var endTime = eventTime.Add(TimeSpan.FromMinutes(10));

        // Act
        var result = @event.IsWithinTimeRange(startTime, endTime);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsWithinTimeRange_ReturnsFalse_WhenEventAfterRange()
    {
        // Arrange
        var @event = CreateLockAcquiredEvent();
        var eventTime = @event.OccurredAt;
        var startTime = eventTime.Subtract(TimeSpan.FromMinutes(10));
        var endTime = eventTime.Subtract(TimeSpan.FromMinutes(5));

        // Act
        var result = @event.IsWithinTimeRange(startTime, endTime);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsWithinTimeRange_ReturnsTrue_AtBoundaryStart()
    {
        // Arrange
        var @event = CreateLockAcquiredEvent();
        var eventTime = @event.OccurredAt;
        var startTime = eventTime;
        var endTime = eventTime.Add(TimeSpan.FromMinutes(5));

        // Act
        var result = @event.IsWithinTimeRange(startTime, endTime);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsWithinTimeRange_ReturnsTrue_AtBoundaryEnd()
    {
        // Arrange
        var @event = CreateLockAcquiredEvent();
        var eventTime = @event.OccurredAt;
        var startTime = eventTime.Subtract(TimeSpan.FromMinutes(5));
        var endTime = eventTime;

        // Act
        var result = @event.IsWithinTimeRange(startTime, endTime);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsWithinTimeRange_ThrowsArgumentNullException_ForNullEvent()
    {
        // Arrange
        LockEvent? @event = null;
        var startTime = DateTime.UtcNow;
        var endTime = DateTime.UtcNow.AddHours(1);

        // Act
        Action act = () => @event!.IsWithinTimeRange(startTime, endTime);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsWithinTimeRange_ThrowsArgumentOutOfRangeException_WhenStartAfterEnd()
    {
        // Arrange
        var @event = CreateLockAcquiredEvent();
        var startTime = DateTime.UtcNow.AddHours(2);
        var endTime = DateTime.UtcNow.AddHours(1); // Earlier than start

        // Act
        Action act = () => @event.IsWithinTimeRange(startTime, endTime);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // GetDuration Tests
    [Fact]
    public void GetDuration_ReturnsDuration_ForLockAcquiredEvent()
    {
        // Arrange
        var expectedDuration = TimeSpan.FromSeconds(45);
        var @event = new LockAcquiredEvent
        {
            LockId = TestLockId,
            LockName = TestLockName,
            OwnerId = TestOwnerId,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            FencingToken = 12345,
            Duration = expectedDuration,
            Status = LockStatus.Held
        };

        // Act
        var result = @event.GetDuration();

        // Assert
        result.Should().Be(expectedDuration);
    }

    [Fact]
    public void GetDuration_ReturnsHeldDuration_ForLockReleasedEvent()
    {
        // Arrange
        var expectedDuration = TimeSpan.FromMinutes(10);
        var acquiredAt = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(5));
        var @event = new LockReleasedEvent
        {
            LockId = TestLockId,
            LockName = TestLockName,
            OwnerId = TestOwnerId,
            AcquiredAt = acquiredAt,
            ReleasedAt = DateTime.UtcNow,
            HeldDuration = expectedDuration
        };

        // Act
        var result = @event.GetDuration();

        // Assert
        result.Should().Be(expectedDuration);
    }

    [Fact]
    public void GetDuration_ReturnsTotalDuration_ForLockExpiredEvent()
    {
        // Arrange
        var expectedDuration = TimeSpan.FromHours(3);
        var @event = new LockExpiredEvent
        {
            LockId = TestLockId,
            LockName = TestLockName,
            OwnerId = TestOwnerId,
            ExpiredAt = DateTime.UtcNow,
            TotalDuration = expectedDuration
        };

        // Act
        var result = @event.GetDuration();

        // Assert
        result.Should().Be(expectedDuration);
    }

    [Fact]
    public void GetDuration_ReturnsRenewedDuration_ForLockRenewedEvent()
    {
        // Arrange
        var expectedDuration = TimeSpan.FromMinutes(15);
        var previousExpiresAt = DateTime.UtcNow.Subtract(TimeSpan.FromHours(1));
        var @event = new LockRenewedEvent
        {
            LockId = TestLockId,
            LockName = TestLockName,
            OwnerId = TestOwnerId,
            PreviousExpiresAt = previousExpiresAt,
            NewExpiresAt = DateTime.UtcNow.AddHours(1),
            RenewedDuration = expectedDuration
        };

        // Act
        var result = @event.GetDuration();

        // Assert
        result.Should().Be(expectedDuration);
    }

    [Fact]
    public void GetDuration_ReturnsZero_ForEventsWithoutDuration()
    {
        // Arrange
        var @event = CreateLockFailedEvent();

        // Act
        var result = @event.GetDuration();

        // Assert
        result.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void GetDuration_ThrowsArgumentNullException_ForNullEvent()
    {
        // Arrange
        LockEvent? @event = null;

        // Act
        Action act = () => @event!.GetDuration();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // GetExpirationTime Tests
    [Fact]
    public void GetExpirationTime_ReturnsExpiresAt_ForLockAcquiredEvent()
    {
        // Arrange
        var expectedTime = DateTime.UtcNow.AddHours(2);
        var @event = new LockAcquiredEvent
        {
            LockId = TestLockId,
            LockName = TestLockName,
            OwnerId = TestOwnerId,
            ExpiresAt = expectedTime,
            FencingToken = 12345,
            Duration = TimeSpan.FromSeconds(30),
            Status = LockStatus.Held
        };

        // Act
        var result = @event.GetExpirationTime();

        // Assert
        result.Should().Be(expectedTime);
    }

    [Fact]
    public void GetExpirationTime_ReturnsNewExpiresAt_ForLockRenewedEvent()
    {
        // Arrange
        var expectedTime = DateTime.UtcNow.AddHours(3);
        var previousExpiresAt = DateTime.UtcNow.Subtract(TimeSpan.FromHours(1));
        var @event = new LockRenewedEvent
        {
            LockId = TestLockId,
            LockName = TestLockName,
            OwnerId = TestOwnerId,
            PreviousExpiresAt = previousExpiresAt,
            NewExpiresAt = expectedTime,
            RenewedDuration = TimeSpan.FromMinutes(10)
        };

        // Act
        var result = @event.GetExpirationTime();

        // Assert
        result.Should().Be(expectedTime);
    }

    [Fact]
    public void GetExpirationTime_ReturnsNull_ForEventsWithoutExpiration()
    {
        // Arrange
        var @event = CreateLockReleasedEvent();

        // Act
        var result = @event.GetExpirationTime();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetExpirationTime_ThrowsArgumentNullException_ForNullEvent()
    {
        // Arrange
        LockEvent? @event = null;

        // Act
        Action act = () => @event!.GetExpirationTime();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // IsRelatedToLock Tests
    [Fact]
    public void IsRelatedToLock_ReturnsTrue_WhenLockIdMatches()
    {
        // Arrange
        var @event = new LockAcquiredEvent
        {
            LockId = TestLockId,
            LockName = TestLockName,
            OwnerId = TestOwnerId,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            FencingToken = 12345,
            Duration = TimeSpan.FromSeconds(30),
            Status = LockStatus.Held
        };

        // Act
        var result = @event.IsRelatedToLock(TestLockId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRelatedToLock_ReturnsTrue_WhenLockNameMatches()
    {
        // Arrange
        var @event = new LockAcquiredEvent
        {
            LockId = TestLockId,
            LockName = TestLockName,
            OwnerId = TestOwnerId,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            FencingToken = 12345,
            Duration = TimeSpan.FromSeconds(30),
            Status = LockStatus.Held
        };

        // Act
        var result = @event.IsRelatedToLock(TestLockName);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRelatedToLock_ReturnsTrue_CaseInsensitiveMatch()
    {
        // Arrange
        var @event = new LockAcquiredEvent
        {
            LockId = TestLockId.ToUpper(),
            LockName = TestLockName,
            OwnerId = TestOwnerId,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            FencingToken = 12345,
            Duration = TimeSpan.FromSeconds(30),
            Status = LockStatus.Held
        };

        // Act
        var result = @event.IsRelatedToLock(TestLockId.ToLower());

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRelatedToLock_ReturnsFalse_WhenNoMatch()
    {
        // Arrange
        var @event = CreateLockAcquiredEvent();

        // Act
        var result = @event.IsRelatedToLock("non-matching-lock");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRelatedToLock_ReturnsFalse_ForEventsWithoutLockInfo()
    {
        // Arrange
        var @event = new LockErrorEvent
        {
            LockId = TestLockId,
            OperationType = "Test",
            ErrorMessage = "Test error"
        };

        // Act
        var result = @event.IsRelatedToLock("different-lock");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRelatedToLock_ThrowsArgumentNullException_ForNullEvent()
    {
        // Arrange
        LockEvent? @event = null;

        // Act
        Action act = () => @event!.IsRelatedToLock(TestLockId);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsRelatedToLock_ThrowsArgumentException_ForNullOrEmptyLockName()
    {
        // Arrange
        var @event = CreateLockAcquiredEvent();

        // Act & Assert
        Action act = () => @event.IsRelatedToLock(null!);
        act.Should().Throw<ArgumentException>();

        act = () => @event.IsRelatedToLock(string.Empty);
        act.Should().Throw<ArgumentException>();
    }
}