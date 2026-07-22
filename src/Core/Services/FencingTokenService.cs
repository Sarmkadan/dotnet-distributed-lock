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
        var sequenceNumber = _sequenceCounters.AddOrUpdate(
            lockKey,
            key => 1L,
            (key, current) => current + 1
        );
        var token = new FencingToken(Guid.NewGuid().ToString("N")[..16], sequenceNumber);
        _tokens[lockKey] = token;
        _logger.LogDebug("Issued fencing token for {LockKey}: {Token}", lockKey, token);
        return token;
    }

    // Validates a fencing token against the current token for a lock
    public bool ValidateToken(string lockKey, FencingToken providedToken)
    {
        if (!_tokens.TryGetValue(lockKey, out var currentToken))
            return false;

        var isValid = providedToken.IsGreaterThan(currentToken) || providedToken.Equals(currentToken);
        if (!isValid)
        {
            _logger.LogWarning(
                "Fencing token validation failed for {LockKey}. Provided: {ProvidedToken}, Current: {CurrentToken}",
                lockKey, providedToken, currentToken
            );
        }
        return isValid;
    }

    // Gets the current fencing token for a lock
    public FencingToken? GetToken(string lockKey)
    {
        _tokens.TryGetValue(lockKey, out var token);
        return token;
    }

    // Revokes a fencing token (typically when lock is released)
    public void RevokeToken(string lockKey)
    {
        _tokens.TryRemove(lockKey, out _);
        _logger.LogDebug("Revoked fencing token for {LockKey}", lockKey);
    }

    // Increments the token sequence (creates a new token generation)
    public FencingToken IncrementToken(string lockKey)
    {
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
        _logger.LogDebug("Incremented fencing token for {LockKey}", lockKey);
        return newToken;
    }

    // Validates a token and throws an exception if invalid
    public void ValidateTokenOrThrow(string lockKey, FencingToken providedToken)
    {
        if (!ValidateToken(lockKey, providedToken))
        {
            var currentToken = GetToken(lockKey);
            throw new InvalidFencingTokenException(
                providedToken.ToString(),
                currentToken?.ToString() ?? "none"
            );
        }
    }

    // Checks whether a fencing token has been issued (and not yet revoked) for a resource
    public bool IsResourceLocked(string lockKey)
    {
        return _tokens.ContainsKey(lockKey);
    }

    // Clears all tokens (typically for testing)
    public void ClearAllTokens()
    {
        _tokens.Clear();
        _logger.LogInformation("Cleared all fencing tokens");
    }
}
