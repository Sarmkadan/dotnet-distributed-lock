#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

using Microsoft.Extensions.Logging;
using SarmKadan.DistributedLock.Exceptions;
using SarmKadan.DistributedLock.Models;

namespace SarmKadan.DistributedLock.Services;

/// <summary>
/// Manages fencing tokens to prevent zombie processes from writing to shared resources.
/// </summary>
public sealed class FencingTokenService
{
    private readonly Dictionary<string, FencingToken> _tokens = new();
    private readonly ReaderWriterLockSlim _lockSlim = new();
    private readonly ILogger<FencingTokenService> _logger;
    private long _sequenceCounter = 0;

    public FencingTokenService(ILogger<FencingTokenService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Issues a new fencing token for a lock
    public FencingToken IssueToken(string lockKey)
    {
        _lockSlim.EnterWriteLock();
        try
        {
            var sequenceNumber = Interlocked.Increment(ref _sequenceCounter);
            var token = new FencingToken(Guid.NewGuid().ToString("N")[..16], sequenceNumber);

            _tokens[lockKey] = token;
            _logger.LogDebug("Issued fencing token for {LockKey}: {Token}", lockKey, token);
            return token;
        }
        finally
        {
            _lockSlim.ExitWriteLock();
        }
    }

    // Validates a fencing token against the current token for a lock
    public bool ValidateToken(string lockKey, FencingToken providedToken)
    {
        _lockSlim.EnterReadLock();
        try
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
        finally
        {
            _lockSlim.ExitReadLock();
        }
    }

    // Gets the current fencing token for a lock
    public FencingToken? GetToken(string lockKey)
    {
        _lockSlim.EnterReadLock();
        try
        {
            _tokens.TryGetValue(lockKey, out var token);
            return token;
        }
        finally
        {
            _lockSlim.ExitReadLock();
        }
    }

    // Revokes a fencing token (typically when lock is released)
    public void RevokeToken(string lockKey)
    {
        _lockSlim.EnterWriteLock();
        try
        {
            if (_tokens.Remove(lockKey))
            {
                _logger.LogDebug("Revoked fencing token for {LockKey}", lockKey);
            }
        }
        finally
        {
            _lockSlim.ExitWriteLock();
        }
    }

    // Increments the token sequence (creates a new token generation)
    public FencingToken IncrementToken(string lockKey)
        {
            _lockSlim.EnterWriteLock();
            try
            {
                if (_tokens.TryGetValue(lockKey, out var currentToken))
                {
                    var newToken = currentToken.IncrementSequence();
                    _tokens[lockKey] = newToken;
                    _logger.LogDebug("Incremented fencing token for {LockKey}", lockKey);
                    return newToken;
                }

                var sequenceNumber = Interlocked.Increment(ref _sequenceCounter);
                var token = new FencingToken(Guid.NewGuid().ToString("N")[..16], sequenceNumber);
                _tokens[lockKey] = token;
                return token;
            }
            finally
            {
                _lockSlim.ExitWriteLock();
            }
        }
    {
        _lockSlim.EnterWriteLock();
        try
        {
            if (_tokens.TryGetValue(lockKey, out var currentToken))
            {
                var newToken = currentToken.IncrementSequence();
                _tokens[lockKey] = newToken;
                _logger.LogDebug("Incremented fencing token for {LockKey}", lockKey);
                return newToken;
            }

            var sequenceNumber = Interlocked.Increment(ref _sequenceCounter);
            var token = new FencingToken(Guid.NewGuid().ToString("N")[..16], sequenceNumber);
            _tokens[lockKey] = token;
            return token;
        }
        finally
        {
            _lockSlim.ExitWriteLock();
        }
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

    // Clears all tokens (typically for testing)
    public void ClearAllTokens()
    {
        _lockSlim.EnterWriteLock();
        try
        {
            _tokens.Clear();
            _logger.LogInformation("Cleared all fencing tokens");
        }
        finally
        {
            _lockSlim.ExitWriteLock();
        }
    }
}
