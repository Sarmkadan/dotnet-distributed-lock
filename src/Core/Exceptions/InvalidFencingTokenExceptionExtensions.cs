#nullable enable

namespace SarmKadan.DistributedLock.Exceptions;

/// <summary>
/// Provides extension methods for <see cref="InvalidFencingTokenException"/> to facilitate common operations
/// when working with fencing tokens.
/// </summary>
public static class InvalidFencingTokenExceptionExtensions
{
    /// <summary>
    /// Determines whether the provided token does not match the current token exactly.
    /// </summary>
    /// <param name="exception">The exception instance.</param>
    /// <returns><see langword="true"/> if the tokens differ; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception"/> is <see langword="null"/>.</exception>
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
    /// <returns><see langword="true"/> if the provided token is older; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Tokens are compared using ordinal comparison. This method assumes tokens are monotonically increasing values
    /// such as GUIDs, timestamps, or incrementing counters where older tokens have lower values.
    /// </remarks>
    public static bool IsTokenSuperseded(this InvalidFencingTokenException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return string.CompareOrdinal(exception.ProvidedToken, exception.CurrentToken) < 0;
    }

    /// <summary>
    /// Gets a value indicating whether the provided token is newer than the current token.
    /// </summary>
    /// <param name="exception">The exception instance.</param>
    /// <returns><see langword="true"/> if the provided token is newer; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Tokens are compared using ordinal comparison. This method assumes tokens are monotonically increasing values
    /// such as GUIDs, timestamps, or incrementing counters where newer tokens have higher values.
    /// </remarks>
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
    /// Thrown when <paramref name="exception"/>, <paramref name="newProvidedToken"/>, or <paramref name="newCurrentToken"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="newProvidedToken"/> or <paramref name="newCurrentToken"/> is <see langword="null"/> or empty.
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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception"/> is <see langword="null"/>.</exception>
    public static string GetTokenDetails(this InvalidFencingTokenException exception)
        => $"Provided: '{exception.ProvidedToken}', Current: '{exception.CurrentToken}'";
}