#region

using System.Collections.ObjectModel;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     Base Result class supporting generic error types
/// </summary>
public class Result<TError> : IEquatable<Result<TError>?> where TError : ResultError
{
    private protected static ILogger? Logger;

    protected Result(ResultState state, TError? error, Exception? exception)
    {
        if (state is ResultState.Failure or ResultState.PartialSuccess && error is null)
        {
            throw new ArgumentException("An error must be provided for non-success results.", nameof(error));
        }

        State = state;
        Error = error;
        Exception = exception;
        Exceptions = new ReadOnlyCollection<Exception>(new List<Exception>());
        Logger = LoggerFactory.Logger.ForContext<Result<TError>>();
    }

    public ResultState State { get; }
    public TError? Error { get; }
    public Exception? Exception { get; }

    protected ReadOnlyCollection<Exception> Exceptions { get; init; }

    public bool IsSuccess => State is ResultState.Success or ResultState.PartialSuccess;

    public bool Equals(Result<TError>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return State == other.State &&
               EqualityComparer<TError>.Default.Equals(Error, other.Error) &&
               EqualityComparer<Exception>.Default.Equals(Exception, other.Exception) &&
               Exceptions.SequenceEqual(other.Exceptions);
    }

    public Result<TError> Recover(Func<TError, Exception?, Result<TError>> recovery)
    {
        return IsSuccess ? this : recovery(Error!, Exception);
    }

    public Result<TError> Ensure(Func<bool> predicate, TError error)
    {
        if (!IsSuccess)
        {
            return this;
        }

        return predicate() ? this : Failure(error);
    }

    protected static void SafeExecute(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Logger?.Error(ex, "Exception during action execution.");
        }
    }

    protected static async Task SafeExecuteAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger?.Error(ex, "Exception during asynchronous action execution.");
        }
    }

    public static Result<TError> Success()
    {
        return new Result<TError>(ResultState.Success, default, null);
    }

    public static Result<TError> Failure(TError error, Exception? exception = null)
    {
        return new Result<TError>(ResultState.Failure, error, exception);
    }

    public static Result<TError> Warning(TError error)
    {
        return new Result<TError>(ResultState.Warning, error, null);
    }

    public static Result<TError> PartialSuccess(TError error)
    {
        return new Result<TError>(ResultState.PartialSuccess, error, null);
    }

    public static Result<TError> Cancelled(TError error)
    {
        return new Result<TError>(ResultState.Cancelled, error, null);
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
        return State switch
        {
            ResultState.Success => onSuccess(),
            ResultState.Failure => onFailure(Error!, Exception),
            ResultState.Warning => InvokeOrDefault(onWarning, onFailure),
            ResultState.PartialSuccess => InvokeOrDefault(onPartialSuccess, onFailure),
            ResultState.Cancelled => InvokeOrDefault(onCancelled, onFailure),
            ResultState.Pending => InvokeOrDefault(onPending, onFailure),
            ResultState.NoOp => InvokeOrDefault(onNoOp, onFailure),
            _ => throw new InvalidOperationException("Unhandled result state.")
        };

        T InvokeOrDefault(Func<TError, T>? handler, Func<TError, Exception?, T> defaultHandler)
        {
            return handler != null ? handler(Error!) : defaultHandler(Error!, Exception);
        }
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

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals(obj as Result<TError>);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(State, Error, Exception, Exceptions);
    }
}
