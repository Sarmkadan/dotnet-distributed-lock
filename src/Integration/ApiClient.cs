#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Integration;

using System.Text.Json;

/// <summary>
/// High-level API client for consuming the distributed lock service.
/// Handles serialization, error handling, and provides typed methods for all operations.
/// Can be used by clients that need to interact with a remote lock service.
/// </summary>
public interface ILockApiClient
{
    Task<LockResponse?> AcquireLockAsync(AcquireLockRequest request, CancellationToken cancellationToken = default);
    Task ReleaseLockAsync(string lockId, ulong fencingToken, CancellationToken cancellationToken = default);
    Task<RenewLockResponse?> RenewLockAsync(string lockId, ulong fencingToken, int additionalSeconds = 30, CancellationToken cancellationToken = default);
    Task<LockStatusResponse?> GetLockStatusAsync(string lockId, CancellationToken cancellationToken = default);
}

/// <summary>
/// HTTP-based implementation of lock API client.
/// </summary>
public class HttpLockApiClient : ILockApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpLockApiClient> _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public HttpLockApiClient(HttpClient httpClient, ILogger<HttpLockApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LockResponse?> AcquireLockAsync(
        AcquireLockRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Acquiring lock: {LockName}", request.LockName);

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/distributed-lock/acquire", content, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<LockResponse>(responseJson, _jsonOptions);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to acquire lock: {LockName}", request.LockName);
            throw;
        }
    }

    public async Task ReleaseLockAsync(
        string lockId,
        ulong fencingToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Releasing lock: {LockId}", lockId);

            var response = await _httpClient.PostAsync(
                $"/api/distributed-lock/release/{lockId}?fencingToken={fencingToken}",
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

    public async Task<RenewLockResponse?> RenewLockAsync(
        string lockId,
        ulong fencingToken,
        int additionalSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Renewing lock: {LockId}", lockId);

            var response = await _httpClient.PostAsync(
                $"/api/distributed-lock/renew/{lockId}?fencingToken={fencingToken}&additionalSeconds={additionalSeconds}",
                null,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<RenewLockResponse>(responseJson, _jsonOptions);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to renew lock: {LockId}", lockId);
            throw;
        }
    }

    public async Task<LockStatusResponse?> GetLockStatusAsync(
        string lockId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting lock status: {LockId}", lockId);

            var response = await _httpClient.GetAsync(
                $"/api/distributed-lock/status/{lockId}",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<LockStatusResponse>(responseJson, _jsonOptions);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get lock status: {LockId}", lockId);
            throw;
        }
    }
}

/// <summary>
/// Request to acquire a lock via API.
/// </summary>
public record AcquireLockRequest
{
    public required string LockName { get; init; }
    public required int DurationSeconds { get; init; }
    public bool AutoRenew { get; init; }
    public int? RenewalIntervalSeconds { get; init; }
}

/// <summary>
/// Response from lock acquisition.
/// </summary>
public record LockResponse
{
    public bool Success { get; init; }
    public required string LockId { get; init; }
    public required ulong FencingToken { get; init; }
    public DateTime AcquiredAt { get; init; }
    public DateTime ExpiresAt { get; init; }
}

/// <summary>
/// Response from lock renewal.
/// </summary>
public record RenewLockResponse
{
    public bool Success { get; init; }
    public DateTime ExpiresAt { get; init; }
    public int RemainingSeconds { get; init; }
}

/// <summary>
/// Response containing lock status information.
/// </summary>
public record LockStatusResponse
{
    public required string LockId { get; init; }
    public required string Name { get; init; }
    public required bool IsActive { get; init; }
    public required string OwnerId { get; init; }
    public DateTime AcquiredAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public int RemainingSeconds { get; init; }
}

/// <summary>
/// Extension methods for configuring the lock API client.
/// </summary>
public static class LockApiClientExtensions
{
    /// <summary>
    /// Registers the lock API client in the dependency injection container.
    /// </summary>
    public static IServiceCollection AddLockApiClient(
        this IServiceCollection services,
        Uri baseAddress)
    {
        services.AddHttpClient<ILockApiClient, HttpLockApiClient>(client =>
        {
            client.BaseAddress = baseAddress;
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        return services;
    }

    /// <summary>
    /// Registers the lock API client with custom configuration.
    /// </summary>
    public static IServiceCollection AddLockApiClient(
        this IServiceCollection services,
        Action<HttpClient> configureClient)
    {
        services.AddHttpClient<ILockApiClient, HttpLockApiClient>(configureClient);
        return services;
    }
}
