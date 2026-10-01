#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using Microsoft.Extensions.Logging;

namespace SarmKadan.DistributedLock.Services;

/// <summary>
/// Configurable retry policy that executes an async operation with exponential backoff,
/// jitter, and optional per-exception filtering. Built on top of <see cref="ILockRetryPolicy"/>
/// for delay calculation while adding execution orchestration.
/// </summary>
public sealed class RetryPolicy
{
    private readonly ILockRetryPolicy _delayPolicy;
    private readonly ILogger<RetryPolicy> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="RetryPolicy"/>.
    /// </summary>
    /// <param name="delayPolicy">The delay calculation policy.</param>
    /// <param name="logger">Logger instance.</param>
    public RetryPolicy(ILockRetryPolicy delayPolicy, ILogger<RetryPolicy> logger)
    {
        _delayPolicy = delayPolicy ?? throw new ArgumentNullException(nameof(delayPolicy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes a <see cref="RetryPolicy"/> with default exponential backoff settings.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public RetryPolicy(ILogger<RetryPolicy> logger)
        : this(new DefaultLockRetryPolicy(), logger)
    {
    }

    /// <summary>
    /// Executes an async operation with retry logic.
    /// </summary>
    /// <typeparam name="TResult">The return type of the operation.</typeparam>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="shouldRetry">
    /// Optional predicate that receives the caught exception and returns true if the operation
    /// should be retried. When null, all exceptions trigger a retry.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        Func<Exception, bool>? shouldRetry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return await operation(cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt >= _delayPolicy.MaxRetries)
                {
                    _logger.LogWarning(
                        ex,
                        "Retry exhausted after {Attempts} attempts",
                        attempt + 1);
                    throw;
                }

                if (shouldRetry is not null && !shouldRetry(ex))
                {
                    _logger.LogDebug(ex, "Exception not retryable, re-throwing on attempt {Attempt}", attempt);
                    throw;
                }

                var delay = _delayPolicy.GetDelay(attempt);
                _logger.LogDebug(
                    "Attempt {Attempt} failed, retrying in {DelayMs}ms: {Message}",
                    attempt + 1, delay.TotalMilliseconds, ex.Message);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Executes an async action (no return value) with retry logic.
    /// </summary>
    /// <param name="operation">The async action to execute.</param>
    /// <param name="shouldRetry">Optional predicate for filtering retryable exceptions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        Func<Exception, bool>? shouldRetry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await ExecuteAsync<object?>(
            async ct =>
            {
                await operation(ct);
                return null;
            },
            shouldRetry,
            cancellationToken);
    }

    /// <summary>
    /// Executes a synchronous operation with retry logic.
    /// </summary>
    /// <typeparam name="TResult">The return type of the operation.</typeparam>
    /// <param name="operation">The synchronous operation to execute.</param>
    /// <param name="shouldRetry">Optional predicate for filtering retryable exceptions.</param>
    /// <returns>The result of the operation.</returns>
    public TResult Execute<TResult>(
        Func<TResult> operation,
        Func<Exception, bool>? shouldRetry = null)
    {
        ArgumentNullException.ThrowIfNull(operation);

        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return operation();
            }
            catch (Exception ex)
            {
                if (attempt >= _delayPolicy.MaxRetries)
                {
                    _logger.LogWarning(ex, "Retry exhausted after {Attempts} attempts", attempt + 1);
                    throw;
                }

                if (shouldRetry is not null && !shouldRetry(ex))
                {
                    throw;
                }

                var delay = _delayPolicy.GetDelay(attempt);
                _logger.LogDebug(
                    "Sync attempt {Attempt} failed, retrying in {DelayMs}ms: {Message}",
                    attempt + 1, delay.TotalMilliseconds, ex.Message);

                Thread.Sleep(delay);
            }
        }
    }

    /// <summary>
    /// Gets the maximum number of retries configured for this policy.
    /// </summary>
    public int MaxRetries => _delayPolicy.MaxRetries;

    /// <summary>
    /// Gets the initial delay between retries.
    /// </summary>
    public TimeSpan InitialDelay => _delayPolicy.InitialDelay;
}
