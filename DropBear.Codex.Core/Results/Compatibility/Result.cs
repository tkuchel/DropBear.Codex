#region

using System.Collections.ObjectModel;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Core.Results.Compatibility;

/// <summary>
///     Backwards-compatible Result class using DefaultError type
/// </summary>
public class Result
{
    private protected static ILogger? Logger;

    protected Result(ResultState state, string? error, Exception? exception)
    {
        if (state is ResultState.Failure or ResultState.PartialSuccess && string.IsNullOrEmpty(error))
        {
            throw new ArgumentException("An error message must be provided for non-success results.", nameof(error));
        }

        State = state;
        ErrorMessage = error ?? string.Empty;
        Exception = exception;
        Exceptions = new ReadOnlyCollection<Exception>(new List<Exception>());
        Logger = LoggerFactory.Logger.ForContext<Result>();
    }

    public ResultState State { get; }

    public string ErrorMessage { get; }

    public Exception? Exception { get; }

    protected ReadOnlyCollection<Exception> Exceptions { get; init; }

    public bool IsSuccess => State is ResultState.Success or ResultState.PartialSuccess;

    public static Result Success()
    {
        return new Result(ResultState.Success, null, null);
    }

    public static Result Failure(string error, Exception? exception = null)
    {
        return new Result(ResultState.Failure, error, exception);
    }

    public static Result Failure(IEnumerable<Exception> exceptions)
    {
        var exceptionList = exceptions.ToList();
        var errorMessage = exceptionList.Count > 0 ? exceptionList[0].Message : "Multiple errors occurred.";
        return new Result(ResultState.Failure, errorMessage, exceptionList.FirstOrDefault())
        {
            Exceptions = new ReadOnlyCollection<Exception>(exceptionList)
        };
    }

    public static Result Warning(string error)
    {
        return new Result(ResultState.Warning, error, null);
    }

    public static Result PartialSuccess(string error)
    {
        return new Result(ResultState.PartialSuccess, error, null);
    }

    public static Result Cancelled(string error)
    {
        return new Result(ResultState.Cancelled, error, null);
    }

    public Result OnFailure(Action<string, Exception?> action)
    {
        if (State is ResultState.Failure)
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
        return State switch
        {
            ResultState.Success => onSuccess(),
            ResultState.Failure => onFailure(ErrorMessage, Exception),
            ResultState.Warning => InvokeOrDefault(onWarning, onFailure),
            ResultState.PartialSuccess => InvokeOrDefault(onPartialSuccess, onFailure),
            ResultState.Cancelled => InvokeOrDefault(onCancelled, onFailure),
            ResultState.Pending => InvokeOrDefault(onPending, onFailure),
            ResultState.NoOp => InvokeOrDefault(onNoOp, onFailure),
            _ => throw new InvalidOperationException("Unhandled result state.")
        };

        T InvokeOrDefault(Func<string, T>? handler, Func<string, Exception?, T> defaultHandler)
        {
            return handler != null ? handler(ErrorMessage) : defaultHandler(ErrorMessage, Exception);
        }
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

    public override bool Equals(object? obj)
    {
        if (obj is not Result other)
        {
            return false;
        }

        return State == other.State &&
               string.Equals(ErrorMessage, other.ErrorMessage, StringComparison.Ordinal) &&
               Equals(Exception, other.Exception) &&
               Exceptions.SequenceEqual(other.Exceptions);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(State, ErrorMessage, Exception);
    }
}
