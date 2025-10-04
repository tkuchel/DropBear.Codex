#region

using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Common;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Base;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Core.Results.Compatibility;

/// <summary>
///     A backwards-compatible Result&lt;T&gt; class with string-based errors.
///     Use Result&lt;T, TError&gt; with custom error types instead.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
[Obsolete(
    "Use Result<T, TError> with custom error types instead of string-based errors. " +
    "This type will be removed in a future version.",
    DiagnosticId = "DROPBEAR003")]
[ExcludeFromCodeCoverage] // Legacy code
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class Result<T> : Result<T, LegacyError>, IEnumerable<T>, IPooledResult
{
    private static readonly ObjectPool<Result<T>> Pool =
        ObjectPoolManager.GetPool(() => new Result<T>(default!, ResultState.Success));

    #region Constructors

    /// <summary>
    ///     Protected constructor for internal use.
    /// </summary>
    protected Result(
        T value,
        ResultState state,
        string? error = null,
        Exception? exception = null)
        : base(
            value,
            state,
            error is null ? null : new LegacyError(error),
            exception)
    {
    }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the error message for backwards compatibility.
    /// </summary>
    public string ErrorMessage => Error?.Message ?? string.Empty;

    private string DebuggerDisplay =>
        $"State = {State}, Success = {IsSuccess}, Value = {(IsSuccess ? Value?.ToString() : "null")}, Error = {ErrorMessage}";

    #endregion

    #region Factory Methods

    /// <summary>
    ///     Creates a successful result using object pooling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new static Result<T> Success(T value)
    {
        var result = Pool.Get();
        result.Initialize(value, ResultState.Success, null, null);
        return result;
    }

    /// <summary>
    ///     Creates a failed result using object pooling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Failure(string error, Exception? exception = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        var result = Pool.Get();
        result.Initialize(default!, ResultState.Failure, error, exception);
        return result;
    }

    /// <summary>
    ///     Creates a failed result from an exception.
    /// </summary>
    public static Result<T> Failure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return Failure(exception.Message, exception);
    }

    /// <summary>
    ///     Creates a partial success result using object pooling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> PartialSuccess(T value, string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        var result = Pool.Get();
        result.Initialize(value, ResultState.PartialSuccess, error, null);
        return result;
    }

    /// <summary>
    ///     Creates a warning result using object pooling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Warning(T value, string warning)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(warning);

        var result = Pool.Get();
        result.Initialize(value, ResultState.Warning, warning, null);
        return result;
    }

    /// <summary>
    ///     Creates a cancelled result using object pooling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Cancelled(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var result = Pool.Get();
        result.Initialize(default!, ResultState.Cancelled, message, null);
        return result;
    }

    /// <summary>
    ///     Creates a cancelled result with value using object pooling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Cancelled(T value, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var result = Pool.Get();
        result.Initialize(value, ResultState.Cancelled, message, null);
        return result;
    }

    /// <summary>
    ///     Creates a pending result using object pooling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Pending(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var result = Pool.Get();
        result.Initialize(default!, ResultState.Pending, message, null);
        return result;
    }

    /// <summary>
    ///     Creates a pending result with value using object pooling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Pending(T value, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var result = Pool.Get();
        result.Initialize(value, ResultState.Pending, message, null);
        return result;
    }

    /// <summary>
    ///     Creates a no-op result using object pooling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> NoOp(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var result = Pool.Get();
        result.Initialize(default!, ResultState.NoOp, message, null);
        return result;
    }

    /// <summary>
    ///     Creates a no-op result with value using object pooling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> NoOp(T value, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var result = Pool.Get();
        result.Initialize(value, ResultState.NoOp, message, null);
        return result;
    }

    #endregion

    #region Conversion Methods

    /// <summary>
    ///     Converts a modern Result&lt;T, TError&gt; to a legacy Result&lt;T&gt;.
    /// </summary>
    /// <typeparam name="TError">The custom error type.</typeparam>
    /// <param name="modernResult">The modern result to convert from.</param>
    /// <returns>A legacy Result&lt;T&gt; with the same state and value/error information.</returns>
    public static Result<T> FromModern<TError>(Base.Result<T, TError> modernResult)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(modernResult);

        if (modernResult.IsSuccess)
        {
            return Success(modernResult.Value!);
        }

        var errorMessage = modernResult.Error?.Message ?? "Unknown error";

        return modernResult.State switch
        {
            ResultState.Failure => Failure(errorMessage, modernResult.Exception),
            ResultState.Warning => Warning(modernResult.Value!, errorMessage),
            ResultState.PartialSuccess => PartialSuccess(modernResult.Value!, errorMessage),
            ResultState.Cancelled when modernResult.Value != null => Cancelled(modernResult.Value!, errorMessage),
            ResultState.Cancelled => Cancelled(errorMessage),
            ResultState.Pending when modernResult.Value != null => Pending(modernResult.Value!, errorMessage),
            ResultState.Pending => Pending(errorMessage),
            ResultState.NoOp when modernResult.Value != null => NoOp(modernResult.Value!, errorMessage),
            ResultState.NoOp => NoOp(errorMessage),
            _ => Failure(errorMessage, modernResult.Exception)
        };
    }

    #endregion

    #region Pooling Support

    /// <summary>
    ///     Initializes the pooled result instance with new state.
    ///     Internal method for use by the object pool.
    /// </summary>
    private void Initialize(T value, ResultState state, string? error, Exception? exception)
    {
        // Call the base class initialization
        InitializeFromValue(value, state, error is null ? null : new LegacyError(error), exception);
    }

    /// <summary>
    ///     Resets this result to its default state before returning to the pool.
    ///     Implements IPooledResult.
    /// </summary>
    void IPooledResult.Reset()
    {
        // The base result doesn't hold mutable state that needs explicit cleanup
        // This is a no-op for now, but can be extended if needed
    }

    /// <summary>
    ///     Returns this result instance to the object pool for reuse.
    ///     Call this when you're done with a pooled result to improve performance.
    /// </summary>
    public void ReturnToPool()
    {
        Pool.Return(this);
    }

    #endregion

    #region IEnumerable<T> Implementation

    /// <summary>
    ///     If successful, yields the stored value; otherwise yields nothing.
    /// </summary>
    public IEnumerator<T> GetEnumerator()
    {
        if (IsSuccess && Value != null)
        {
            yield return Value;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    #endregion

    #region Operators

    public static implicit operator Result<T>(T value)
    {
        return Success(value);
    }

    public static implicit operator Result<T>(Exception exception)
    {
        return Failure(exception);
    }

    #endregion
}
