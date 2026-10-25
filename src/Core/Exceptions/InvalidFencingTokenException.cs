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
        ArgumentNullException.ThrowIfNull(providedToken);
        ArgumentNullException.ThrowIfNull(currentToken);

        ProvidedToken = providedToken;
        CurrentToken = currentToken;
    }

    public InvalidFencingTokenException(string providedToken, string currentToken, Exception innerException)
        : base($"Fencing token '{providedToken}' is invalid or superseded by '{currentToken}'.", innerException)
    {
        ArgumentNullException.ThrowIfNull(providedToken);
        ArgumentNullException.ThrowIfNull(currentToken);

        ProvidedToken = providedToken;
        CurrentToken = currentToken;
    }
}
