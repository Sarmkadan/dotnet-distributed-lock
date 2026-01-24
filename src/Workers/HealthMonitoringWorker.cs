// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Workers;

using SarmKadan.DistributedLock.Core.Repository;

/// <summary>
/// Background worker that monitors the health of the distributed lock system.
/// Periodically verifies backend connectivity and records health status.
/// Alerts when issues are detected with the lock service or backends.
/// </summary>
public class HealthMonitoringWorker : BackgroundService
{
    private readonly ILockRepository _repository;
    private readonly ILogger<HealthMonitoringWorker> _logger;
    private readonly HealthMonitoringWorkerOptions _options;
    private readonly HealthStatus _healthStatus = new();

    public HealthMonitoringWorker(
        ILockRepository repository,
        ILogger<HealthMonitoringWorker> logger,
        HealthMonitoringWorkerOptions? options = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new HealthMonitoringWorkerOptions();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Health monitoring worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckHealthAsync(stoppingToken);
                await Task.Delay(_options.CheckIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in health monitoring worker");
            }
        }

        _logger.LogInformation("Health monitoring worker stopped");
    }

    /// <summary>
    /// Checks the overall health of the lock system.
    /// </summary>
    private async Task CheckHealthAsync(CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Test backend connectivity
            await TestBackendConnectivityAsync(cancellationToken);

            stopwatch.Stop();
            _healthStatus.LastCheckTime = DateTime.UtcNow;
            _healthStatus.IsHealthy = true;
            _healthStatus.CheckDurationMs = stopwatch.ElapsedMilliseconds;

            _logger.LogDebug("Health check passed in {DurationMs}ms", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _healthStatus.LastCheckTime = DateTime.UtcNow;
            _healthStatus.IsHealthy = false;
            _healthStatus.CheckDurationMs = stopwatch.ElapsedMilliseconds;
            _healthStatus.LastErrorMessage = ex.Message;

            _logger.LogError(ex, "Health check failed: {Message}", ex.Message);

            // Alert if configured
            if (_options.AlertOnUnhealthy)
            {
                await AlertUnhealthyStatusAsync(ex);
            }
        }
    }

    /// <summary>
    /// Tests connectivity to the lock repository backend.
    /// </summary>
    private async Task TestBackendConnectivityAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Attempt to get a non-existent lock (should return null, not throw)
            var testLockId = $"__health_check_{DateTime.UtcNow.Ticks}";
            var result = await _repository.GetLockAsync(testLockId);

            // If we get here without exception, backend is responding
            _healthStatus.BackendConnected = true;
        }
        catch
        {
            _healthStatus.BackendConnected = false;
            throw;
        }
    }

    /// <summary>
    /// Sends an alert when the system becomes unhealthy.
    /// Can be extended to integrate with alerting systems.
    /// </summary>
    private async Task AlertUnhealthyStatusAsync(Exception ex)
    {
        _healthStatus.ConsecutiveFailures++;

        if (_healthStatus.ConsecutiveFailures >= _options.FailureThreshold)
        {
            _logger.LogCritical(
                "Health monitoring: System is unhealthy after {Failures} consecutive failures: {Message}",
                _healthStatus.ConsecutiveFailures,
                ex.Message);

            // TODO: Integrate with alerting service (PagerDuty, Slack, etc)
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets the current health status.
    /// </summary>
    public HealthStatus GetStatus()
    {
        return new HealthStatus
        {
            IsHealthy = _healthStatus.IsHealthy,
            BackendConnected = _healthStatus.BackendConnected,
            LastCheckTime = _healthStatus.LastCheckTime,
            CheckDurationMs = _healthStatus.CheckDurationMs,
            ConsecutiveFailures = _healthStatus.ConsecutiveFailures,
            LastErrorMessage = _healthStatus.LastErrorMessage
        };
    }

    /// <summary>
    /// Resets the failure counter when health recovers.
    /// </summary>
    public void ResetFailureCounter()
    {
        _healthStatus.ConsecutiveFailures = 0;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping health monitoring worker");
        await base.StopAsync(cancellationToken);
    }
}

/// <summary>
/// Represents the current health status of the system.
/// </summary>
public class HealthStatus
{
    public bool IsHealthy { get; set; }
    public bool BackendConnected { get; set; }
    public DateTime LastCheckTime { get; set; }
    public long CheckDurationMs { get; set; }
    public int ConsecutiveFailures { get; set; }
    public string? LastErrorMessage { get; set; }
}

/// <summary>
/// Configuration for health monitoring worker.
/// </summary>
public class HealthMonitoringWorkerOptions
{
    /// <summary>
    /// Interval between health checks (milliseconds).
    /// </summary>
    public int CheckIntervalMs { get; set; } = 30000; // 30 seconds

    /// <summary>
    /// Number of consecutive failures before alerting.
    /// Prevents false alarms from transient issues.
    /// </summary>
    public int FailureThreshold { get; set; } = 3;

    /// <summary>
    /// Whether to send alerts on unhealthy status.
    /// </summary>
    public bool AlertOnUnhealthy { get; set; } = true;

    /// <summary>
    /// Maximum time a health check can take before timing out.
    /// </summary>
    public TimeSpan CheckTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
