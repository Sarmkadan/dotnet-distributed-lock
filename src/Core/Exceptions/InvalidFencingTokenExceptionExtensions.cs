#nullable enable

namespace SarmKadan.DistributedLock.Exceptions;

/// <summary>
/// Provides extension methods for <see cref="InvalidFencingTokenException"/> to facilitate common operations
/// when working with fencing tokens.
/// </summary>
public static class InvalidFencingTokenExceptionExtensions
{
    /// <summary>
    /// Determines whether the provided token matches the current token exactly.
    /// </summary>
    /// <param name="exception">The exception instance.</param>
    /// <returns>True if the tokens match; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception"/> is null.</exception>
    public static bool IsTokenMismatch(this InvalidFencingTokenException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return !string.Equals(
            exception.ProvidedToken,
            exception.CurrentToken,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Gets a value indicating whether the provided token is older than the current token.
    /// </summary>
    /// <param name="exception">The exception instance.</param>
    /// <returns>True if the provided token is older; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception"/> is null.</exception>
    public static bool IsTokenSuperseded(this InvalidFencingTokenException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // Since tokens are typically GUIDs or incrementing values, we can compare them directly
        // Older tokens will have lower values when parsed as Guid or compared as strings
        return string.CompareOrdinal(exception.ProvidedToken, exception.CurrentToken) < 0;
    }

    /// <summary>
    /// Gets a value indicating whether the provided token is newer than the current token.
    /// </summary>
    /// <param name="exception">The exception instance.</param>
    /// <returns>True if the provided token is newer; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception"/> is null.</exception>
    public static bool IsTokenFromFuture(this InvalidFencingTokenException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return string.CompareOrdinal(exception.ProvidedToken, exception.CurrentToken) > 0;
    }

    /// <summary>
    /// Creates a new exception with updated tokens, preserving the original message format.
    /// </summary>
    /// <param name="exception">The exception instance.</param>
    /// <param name="newProvidedToken">The new provided token value.</param>
    /// <param name="newCurrentToken">The new current token value.</param>
    /// <returns>A new <see cref="InvalidFencingTokenException"/> instance.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="exception"/>, <paramref name="newProvidedToken"/>, or <paramref name="newCurrentToken"/> is null.
    /// </exception>
    public static InvalidFencingTokenException WithTokens(
        this InvalidFencingTokenException exception,
        string newProvidedToken,
        string newCurrentToken)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrEmpty(newProvidedToken);
        ArgumentException.ThrowIfNullOrEmpty(newCurrentToken);

        return new InvalidFencingTokenException(newProvidedToken, newCurrentToken);
    }

    /// <summary>
    /// Gets a formatted message containing both tokens for logging purposes.
    /// </summary>
    /// <param name="exception">The exception instance.</param>
    /// <returns>A formatted string containing both token values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception"/> is null.</exception>
    public static string GetTokenDetails(this InvalidFencingTokenException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return $"Provided: '{exception.ProvidedToken}', Current: '{exception.CurrentToken}'";
    }
}