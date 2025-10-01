#region

using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Common;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Core.Results.Compatibility;

/// <summary>
///     A backwards-compatible Result&lt;T&gt; class with string-based errors.
///     Use Result&lt;T, TError&gt; with custom error types instead.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
[Obsolete("Use Result<T, TError> with custom error types instead of string-based errors. This type will be removed in a future version.", DiagnosticId = "DROPBEAR003")]
[ExcludeFromCodeCoverage] // Legacy code
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class Result<T> : Result<T, LegacyError>, IEnumerable<T>
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
    ///     Creates a successful result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new static Result<T> Success(T value)
    {
        return FromPool(ResultState.Success, value, null);
    }

    /// <summary>
    ///     Creates a failed result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Failure(string error, Exception? exception = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return FromPool(ResultState.Failure, default!, error, exception);
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
    ///     Creates a partial success result.
    /// </summary>
    public static Result<T> PartialSuccess(T value, string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return FromPool(ResultState.PartialSuccess, value, error);
    }

    /// <summary>
    ///     Creates a warning result.
    /// </summary>
    public static Result<T> Warning(T value, string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return FromPool(ResultState.Warning, value, error);
    }

    /// <summary>
    ///     Creates a cancelled result.
    /// </summary>
    public static Result<T> Cancelled(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return FromPool(ResultState.Cancelled, default!, error);
    }

    /// <summary>
    ///     Creates a cancelled result with a value.
    /// </summary>
    public static Result<T> Cancelled(T value, string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return FromPool(ResultState.Cancelled, value, error);
    }

    /// <summary>
    ///     Creates a pending result.
    /// </summary>
    public static Result<T> Pending(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return FromPool(ResultState.Pending, default!, error);
    }

    /// <summary>
    ///     Creates a pending result with a value.
    /// </summary>
    public static Result<T> Pending(T value, string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return FromPool(ResultState.Pending, value, error);
    }

    /// <summary>
    ///     Creates a no-op result.
    /// </summary>
    public static Result<T> NoOp(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return FromPool(ResultState.NoOp, default!, error);
    }

    /// <summary>
    ///     Creates a no-op result with a value.
    /// </summary>
    public static Result<T> NoOp(T value, string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return FromPool(ResultState.NoOp, value, error);
    }

    /// <summary>
    ///     Tries to execute a function and wraps the result.
    /// </summary>
    public static Result<T> Try(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);

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

    #region Migration Helpers

    /// <summary>
    ///     Converts this legacy Result&lt;T&gt; to a modern Result&lt;T, TError&gt;.
    /// </summary>
    /// <typeparam name="TError">The custom error type to convert to.</typeparam>
    /// <returns>A new Result&lt;T, TError&gt; with the same state and value/error information.</returns>
    public Base.Result<T, TError> ToModern<TError>()
        where TError : ResultError
    {
        if (IsSuccess)
        {
            return Base.Result<T, TError>.Success(Value!);
        }

        var error = (TError)Activator.CreateInstance(typeof(TError), ErrorMessage)!;

        return State switch
        {
            ResultState.Failure => Base.Result<T, TError>.Failure(error, Exception),
            ResultState.Warning => Base.Result<T, TError>.Warning(Value!, error),
            ResultState.PartialSuccess => Base.Result<T, TError>.PartialSuccess(Value!, error),
            ResultState.Cancelled when Value != null => Base.Result<T, TError>.Cancelled(Value!, error),
            ResultState.Cancelled => Base.Result<T, TError>.Cancelled(error),
            ResultState.Pending when Value != null => Base.Result<T, TError>.Pending(Value!, error),
            ResultState.Pending => Base.Result<T, TError>.Pending(error),
            ResultState.NoOp when Value != null => Base.Result<T, TError>.NoOp(Value!, error),
            ResultState.NoOp => Base.Result<T, TError>.NoOp(error),
            _ => Base.Result<T, TError>.Failure(error, Exception)
        };
    }

    /// <summary>
    ///     Creates a legacy Result&lt;T&gt; from a modern Result&lt;T, TError&gt;.
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

    #region Internal Helpers

    private static Result<T> FromPool(ResultState state, T value, string? error, Exception? exception = null)
    {
        var result = Pool.Get();
        result.InitializeFromValue(value, state, error is null ? null : new LegacyError(error), exception);
        return result;
    }

    #endregion
}
