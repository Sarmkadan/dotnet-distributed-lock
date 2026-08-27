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
    public bool IsValid_MetricsTrackingEventSubscriber_Valid()
    {
        return LockEventSubscriberValidation.IsValid(_metricsSubscriber);
    }

    // Simple mock logger for benchmarking
    private class MockLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}