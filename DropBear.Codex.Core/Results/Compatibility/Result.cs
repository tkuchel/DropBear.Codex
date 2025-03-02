#region

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Enums;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Core.Results.Compatibility;

/// <summary>
///     A backwards-compatible Result class that maintains the old API surface
///     while leveraging the new implementation internally.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class Result : Result<LegacyError>
{
    private static readonly ConcurrentDictionary<Type, DefaultObjectPool<Result>> ResultPool = new();

    protected Result(ResultState state, string? error, Exception? exception)
        : base(new LegacyError(error ?? string.Empty), state, error, exception)
    {
    }

    /// <summary>
    ///     Gets the error message, maintaining backwards compatibility.
    /// </summary>
    public new string ErrorMessage => Error?.Message ?? string.Empty;

    private string DebuggerDisplay => $"State = {State}, Success = {IsSuccess}, Error = {ErrorMessage}";

    private static DefaultObjectPool<Result> GetOrCreatePool(Type type)
    {
        return ResultPool.GetOrAdd(type, _ => new DefaultObjectPool<Result>(new ResultPooledObjectPolicy()));
    }

    private sealed class ResultPooledObjectPolicy : IPooledObjectPolicy<Result>
    {
        public Result Create()
        {
            return new Result(ResultState.Success, null, null);
        }

        public bool Return(Result obj)
        {
            obj.Initialize(ResultState.Success);
            return true;
        }
    }

    #region Static Factory Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new static Result Success()
    {
        var pool = GetOrCreatePool(typeof(Result));
        return pool.Get();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new static Result Failure(string error, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        var pool = GetOrCreatePool(typeof(Result));
        var result = pool.Get();
        result.Initialize(ResultState.Failure, new LegacyError(error), exception);
        return result;
    }

    public static Result Failure(IEnumerable<Exception> exceptions)
    {
        ArgumentNullException.ThrowIfNull(exceptions);
        var exceptionList = exceptions.ToList();
        var error = new LegacyError(exceptionList.FirstOrDefault()?.Message ?? "Multiple errors occurred");

        var pool = GetOrCreatePool(typeof(Result));
        var result = pool.Get();
        result.Initialize(ResultState.Failure, error, exceptionList.FirstOrDefault());
        return result;
    }

    public static Result Warning(string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var pool = GetOrCreatePool(typeof(Result));
        var result = pool.Get();
        result.Initialize(ResultState.Warning, new LegacyError(error));
        return result;
    }

    public static Result PartialSuccess(string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var pool = GetOrCreatePool(typeof(Result));
        var result = pool.Get();
        result.Initialize(ResultState.PartialSuccess, new LegacyError(error));
        return result;
    }

    public static Result Cancelled(string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var pool = GetOrCreatePool(typeof(Result));
        var result = pool.Get();
        result.Initialize(ResultState.Cancelled, new LegacyError(error));
        return result;
    }

    public static Result Pending(string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var pool = GetOrCreatePool(typeof(Result));
        var result = pool.Get();
        result.Initialize(ResultState.Pending, new LegacyError(error));
        return result;
    }

    public static Result NoOp(string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var pool = GetOrCreatePool(typeof(Result));
        var result = pool.Get();
        result.Initialize(ResultState.NoOp, new LegacyError(error));
        return result;
    }

    #endregion

    #region Chained Operations

    public Result OnFailure(Action<string, Exception?> action)
    {
        if (State == ResultState.Failure)
        {
            SafeExecute(() => action(ErrorMessage, Exception));
        }

        return this;
    }

    public void OnSuccess(Action action)
    {
        if (IsSuccess)
        {
            SafeExecute(action);
        }
    }

    public T Match<T>(
        Func<T> onSuccess,
        Func<string, Exception?, T> onFailure,
        Func<string, T>? onWarning = null,
        Func<string, T>? onPartialSuccess = null,
        Func<string, T>? onCancelled = null,
        Func<string, T>? onPending = null,
        Func<string, T>? onNoOp = null)
    {
        return base.Match(
            onSuccess,
            (error, ex) => onFailure(error.Message, ex),
            error => onWarning is not null ? onWarning(error.Message) : onFailure(error.Message, null),
            error => onPartialSuccess is not null ? onPartialSuccess(error.Message) : onFailure(error.Message, null),
            error => onCancelled is not null ? onCancelled(error.Message) : onFailure(error.Message, null),
            error => onPending is not null ? onPending(error.Message) : onFailure(error.Message, null),
            error => onNoOp is not null ? onNoOp(error.Message) : onFailure(error.Message, null));
    }

    #endregion
}
