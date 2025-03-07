#region

using System.Diagnostics;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Common;
using DropBear.Codex.Core.Enums;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Core.Results.Compatibility;

/// <summary>
///     A backwards-compatible, untyped Result class that leverages <see cref="LegacyError" />
///     and uses <see cref="ObjectPoolManager" /> under the hood.
///     Inherits from <c>Result&lt;LegacyError&gt;</c>, which defines <c>InitializeInternal(...)</c>.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class Result : Base.Result<LegacyError>
{
    // Single shared pool, created via ObjectPoolManager
    private static readonly ObjectPool<Result> Pool =
        ObjectPoolManager.GetPool(() => new Result(ResultState.Success, null, null));

    /// <summary>
    ///     Protected constructor bridging to the base <see cref="Result{TError}" />.
    /// </summary>
    /// <param name="state">The result state (e.g., Success, Failure).</param>
    /// <param name="error">An optional string error message.</param>
    /// <param name="exception">An optional exception if the result represents an error scenario.</param>
    protected Result(
            ResultState state,
            string? error,
            Exception? exception)
        // Convert `error` into a LegacyError if present, or null otherwise
        : base(state, error is null ? null : new LegacyError(error), exception)
    {
        // The base constructor sets this.Error (and calls ValidateErrorState(...)) as needed.
    }

    /// <summary>
    ///     Gets the error message, maintaining backwards compatibility.
    /// </summary>
    public string ErrorMessage => Error?.Message ?? string.Empty;

    private string DebuggerDisplay =>
        $"State = {State}, Success = {IsSuccess}, Error = {ErrorMessage}";

    /// <summary>
    ///     Central helper for creating a <see cref="Result" /> from the shared pool
    ///     and setting its <see cref="ResultState" />, error, and exception.
    /// </summary>
    private static Result FromPool(
        ResultState state,
        string? error,
        Exception? exception = null)
    {
        var result = Pool.Get();

        // The parent class "Result<TError>" defines "InitializeInternal" as:
        //   internal void InitializeInternal(
        //       ResultState newState, TError? newError = null, Exception? ex = null
        //   )
        // Here TError = LegacyError, so we pass either a new LegacyError or null.
        result.InitializeInternal(
            state,
            error is null ? null : new LegacyError(error),
            exception
        );
        return result;
    }

    #region Static Factory Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new static Result Success()
    {
        return FromPool(ResultState.Success, null);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result Failure(string error, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        return FromPool(ResultState.Failure, error, exception);
    }

    public static Result Failure(IEnumerable<Exception> exceptions)
    {
        ArgumentNullException.ThrowIfNull(exceptions);

        var exceptionList = exceptions.ToList();
        var primaryException = exceptionList.FirstOrDefault();
        var combinedErrorMessage = primaryException?.Message ?? "Multiple errors occurred";

        return FromPool(
            ResultState.Failure,
            combinedErrorMessage,
            primaryException
        );
    }

    public static Result Warning(string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return FromPool(ResultState.Warning, error);
    }

    public static Result PartialSuccess(string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return FromPool(ResultState.PartialSuccess, error);
    }

    public static Result Cancelled(string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return FromPool(ResultState.Cancelled, error);
    }

    public static Result Pending(string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return FromPool(ResultState.Pending, error);
    }

    public static Result NoOp(string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return FromPool(ResultState.NoOp, error);
    }

    #endregion

    #region Chained Operations

    /// <summary>
    ///     Executes the given action if the result is in a Failure state.
    /// </summary>
    public Result OnFailure(Action<string, Exception?> action)
    {
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
        if (IsSuccess)
        {
            SafeExecute(action);
        }
    }

    /// <summary>
    ///     Pattern matches the result state, providing callbacks for each scenario.
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
        // "base.Match" calls into Result<LegacyError>.Match, bridging error
        // to string for backwards compatibility.
        return base.Match(
            onSuccess,
            (err, ex) => onFailure(err.Message, ex),
            err => onWarning is not null
                ? onWarning(err.Message)
                : onFailure(err.Message, null),
            err => onPartialSuccess is not null
                ? onPartialSuccess(err.Message)
                : onFailure(err.Message, null),
            err => onCancelled is not null
                ? onCancelled(err.Message)
                : onFailure(err.Message, null),
            err => onPending is not null
                ? onPending(err.Message)
                : onFailure(err.Message, null),
            err => onNoOp is not null
                ? onNoOp(err.Message)
                : onFailure(err.Message, null)
        );
    }

    #endregion
}
