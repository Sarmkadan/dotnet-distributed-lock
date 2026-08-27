using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock.Events;

namespace SarmKadan.DistributedLock.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for LockEventSubscriberValidation validation methods
/// </summary>
[MemoryDiagnoser]
public class LockEventSubscriberValidationBenchmarks
{
    private MetricsTrackingEventSubscriber _metricsSubscriber = null!;
    private EventMetrics _validMetrics = null!;
    private EventMetrics _invalidMetrics = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Create mock loggers
        var metricsLogger = new MockLogger<MetricsTrackingEventSubscriber>();
        var loggingLogger = new MockLogger<LoggingLockEventSubscriber>();

        // Create metrics subscriber
        _metricsSubscriber = new MetricsTrackingEventSubscriber(metricsLogger);

        // Create valid metrics
        _validMetrics = new EventMetrics
        {
            Acquisitions = 100,
            Releases = 95,
            Failures = 5,
            ContentionEvents = 10,
            Timestamp = DateTime.UtcNow
        };

        // Create invalid metrics (negative values)
        _invalidMetrics = new EventMetrics
        {
            Acquisitions = -1,
            Releases = -2,
            Failures = -3,
            ContentionEvents = -4,
            Timestamp = DateTime.UtcNow
        };
    }

    [Benchmark]
    public IReadOnlyList<string> ValidateLockEventSubscriber_Null()
    {
        LockEventSubscriber? subscriber = null;
        return LockEventSubscriberValidation.Validate(subscriber);
    }

    [Benchmark]
    public IReadOnlyList<string> ValidateLockEventSubscriber_Valid()
    {
        var subscriber = new LoggingLockEventSubscriber(new MockLogger<LoggingLockEventSubscriber>());
        return LockEventSubscriberValidation.Validate(subscriber);
    }

    [Benchmark]
    public IReadOnlyList<string> ValidateMetricsTrackingEventSubscriber_Null()
    {
        MetricsTrackingEventSubscriber? subscriber = null;
        return LockEventSubscriberValidation.Validate(subscriber);
    }

    [Benchmark]
    public IReadOnlyList<string> ValidateMetricsTrackingEventSubscriber_Valid()
    {
        return LockEventSubscriberValidation.Validate(_metricsSubscriber);
    }

    [Benchmark]
    public IReadOnlyList<string> ValidateMetricsTrackingEventSubscriber_Invalid()
    {
        return LockEventSubscriberValidation.Validate(_invalidMetrics);
    }

    [Benchmark]
    public IReadOnlyList<string> ValidateEventMetrics_Null()
    {
        EventMetrics? metrics = null;
        return LockEventSubscriberValidation.Validate(metrics);
    }

    [Benchmark]
    public IReadOnlyList<string> ValidateEventMetrics_Valid()
    {
        return LockEventSubscriberValidation.Validate(_validMetrics);
    }

    [Benchmark]
    public IReadOnlyList<string> ValidateEventMetrics_Invalid()
    {
        return LockEventSubscriberValidation.Validate(_invalidMetrics);
    }

    [Benchmark]
    public bool IsValid_LockEventSubscriber_Null()
    {
        LockEventSubscriber? subscriber = null;
        return LockEventSubscriberValidation.IsValid(subscriber);
    }

    [Benchmark]
    public bool IsValid_LockEventSubscriber_Valid()
    {
        var subscriber = new LoggingLockEventSubscriber(new MockLogger<LoggingLockEventSubscriber>());
        return LockEventSubscriberValidation.IsValid(subscriber);
    }

    [Benchmark]
    public bool IsValid_MetricsTrackingEventSubscriber_Null()
    {
        MetricsTrackingEventSubscriber? subscriber = null;
        return LockEventSubscriberValidation.IsValid(subscriber);
    }

    [Benchmark]
    public bool IsValid_MetricsTrackingEventSubscriber_Valid()
    {
        return LockEventSubscriberValidation.IsValid(_metricsSubscriber);
    }

    [Benchmark]
    public bool IsValid_EventMetrics_Null()
    {
        EventMetrics? metrics = null;
        return LockEventSubscriberValidation.IsValid(metrics);
    }

    [Benchmark]
    public bool IsValid_EventMetrics_Valid()
    {
        return LockEventSubscriberValidation.IsValid(_validMetrics);
    }

    [Benchmark]
    public void EnsureValid_LockEventSubscriber_Null()
    {
        LockEventSubscriber? subscriber = null;
        LockEventSubscriberValidation.EnsureValid(subscriber);
    }

    [Benchmark]
    public void EnsureValid_LockEventSubscriber_Valid()
    {
        var subscriber = new LoggingLockEventSubscriber(new MockLogger<LoggingLockEventSubscriber>());
        LockEventSubscriberValidation.EnsureValid(subscriber);
    }

    [Benchmark]
    public void EnsureValid_MetricsTrackingEventSubscriber_Null()
    {
        MetricsTrackingEventSubscriber? subscriber = null;
        LockEventSubscriberValidation.EnsureValid(subscriber);
    }

    [Benchmark]
    public void EnsureValid_MetricsTrackingEventSubscriber_Valid()
    {
        LockEventSubscriberValidation.EnsureValid(_metricsSubscriber);
    }

    [Benchmark]
    public void EnsureValid_EventMetrics_Null()
    {
        EventMetrics? metrics = null;
        LockEventSubscriberValidation.EnsureValid(metrics);
    }

    [Benchmark]
    public void EnsureValid_EventMetrics_Valid()
    {
        LockEventSubscriberValidation.EnsureValid(_validMetrics);
    }

    // Simple mock logger for benchmarking
    private class MockLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}