#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =============================================================================

namespace SarmKadan.DistributedLock.Exceptions;

/// <summary>
/// Thrown when a fencing token is invalid or has been superseded.
/// </summary>
public class InvalidFencingTokenException : DistributedLockException
{
    public string ProvidedToken { get; }
    public string CurrentToken { get; }

    public InvalidFencingTokenException(string providedToken, string currentToken)
        : base($"Fencing token '{providedToken}' is invalid or superseded by '{currentToken}'.")
    {
        ProvidedToken = providedToken;
        CurrentToken = currentToken;
    }

    public InvalidFencingTokenException(string providedToken, string currentToken, Exception innerException)
        : base($"Fencing token '{providedToken}' is invalid or superseded by '{currentToken}'.", innerException)
    {
        ProvidedToken = providedToken;
        CurrentToken = currentToken;
    }
}
