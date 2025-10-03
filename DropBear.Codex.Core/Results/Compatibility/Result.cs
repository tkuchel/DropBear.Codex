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
///     A backwards-compatible, untyped Result class.
///     Use Result&lt;TError&gt; with custom error types instead.
/// </summary>
[Obsolete(
    "Use Result<TError> with custom error types instead of string-based errors. This type will be removed in a future version.",
    DiagnosticId = "DROPBEAR002")]
[ExcludeFromCodeCoverage] // Legacy code
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class Result : Base.Result<LegacyError>
{
    private static readonly ObjectPool<Result> Pool =
        ObjectPoolManager.GetPool(() => new Result(ResultState.Success, null, null));

    #region Constructors

    /// <summary>
    ///     Protected constructor for internal use.
    /// </summary>
    protected Result(
        ResultState state,
        string? error,
        Exception? exception)
        : base(state, error is null ? null : new LegacyError(error), exception)
    {
    }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the error message, maintaining backwards compatibility.
    /// </summary>
    public string ErrorMessage => Error?.Message ?? string.Empty;

    private string DebuggerDisplay =>
        $"State = {State}, Success = {IsSuccess}, Error = {ErrorMessage}";

    #endregion

    #region Factory Methods

    /// <summary>
    ///     Creates a successful result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new static Result Success()
    {
        return FromPool(ResultState.Success, null);
    }

    /// <summary>
    ///     Creates a failed result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result Failure(string error, Exception? exception = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return FromPool(ResultState.Failure, error, exception);
    }

    /// <summary>
    ///     Creates a failed result from multiple exceptions.
    /// </summary>
    public static Result Failure(IEnumerable<Exception> exceptions)
    {
        ArgumentNullException.ThrowIfNull(exceptions);

        var exceptionList = exceptions.ToList();
        if (exceptionList.Count == 0)
        {
            throw new ArgumentException("Exception collection cannot be empty", nameof(exceptions));
        }

        var primaryException = exceptionList.FirstOrDefault();
        var combinedErrorMessage = primaryException?.Message ?? "Multiple errors occurred";

        return FromPool(ResultState.Failure, combinedErrorMessage, primaryException);
    }

    /// <summary>
    ///     Creates a warning result.
    /// </summary>
    public static Result Warning(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return FromPool(ResultState.Warning, error);
    }

    /// <summary>
    ///     Creates a partial success result.
    /// </summary>
    public static Result PartialSuccess(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return FromPool(ResultState.PartialSuccess, error);
    }

    /// <summary>
    ///     Creates a cancelled result.
    /// </summary>
    public static Result Cancelled(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return FromPool(ResultState.Cancelled, error);
    }

    /// <summary>
    ///     Creates a pending result.
    /// </summary>
    public static Result Pending(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return FromPool(ResultState.Pending, error);
    }

    /// <summary>
    ///     Creates a no-op result.
    /// </summary>
    public static Result NoOp(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return FromPool(ResultState.NoOp, error);
    }

    #endregion

    #region Migration Helpers

    /// <summary>
    ///     Converts this legacy Result to a modern Result&lt;TError&gt;.
    /// </summary>
    /// <typeparam name="TError">The custom error type to convert to.</typeparam>
    /// <returns>A new Result&lt;TError&gt; with the same state and error information.</returns>
    public Base.Result<TError> ToModern<TError>()
        where TError : ResultError
    {
        if (IsSuccess)
        {
            return Base.Result<TError>.Success();
        }

        var error = (TError)Activator.CreateInstance(typeof(TError), ErrorMessage)!;

        return State switch
        {
            ResultState.Failure => Base.Result<TError>.Failure(error, Exception),
            ResultState.Warning => Base.Result<TError>.Warning(error),
            ResultState.PartialSuccess => Base.Result<TError>.PartialSuccess(error),
            ResultState.Cancelled => Base.Result<TError>.Cancelled(error),
            ResultState.Pending => Base.Result<TError>.Pending(error),
            ResultState.NoOp => Base.Result<TError>.NoOp(error),
            _ => Base.Result<TError>.Failure(error, Exception)
        };
    }

    /// <summary>
    ///     Creates a legacy Result from a modern Result&lt;TError&gt;.
    /// </summary>
    /// <typeparam name="TError">The custom error type.</typeparam>
    /// <param name="modernResult">The modern result to convert from.</param>
    /// <returns>A legacy Result with the same state and error information.</returns>
    public static Result FromModern<TError>(Base.Result<TError> modernResult)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(modernResult);

        if (modernResult.IsSuccess)
        {
            return Success();
        }

        var errorMessage = modernResult.Error?.Message ?? "Unknown error";

        return modernResult.State switch
        {
            ResultState.Failure => Failure(errorMessage, modernResult.Exception),
            ResultState.Warning => Warning(errorMessage),
            ResultState.PartialSuccess => PartialSuccess(errorMessage),
            ResultState.Cancelled => Cancelled(errorMessage),
            ResultState.Pending => Pending(errorMessage),
            ResultState.NoOp => NoOp(errorMessage),
            _ => Failure(errorMessage, modernResult.Exception)
        };
    }

    #endregion

    #region Chained Operations

    /// <summary>
    ///     Executes the given action if the result is in a Failure state.
    /// </summary>
    public Result OnFailure(Action<string, Exception?> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (State == ResultState.Failure)
        {
            SafeExecute(() => action(ErrorMessage, Exception));
        }

        return this;
    }

    /// <summary>
    ///     Executes the given action if the result is in a Success state.
    /// </summary>
    public void OnSuccess(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (IsSuccess)
        {
            SafeExecute(action);
        }
    }

    /// <summary>
    ///     Pattern matches the result state.
    /// </summary>
    public T Match<T>(
        Func<T> onSuccess,
        Func<string, Exception?, T> onFailure,
        Func<string, T>? onWarning = null,
        Func<string, T>? onPartialSuccess = null,
        Func<string, T>? onCancelled = null,
        Func<string, T>? onPending = null,
        Func<string, T>? onNoOp = null)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return base.Match(
            onSuccess,
            (err, ex) => onFailure(err.Message, ex),
            err => onWarning.Invoke(err.Message) ?? onFailure(err.Message, null),
            err => onPartialSuccess.Invoke(err.Message) ?? onFailure(err.Message, null),
            err => onCancelled.Invoke(err.Message) ?? onFailure(err.Message, null),
            err => onPending.Invoke(err.Message) ?? onFailure(err.Message, null),
            err => onNoOp.Invoke(err.Message) ?? onFailure(err.Message, null)
        );
    }

    #endregion

    #region Internal Helpers

    private static Result FromPool(ResultState state, string? error, Exception? exception = null)
    {
        var result = Pool.Get();

        // Call the base class Initialize method with the correct signature
        // The base Result<TError> has: Initialize(ResultState state, TError? error, Exception? exception)
        result.InitializeInternal(state, error is null ? null : new LegacyError(error), exception);

        return result;
    }

    #endregion
}
