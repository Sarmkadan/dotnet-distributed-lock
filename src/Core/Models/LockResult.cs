#nullable enable
// =============================================================================
// Author: Vladyslav Zaiets | https://sarmkadan.com
// CTO & Software Architect
// =====================================================================

using SarmKadan.DistributedLock.Exceptions;

namespace SarmKadan.DistributedLock.Models;

/// <summary>
/// Represents the result of a lock acquisition attempt with typed failure handling.
/// </summary>
/// <typeparam name="T">The type of value returned when the lock is successfully acquired.</typeparam>
public readonly record struct LockResult<T>
{
    /// <summary>
    /// Gets the status of the lock acquisition attempt.
    /// </summary>
    public LockAcquisitionStatus Status { get; }

    /// <summary>
    /// Gets the value when the lock was successfully acquired.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Gets the error message when the lock acquisition failed.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets the exception when the operation failed.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LockResult{T}"/> struct.
    /// </summary>
    /// <param name="status">The acquisition status.</param>
    /// <param name="value">The value if acquired.</param>
    /// <param name="errorMessage">The error message if contended or faulted.</param>
    /// <param name="exception">The exception if faulted.</param>
    public LockResult(LockAcquisitionStatus status, T? value = default, string? errorMessage = null, Exception? exception = null)
    {
        Status = status;
        Value = value;
        ErrorMessage = errorMessage;
        Exception = exception;
    }

    /// <summary>
    /// Creates a successful result with a value.
    /// </summary>
    /// <param name="value">The value to return.</param>
    /// <returns>A successful <see cref="LockResult{T}"/>.</returns>
    public static LockResult<T> Acquired(T value) => new(LockAcquisitionStatus.Acquired, value);

    /// <summary>
    /// Creates a result indicating the lock was contended (already held by another owner).
    /// </summary>
    /// <param name="errorMessage">The error message describing why acquisition failed.</param>
    /// <returns>A contended <see cref="LockResult{T}"/>.</returns>
    public static LockResult<T> Contended(string errorMessage) => new(LockAcquisitionStatus.Contended, default, errorMessage);

    /// <summary>
    /// Creates a result indicating the lock acquisition failed due to an error.
    /// </summary>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <returns>A faulted <see cref="LockResult{T}"/>.</returns>
    public static LockResult<T> Faulted(Exception exception) => new(LockAcquisitionStatus.Faulted, default, exception.Message, exception);

    /// <summary>
    /// Gets a value indicating whether the lock was successfully acquired.
    /// </summary>
    public bool IsAcquired => Status == LockAcquisitionStatus.Acquired;

    /// <summary>
    /// Gets a value indicating whether the lock acquisition was contended (already held).
    /// </summary>
    public bool IsContended => Status == LockAcquisitionStatus.Contended;

    /// <summary>
    /// Gets a value indicating whether the operation failed due to an error.
    /// </summary>
    public bool IsFaulted => Status == LockAcquisitionStatus.Faulted;

    /// <summary>
    /// Deconstructs the result into its components.
    /// </summary>
    /// <param name="status">The acquisition status.</param>
    /// <param name="value">The value if acquired, otherwise default.</param>
    /// <param name="errorMessage">The error message if contended or faulted.</param>
    /// <param name="exception">The exception if faulted.</param>
    public void Deconstruct(out LockAcquisitionStatus status, out T? value, out string? errorMessage, out Exception? exception)
    {
        status = Status;
        value = Value;
        errorMessage = ErrorMessage;
        exception = Exception;
    }

    /// <summary>
    /// Implicit conversion from successful result to value.
    /// </summary>
    /// <param name="result">The result to convert.</param>
    public static implicit operator T?(LockResult<T> result) => result.IsAcquired ? result.Value : default;

    /// <summary>
    /// Implicit conversion from value to successful result.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator LockResult<T>(T value) => Acquired(value);
}

/// <summary>
/// Represents the result of a lock acquisition attempt with typed failure handling.
/// </summary>
public readonly record struct LockResult
{
    /// <summary>
    /// Gets the status of the lock acquisition attempt.
    /// </summary>
    public LockAcquisitionStatus Status { get; }

    /// <summary>
    /// Gets the error message when the lock acquisition failed.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets the exception when the operation failed.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LockResult"/> struct.
    /// </summary>
    /// <param name="status">The acquisition status.</param>
    /// <param name="errorMessage">The error message if contended or faulted.</param>
    /// <param name="exception">The exception if faulted.</param>
    public LockResult(LockAcquisitionStatus status, string? errorMessage = null, Exception? exception = null)
    {
        Status = status;
        ErrorMessage = errorMessage;
        Exception = exception;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <returns>A successful <see cref="LockResult"/>.</returns>
    public static LockResult Acquired() => new(LockAcquisitionStatus.Acquired);

    /// <summary>
    /// Creates a result indicating the lock was contended (already held by another owner).
    /// </summary>
    /// <param name="errorMessage">The error message describing why acquisition failed.</param>
    /// <returns>A contended <see cref="LockResult"/>.</returns>
    public static LockResult Contended(string errorMessage) => new(LockAcquisitionStatus.Contended, errorMessage);

    /// <summary>
    /// Creates a result indicating the lock acquisition failed due to an error.
    /// </summary>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <returns>A faulted <see cref="LockResult"/>.</returns>
    public static LockResult Faulted(Exception exception) => new(LockAcquisitionStatus.Faulted, exception.Message, exception);

    /// <summary>
    /// Gets a value indicating whether the lock was successfully acquired.
    /// </summary>
    public bool IsAcquired => Status == LockAcquisitionStatus.Acquired;

    /// <summary>
    /// Gets a value indicating whether the lock acquisition was contended (already held).
    /// </summary>
    public bool IsContended => Status == LockAcquisitionStatus.Contended;

    /// <summary>
    /// Gets a value indicating whether the operation failed due to an error.
    /// </summary>
    public bool IsFaulted => Status == LockAcquisitionStatus.Faulted;
}

/// <summary>
/// Represents the status of a lock acquisition attempt.
/// </summary>
public enum LockAcquisitionStatus
{
    /// <summary>
    /// The lock was successfully acquired.
    /// </summary>
    Acquired,

    /// <summary>
    /// The lock was contended (already held by another owner).
    /// </summary>
    Contended,

    /// <summary>
    /// The lock acquisition failed due to an error.
    /// </summary>
    Faulted
}