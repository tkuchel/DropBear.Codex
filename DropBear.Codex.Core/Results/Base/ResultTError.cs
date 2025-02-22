#region

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Compatibility;
using DropBear.Codex.Core.Results.Errors;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     A base Result implementation parameterized by an error type.
/// </summary>
/// <typeparam name="TError">A type inheriting from ResultError.</typeparam>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[JsonConverter(typeof(ResultJsonConverter<>))]
public class Result<TError> : ResultBase, IResult<TError>
    where TError : ResultError
{
    private static readonly ConcurrentDictionary<Type, DefaultObjectPool<Result<TError>>> ResultPool = new();
    private readonly ReadOnlyCollection<Exception> _exceptions;


    #region Protected Methods

    /// <summary>
    ///     Initializes or reinitializes the result instance.
    /// </summary>
    protected virtual void Initialize(ResultState state, TError? error = null, Exception? exception = null)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;

        // Use field info variables to avoid repetitive lookups
        var stateField = GetType().GetField("_state", flags);
        var errorField = GetType().GetField("_error", flags);
        var exceptionField = GetType().GetField("_exception", flags);

        stateField?.SetValue(this, state);
        errorField?.SetValue(this, error);
        exceptionField?.SetValue(this, exception);
    }

    #endregion

    private sealed class ResultPooledObjectPolicy : IPooledObjectPolicy<Result<TError>>
    {
        public Result<TError> Create()
        {
            return new Result<TError>(ResultState.Success);
        }

        public bool Return(Result<TError> obj)
        {
            obj.Initialize(ResultState.Success);
            return true;
        }
    }

    #region Constructors and Initialization

    /// <summary>
    ///     Initializes a new instance of Result{TError}.
    /// </summary>
    protected Result(ResultState state, TError? error = null, Exception? exception = null)
        : base(state, exception)
    {
        ValidateErrorState(state, error);
        Error = error;
        _exceptions = new ReadOnlyCollection<Exception>(CreateExceptionList(exception));
    }

    private static void ValidateErrorState(ResultState state, TError? error)
    {
        if (state is ResultState.Failure or ResultState.PartialSuccess && error is null)
        {
            throw new ResultValidationException("Error is required for non-success results");
        }
    }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the error object if the result is unsuccessful.
    /// </summary>
    public TError? Error { get; }

    /// <summary>
    ///     Gets the read-only collection of exceptions associated with this result.
    /// </summary>
    public override IReadOnlyCollection<Exception> Exceptions => _exceptions;

    #endregion

    #region Factory Methods

    /// <summary>
    ///     Creates a new Result in the Success state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TError> Success()
    {
        var pool = GetOrCreatePool(typeof(Result<TError>));
        var result = pool.Get();
        return result;
    }

    /// <summary>
    ///     Creates a new Result in the Failure state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TError> Failure(TError error, Exception? exception = null)
    {
        var pool = GetOrCreatePool(typeof(Result<TError>));
        var result = pool.Get();
        result.Initialize(ResultState.Failure, error, exception);
        return result;
    }

    /// <summary>
    ///     Creates a new Result in the Warning state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TError> Warning(TError error)
    {
        var pool = GetOrCreatePool(typeof(Result<TError>));
        var result = pool.Get();
        result.Initialize(ResultState.Warning, error);
        return result;
    }

    /// <summary>
    ///     Creates a new Result in the PartialSuccess state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TError> PartialSuccess(TError error)
    {
        var pool = GetOrCreatePool(typeof(Result<TError>));
        var result = pool.Get();
        result.Initialize(ResultState.PartialSuccess, error);
        return result;
    }

    /// <summary>
    ///     Creates a new Result in the Cancelled state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TError> Cancelled(TError error)
    {
        var pool = GetOrCreatePool(typeof(Result<TError>));
        var result = pool.Get();
        result.Initialize(ResultState.Cancelled, error);
        return result;
    }

    #endregion

    #region Operation Methods

    /// <summary>
    ///     Attempts to recover from a failure by invoking a recovery function.
    /// </summary>
    public Result<TError> Recover(Func<TError, Exception?, Result<TError>> recovery)
    {
        if (IsSuccess)
        {
            return this;
        }

        try
        {
            return recovery(Error!, Exception);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during recovery");
            Telemetry.TrackException(ex, State, GetType());
            return this;
        }
    }

    /// <summary>
    ///     Ensures a specified condition is met.
    /// </summary>
    public Result<TError> Ensure(Func<bool> predicate, TError error)
    {
        if (!IsSuccess)
        {
            return this;
        }

        try
        {
            return predicate() ? this : Failure(error);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during ensure predicate");
            Telemetry.TrackException(ex, State, GetType());
            return Failure(error, ex);
        }
    }

    /// <summary>
    ///     Performs pattern matching on the current state.
    /// </summary>
    public T Match<T>(
        Func<T> onSuccess,
        Func<TError, Exception?, T> onFailure,
        Func<TError, T>? onWarning = null,
        Func<TError, T>? onPartialSuccess = null,
        Func<TError, T>? onCancelled = null,
        Func<TError, T>? onPending = null,
        Func<TError, T>? onNoOp = null)
    {
        try
        {
            return State switch
            {
                ResultState.Success => onSuccess(),
                ResultState.Failure => onFailure(Error!, Exception),
                ResultState.Warning => InvokeOrDefault(onWarning, onFailure),
                ResultState.PartialSuccess => InvokeOrDefault(onPartialSuccess, onFailure),
                ResultState.Cancelled => InvokeOrDefault(onCancelled, onFailure),
                ResultState.Pending => InvokeOrDefault(onPending, onFailure),
                ResultState.NoOp => InvokeOrDefault(onNoOp, onFailure),
                _ => throw new ResultException($"Unhandled state: {State}")
            };
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during match operation");
            Telemetry.TrackException(ex, State, GetType());
            return onFailure(Error ?? CreateDefaultError(), ex);
        }

        T InvokeOrDefault(Func<TError, T>? handler, Func<TError, Exception?, T> defaultHandler)
        {
            return handler is not null ? handler(Error!) : defaultHandler(Error!, Exception);
        }
    }

    #endregion

    #region Private Methods

    private static List<Exception> CreateExceptionList(Exception? exception)
    {
        if (exception == null)
        {
            return new List<Exception>();
        }

        if (exception is AggregateException aggregateException)
        {
            return [..aggregateException.InnerExceptions];
        }

        return [exception];
    }

    private static DefaultObjectPool<Result<TError>> GetOrCreatePool(Type type)
    {
        return ResultPool.GetOrAdd(type, _ =>
            new DefaultObjectPool<Result<TError>>(new ResultPooledObjectPolicy()));
    }

    private TError CreateDefaultError()
    {
        return (TError)Activator.CreateInstance(typeof(TError), "Operation failed with unhandled exception")!;
    }

    #endregion

    #region Equality Members

    public bool Equals(TError? other)
    {
        if (other is null)
        {
            return false;
        }

        return Error is not null && Error.Equals(other);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj is not Result<TError> other)
        {
            return false;
        }

        return State == other.State &&
               EqualityComparer<TError?>.Default.Equals(Error, other.Error) &&
               Equals(Exception, other.Exception) &&
               Exceptions.SequenceEqual(other.Exceptions);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(State, Error, Exception, Exceptions);
    }

    #endregion

    #region Debugging Support

    private string DebuggerDisplay => $"State = {State}, Error = {Error?.Message ?? "None"}";

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Dictionary<string, object?> DebugView =>
        new(StringComparer.Ordinal)
        {
            { "State", State },
            { "IsSuccess", IsSuccess },
            { "Error", Error?.Message },
            { "Exception", Exception?.Message },
            { "ExceptionCount", Exceptions.Count }
        };

    #endregion
}
