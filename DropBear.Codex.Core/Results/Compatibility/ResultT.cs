#region

using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Common;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Core.Results.Compatibility;

/// <summary>
///     A backwards-compatible Result{T} class that can hold a success value of type T
///     or an error message. Uses <see cref="ObjectPoolManager" /> for pooling.
///     Also implements <see cref="IEnumerable{T}" /> for legacy usage.
///     Inherits <c>Result{T, LegacyError}</c>, which in turn extends <c>Result{LegacyError}</c>.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class Result<T> : Result<T, LegacyError>, IEnumerable<T>
{
    // Single shared pool for this "Result<T>" type
    private static readonly ObjectPool<Result<T>> Pool =
        ObjectPoolManager.GetPool(() => new Result<T>(default!, ResultState.Success));

    /// <summary>
    ///     Protected constructor bridging to the base <see cref="Result{T, TError}" />.
    ///     The parent constructor is (T initialValue, ResultState state, TError? error, Exception? exception).
    ///     We pass <c>error is null ? null : new LegacyError(error)</c> for the error param.
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
            exception
        )
    {
        // The parent class "Result<T,LegacyError>" might store 'value' in a Lazy<T> internally.
    }

    /// <summary>
    ///     A textual representation of the error, for backwards compatibility.
    /// </summary>
    public string ErrorMessage => Error?.Message ?? string.Empty;

    private string DebuggerDisplay =>
        $"State = {State}, Success = {IsSuccess}, Value = {(IsSuccess ? Value?.ToString() : "null")}, Error = {ErrorMessage}";

    /// <summary>
    ///     Central helper to reduce duplication in static factory methods.
    ///     We'll call <see cref="InitializeFromValue" /> from the parent class
    ///     for setting the new value and state.
    /// </summary>
    private static Result<T> FromPool(
        ResultState state,
        T value,
        string? error,
        Exception? exception = null)
    {
        var result = Pool.Get();

        // The parent "Result<T,LegacyError>" defines a method:
        //   protected void InitializeFromValue(
        //       T newValue, ResultState newState, LegacyError? newError = null, Exception? newException = null
        //   )
        // So we pass an optional LegacyError created from the string if not null.
        result.InitializeFromValue(
            value,
            state,
            error is null ? null : new LegacyError(error),
            exception
        );
        return result;
    }

    #region Factory Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new static Result<T> Success(T value)
    {
        return FromPool(ResultState.Success, value, null);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Failure(string error, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        return FromPool(ResultState.Failure, default!, error, exception);
    }

    public static Result<T> Failure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return Failure(exception.Message, exception);
    }

    public static Result<T> PartialSuccess(T value, string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return FromPool(ResultState.PartialSuccess, value, error);
    }

    public static Result<T> Warning(T value, string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return FromPool(ResultState.Warning, value, error);
    }

    public static Result<T> Cancelled(string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return FromPool(ResultState.Cancelled, default!, error);
    }

    public static Result<T> Cancelled(T value, string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return FromPool(ResultState.Cancelled, value, error);
    }

    public static Result<T> Pending(string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return FromPool(ResultState.Pending, default!, error);
    }

    public static Result<T> Pending(T value, string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return FromPool(ResultState.Pending, value, error);
    }

    public static Result<T> NoOp(string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return FromPool(ResultState.NoOp, default!, error);
    }

    public static Result<T> NoOp(T value, string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return FromPool(ResultState.NoOp, value, error);
    }

    public static Result<T> Try(Func<T> func)
    {
        try
        {
            return Success(func());
        }
        catch (Exception ex)
        {
            return Failure(ex);
        }
    }

    #endregion

    #region IEnumerable<T> Implementation

    /// <summary>
    ///     If successful, yields the stored value; otherwise yields nothing.
    /// </summary>
    public IEnumerator<T> GetEnumerator()
    {
        if (IsSuccess)
        {
            yield return Value!;
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
