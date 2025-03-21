#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Integration;

/// <summary>
/// Factory for creating and managing HttpClient instances.
/// Implements typed client pattern with proper connection pooling and resilience.
/// Prevents socket exhaustion by reusing HttpClient instances across the application.
/// </summary>
public interface IHttpClientFactory
{
    HttpClient CreateClient(string name);
    HttpClient GetClient(string name);
}

/// <summary>
/// Default implementation using IHttpClientFactory from DI container.
/// Leverages dependency injection for consistency with ASP.NET Core guidelines.
/// </summary>
public class DefaultHttpClientFactory : IHttpClientFactory
{
    private readonly System.Net.Http.IHttpClientFactory _factory;
    private readonly ILogger<DefaultHttpClientFactory> _logger;

    public DefaultHttpClientFactory(System.Net.Http.IHttpClientFactory factory, ILogger<DefaultHttpClientFactory> logger)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public HttpClient CreateClient(string name)
    {
        _logger.LogInformation("Creating HTTP client: {ClientName}", name);
        return _factory.CreateClient(name);
    }

    public HttpClient GetClient(string name)
    {
        return _factory.CreateClient(name);
    }
}

/// <summary>
/// Configuration for HTTP client behavior.
/// </summary>
public class HttpClientConfiguration
{
    /// <summary>
    /// Default timeout for all requests (default: 30 seconds).
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum number of retries on transient failures.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Enable automatic decompression of responses.
    /// </summary>
    public bool AutomaticDecompression { get; set; } = true;

    /// <summary>
    /// API base URL for external lock services.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// API key for authentication if required.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Custom headers to add to all requests.
    /// </summary>
    public Dictionary<string, string> DefaultHeaders { get; set; } = new();
}

/// <summary>
/// Typed HTTP client for lock service operations.
/// Provides a clean interface for communicating with distributed lock backends.
/// </summary>
public class LockServiceHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LockServiceHttpClient> _logger;
    private readonly HttpClientConfiguration _config;

    public LockServiceHttpClient(
        HttpClient httpClient,
        ILogger<LockServiceHttpClient> logger,
        HttpClientConfiguration? config = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? new HttpClientConfiguration();

        ConfigureClient();
    }

    /// <summary>
    /// Sends a GET request to retrieve lock information.
    /// </summary>
    public async Task<string?> GetLockAsync(string lockId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/locks/{lockId}", cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get lock: {LockId}", lockId);
            throw;
        }
    }

    /// <summary>
    /// Sends a POST request to acquire a lock.
    /// </summary>
    public async Task<string?> AcquireLockAsync(
        string lockName,
        int durationSeconds,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new { lockName, durationSeconds };
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync("/locks/acquire", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to acquire lock: {LockName}", lockName);
            throw;
        }
    }

    /// <summary>
    /// Sends a POST request to release a lock.
    /// </summary>
    public async Task ReleaseLockAsync(
        string lockId,
        ulong fencingToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"/locks/{lockId}/release?fencingToken={fencingToken}",
                null,
                cancellationToken);

            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to release lock: {LockId}", lockId);
            throw;
        }
    }

    private void ConfigureClient()
    {
        _httpClient.Timeout = _config.DefaultTimeout;

        if (!string.IsNullOrEmpty(_config.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_config.BaseUrl);
        }

        // Add default headers
        foreach (var header in _config.DefaultHeaders)
        {
            _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
        }

        // Add API key if provided
        if (!string.IsNullOrEmpty(_config.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _config.ApiKey);
        }
    }
}
