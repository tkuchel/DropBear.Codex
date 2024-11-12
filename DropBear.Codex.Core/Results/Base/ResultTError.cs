#region

using System.Collections.ObjectModel;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;

#endregion

namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     Base Result class supporting generic error types
/// </summary>
public class Result<TError> : ResultBase, IResult<TError> where TError : ResultError
{
    private readonly ReadOnlyCollection<Exception> _exceptions;

    protected Result(ResultState state, TError? error = default, Exception? exception = null)
        : base(state, exception)
    {
        if (state is ResultState.Failure or ResultState.PartialSuccess && error is null)
        {
            throw new ArgumentException("Error required for non-success results", nameof(error));
        }

        Error = error;
        _exceptions = new ReadOnlyCollection<Exception>(
            exception is null ? Array.Empty<Exception>() : new[] { exception });
    }

    public TError? Error { get; }
    public override IReadOnlyCollection<Exception> Exceptions => _exceptions;

    #region Factory Methods

    public static Result<TError> Success()
    {
        return new Result<TError>(ResultState.Success);
    }

    public static Result<TError> Failure(TError error, Exception? exception = null)
    {
        return new Result<TError>(ResultState.Failure, error, exception);
    }

    public static Result<TError> Warning(TError error)
    {
        return new Result<TError>(ResultState.Warning, error);
    }

    public static Result<TError> PartialSuccess(TError error)
    {
        return new Result<TError>(ResultState.PartialSuccess, error);
    }

    public static Result<TError> Cancelled(TError error)
    {
        return new Result<TError>(ResultState.Cancelled, error);
    }

    #endregion

    #region Operation Methods

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
            return this;
        }
    }

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
            return Failure(error, ex);
        }
    }

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
                _ => throw new InvalidOperationException($"Unhandled state: {State}")
            };
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during match operation");
            return onFailure(Error ?? CreateDefaultError(), ex);
        }

        T InvokeOrDefault(Func<TError, T>? handler, Func<TError, Exception?, T> defaultHandler)
        {
            return handler is not null ? handler(Error!) : defaultHandler(Error!, Exception);
        }
    }

    #endregion

    #region Protected Helper Methods

    protected static async ValueTask SafeExecuteAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during asynchronous execution");
        }
    }

    protected new static void SafeExecute(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during synchronous execution");
        }
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

        if (Error is null)
        {
            return false;
        }

        return Error.Equals(other);
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
               _exceptions.SequenceEqual(other._exceptions);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(State, Error, Exception, _exceptions);
    }

    #endregion
}
