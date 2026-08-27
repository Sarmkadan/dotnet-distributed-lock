using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock.Events;

namespace SarmKadan.DistributedLock.Benchmarks;

[MemoryDiagnoser]
public class LockEventSubscriberValidationBenchmarks
{
    private LockEventSubscriber _validLockSubscriber = null!;
    private MetricsTrackingEventSubscriber _validMetricsSubscriber = null!;
    private EventMetrics _validEventMetrics = null!;
    private EventMetrics _invalidEventMetrics = null!;

    [GlobalSetup]
    public void Setup()
    {
        var lockLogger = new MockLogger<LoggingLockEventSubscriber>();
        var metricsLogger = new MockLogger<MetricsTrackingEventSubscriber>();
        _validLockSubscriber = new LoggingLockEventSubscriber(lockLogger);
        _validMetricsSubscriber = new MetricsTrackingEventSubscriber(metricsLogger);
        _validEventMetrics = new EventMetrics
        {
            Acquisitions = 1,
            Releases = 1,
            Failures = 0,
            ContentionEvents = 0,
            Timestamp = DateTime.UtcNow
        };
        _invalidEventMetrics = new EventMetrics
        {
            Acquisitions = -1, // invalid: negative
            Releases = 1,
            Failures = 0,
            ContentionEvents = 0,
            Timestamp = DateTime.UtcNow
        };
    }

    [Benchmark]
    public IReadOnlyList<string> Validate_LockEventSubscriber() => LockEventSubscriberValidation.Validate(_validLockSubscriber);

    [Benchmark]
    public IReadOnlyList<string> Validate_MetricsTrackingEventSubscriber() => LockEventSubscriberValidation.Validate(_validMetricsSubscriber);

    [Benchmark]
    public IReadOnlyList<string> Validate_EventMetrics_Valid() => LockEventSubscriberValidation.Validate(_validEventMetrics);

    [Benchmark]
    public IReadOnlyList<string> Validate_EventMetrics_Invalid() => LockEventSubscriberValidation.Validate(_invalidEventMetrics);

    [Benchmark]
    public bool IsValid_LockEventSubscriber() => LockEventSubscriberValidation.IsValid(_validLockSubscriber);

    [Benchmark]
    public bool IsValid_MetricsTrackingEventSubscriber() => LockEventSubscriberValidation.IsValid(_validMetricsSubscriber);

    [Benchmark]
    public bool IsValid_EventMetrics() => LockEventSubscriberValidation.IsValid(_validEventMetrics);

    [Benchmark]
    public void EnsureValid_LockEventSubscriber() => LockEventSubscriberValidation.EnsureValid(_validLockSubscriber);

    [Benchmark]
    public void EnsureValid_MetricsTrackingEventSubscriber() => LockEventSubscriberValidation.EnsureValid(_validMetricsSubscriber);

    [Benchmark]
    public void EnsureValid_EventMetrics_Valid() => LockEventSubscriberValidation.EnsureValid(_validEventMetrics);

    // Simple mock logger for benchmarking
    private class MockLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}