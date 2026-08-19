#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock.Exceptions;
using SarmKadan.DistributedLock.Models;
using System.Collections.Concurrent;

namespace SarmKadan.DistributedLock.Services;

/// <summary>
/// Manages fencing tokens to prevent zombie processes from writing to shared resources.
/// </summary>
public sealed class FencingTokenService
{
    private readonly ConcurrentDictionary<string, FencingToken> _tokens = new();
    private readonly ConcurrentDictionary<string, long> _sequenceCounters = new();
    private readonly ILogger<FencingTokenService> _logger;

    public FencingTokenService(ILogger<FencingTokenService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Issues a new fencing token for a lock
    public FencingToken IssueToken(string lockKey)
    {
        _logger.LogInformation("Issuing fencing token for {LockKey}", lockKey);
        var sequenceNumber = _sequenceCounters.AddOrUpdate(
            lockKey,
            key => 1L,
            (key, current) => current + 1
        );
        var token = new FencingToken(Guid.NewGuid().ToString("N")[..16], sequenceNumber);
        _tokens[lockKey] = token;
        _logger.LogDebug("Issued fencing token for {LockKey}: {Token}", lockKey, token);
        _logger.LogInformation("Issued fencing token for {LockKey}", lockKey);
        return token;
    }

    // Validates a fencing token against the current token for a lock
    public bool ValidateToken(string lockKey, FencingToken providedToken)
    {
        _logger.LogInformation("Validating fencing token for {LockKey}", lockKey);
        if (!_tokens.TryGetValue(lockKey, out var currentToken))
        {
            _logger.LogInformation("No fencing token found for {LockKey}", lockKey);
            return false;
        }

        var isValid = providedToken.IsGreaterThan(currentToken) || providedToken.Equals(currentToken);
        if (!isValid)
        {
            _logger.LogWarning(
                "Fencing token validation failed for {LockKey}. Provided: {ProvidedToken}, Current: {CurrentToken}",
                lockKey, providedToken, currentToken
            );
        }
        _logger.LogInformation("Finished validating fencing token for {LockKey}. Result: {Result}", lockKey, isValid);
        return isValid;
    }

    // Gets the current fencing token for a lock
    public FencingToken? GetToken(string lockKey)
    {
        _logger.LogInformation("Getting fencing token for {LockKey}", lockKey);
        _tokens.TryGetValue(lockKey, out var token);
        _logger.LogInformation("Got fencing token for {LockKey}: {Token}", lockKey, token);
        return token;
    }

    // Revokes a fencing token (typically when lock is released)
    public void RevokeToken(string lockKey)
    {
        _logger.LogInformation("Revoking fencing token for {LockKey}", lockKey);
        _tokens.TryRemove(lockKey, out _);
        _logger.LogInformation("Revoked fencing token for {LockKey}", lockKey);
        _logger.LogDebug("Revoked fencing token for {LockKey}", lockKey);
    }

    // Increments the token sequence (creates a new token generation)
    public FencingToken IncrementToken(string lockKey)
    {
        _logger.LogInformation("Incrementing fencing token for {LockKey}", lockKey);
        // Use AddOrUpdate to atomically get or create the token and update the sequence counter
        var newToken = _tokens.AddOrUpdate(
            lockKey,
            key =>
            {
                // Key doesn't exist - create initial token with sequence number 1
                var sequenceNumber = _sequenceCounters.AddOrUpdate(
                    key,
                    _ => 1L,
                    (_, current) => current + 1
                );
                return new FencingToken(Guid.NewGuid().ToString("N")[..16], sequenceNumber);
            },
            (key, existingToken) =>
            {
                // Key exists - increment the sequence number atomically
                var newSequenceNumber = _sequenceCounters.AddOrUpdate(
                    key,
                    _ => existingToken.SequenceNumber + 1,
                    (_, current) => current + 1
                );
                return new FencingToken(Guid.NewGuid().ToString("N")[..16], newSequenceNumber);
            }
        );
        _logger.LogInformation("Incremented fencing token for {LockKey}", lockKey);
        _logger.LogDebug("Incremented fencing token for {LockKey}", lockKey);
        return newToken;
    }

    // Validates a token and throws an exception if invalid
    public void ValidateTokenOrThrow(string lockKey, FencingToken providedToken)
    {
        _logger.LogInformation("Validating token or throwing for {LockKey}", lockKey);
        if (!ValidateToken(lockKey, providedToken))
        {
            var currentToken = GetToken(lockKey);
            _logger.LogInformation("Validation failed for {LockKey}. Provided: {ProvidedToken}, Current: {CurrentToken}", lockKey, providedToken, currentToken);
            throw new InvalidFencingTokenException(
                providedToken.ToString(),
                currentToken?.ToString() ?? "none"
            );
        }
        _logger.LogInformation("Token validation succeeded for {LockKey}", lockKey);
    }

    // Checks whether a fencing token has been issued (and not yet revoked) for a resource
    public bool IsResourceLocked(string lockKey)
    {
        _logger.LogInformation("Checking if resource is locked for {LockKey}", lockKey);
        var result = _tokens.ContainsKey(lockKey);
        _logger.LogInformation("Resource {LockKey} lock status: {IsLocked}", lockKey, result);
        return result;
    }

    // Clears all tokens (typically for testing)
    public void ClearAllTokens()
    {
        _logger.LogInformation("Clearing all fencing tokens");
        _tokens.Clear();
        _logger.LogInformation("Cleared all fencing tokens");
    }
}