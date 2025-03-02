#region

using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Core.Results.Compatibility;

/// <summary>
///     A backwards-compatible Result{T} class that can hold a success value of type T
///     or an error message. Supports enumeration (yielding the value if successful).
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class Result<T> : Result<T, LegacyError>, IEnumerable<T>
{
    private static readonly ConcurrentDictionary<Type, DefaultObjectPool<Result<T>>> ResultPool = new();

    protected Result(T value, ResultState state, string? error = null, Exception? exception = null)
        : base(new Lazy<T>(() => value), state, error is null ? null : new LegacyError(error), exception)
    {
    }

    public string ErrorMessage => Error?.Message ?? string.Empty;

    private string DebuggerDisplay =>
        $"State = {State}, Success = {IsSuccess}, Value = {(IsSuccess ? Value?.ToString() : "null")}, Error = {ErrorMessage}";

    private static DefaultObjectPool<Result<T>> GetOrCreatePool(Type type)
    {
        return ResultPool.GetOrAdd(type, _ => new DefaultObjectPool<Result<T>>(new ResultPooledObjectPolicy()));
    }

    private sealed class ResultPooledObjectPolicy : IPooledObjectPolicy<Result<T>>
    {
        private static readonly T DefaultValue = default!;

        public Result<T> Create()
        {
            return new Result<T>(DefaultValue, ResultState.Success);
        }

        public bool Return(Result<T> obj)
        {
            return true;
        }
    }

    #region Factory Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new static Result<T> Success(T value)
    {
        var pool = GetOrCreatePool(typeof(Result<T>));
        var result = pool.Get();
        result.Initialize(value, ResultState.Success);
        return result;
    }

    public static Result<T> Failure(string error, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        var pool = GetOrCreatePool(typeof(Result<T>));
        var result = pool.Get();
        result.Initialize(default(T)!, ResultState.Failure, new LegacyError(error), exception);
        return result;
    }

    public static Result<T> Failure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return Failure(exception.Message, exception);
    }

    public static Result<T> PartialSuccess(T value, string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var pool = GetOrCreatePool(typeof(Result<T>));
        var result = pool.Get();
        result.Initialize(value, ResultState.PartialSuccess, new LegacyError(error));
        return result;
    }

    public static Result<T> Warning(T value, string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var pool = GetOrCreatePool(typeof(Result<T>));
        var result = pool.Get();
        result.Initialize(value, ResultState.Warning, new LegacyError(error));
        return result;
    }

    public static Result<T> Cancelled(string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var pool = GetOrCreatePool(typeof(Result<T>));
        var result = pool.Get();
        result.Initialize(default(T)!, ResultState.Cancelled, new LegacyError(error));
        return result;
    }

    public static Result<T> Cancelled(T value, string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var pool = GetOrCreatePool(typeof(Result<T>));
        var result = pool.Get();
        result.Initialize(value, ResultState.Cancelled, new LegacyError(error));
        return result;
    }

    public static Result<T> Pending(string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var pool = GetOrCreatePool(typeof(Result<T>));
        var result = pool.Get();
        result.Initialize(default(T)!, ResultState.Pending, new LegacyError(error));
        return result;
    }

    public static Result<T> Pending(T value, string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var pool = GetOrCreatePool(typeof(Result<T>));
        var result = pool.Get();
        result.Initialize(value, ResultState.Pending, new LegacyError(error));
        return result;
    }

    public static Result<T> NoOp(string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var pool = GetOrCreatePool(typeof(Result<T>));
        var result = pool.Get();
        result.Initialize(default(T)!, ResultState.NoOp, new LegacyError(error));
        return result;
    }

    public static Result<T> NoOp(T value, string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var pool = GetOrCreatePool(typeof(Result<T>));
        var result = pool.Get();
        result.Initialize(value, ResultState.NoOp, new LegacyError(error));
        return result;
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

    #region Enumeration

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
