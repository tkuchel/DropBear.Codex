#region

using System.Collections.ObjectModel;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Core;

/// <summary>
///     Represents the outcome of an operation, including success, failure, and various other states.
/// </summary>
public class Result : IEquatable<Result>
{
    private protected static ILogger? Logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Result" /> class.
    /// </summary>
    /// <param name="state">The state of the result.</param>
    /// <param name="error">The error message, if any.</param>
    /// <param name="exception">The exception associated with the result, if any.</param>
    /// <exception cref="ArgumentException">Thrown if an error message is required but not provided.</exception>
    protected internal Result(ResultState state, string? error, Exception? exception)
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

    /// <summary>
    ///     Gets the state of the result.
    /// </summary>
    public ResultState State { get; }

    /// <summary>
    ///     Gets the error message associated with the result, if any.
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    ///     Gets the exception associated with the result, if any.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    ///     Gets the collection of exceptions associated with the result, if any.
    /// </summary>
    public ReadOnlyCollection<Exception> Exceptions { get; internal set; }

    /// <summary>
    ///     Gets a value indicating whether the result represents a success or partial success.
    /// </summary>
    public bool IsSuccess => State is ResultState.Success or ResultState.PartialSuccess;


    /// <summary>
    ///     Determines whether the specified result is equal to the current result.
    /// </summary>
    /// <param name="other">The result to compare with the current result.</param>
    /// <returns>True if the specified result is equal to the current result; otherwise, false.</returns>
    public bool Equals(Result? other)
    {
        return other is not null && State == other.State &&
               string.Equals(ErrorMessage, other.ErrorMessage, StringComparison.Ordinal) &&
               Equals(Exception, other.Exception) &&
               Exceptions.SequenceEqual(other.Exceptions);
    }


    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as Result);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(State, ErrorMessage, Exception);
    }

    /// <summary>
    ///     Creates a successful result.
    /// </summary>
    /// <returns>A new <see cref="Result" /> representing success.</returns>
    public static Result Success()
    {
        return new Result(ResultState.Success, string.Empty, null);
    }

    /// <summary>
    ///     Creates a failure result with the specified error message and exception.
    /// </summary>
    /// <param name="error">The error message describing the failure.</param>
    /// <param name="exception">The exception associated with the failure, if any.</param>
    /// <returns>A new <see cref="Result" /> representing failure.</returns>
    public static Result Failure(string error, Exception? exception = null)
    {
        return new Result(ResultState.Failure, error, exception);
    }

    /// <summary>
    ///     Creates a failure result with multiple exceptions.
    /// </summary>
    /// <param name="exceptions">The collection of exceptions that occurred.</param>
    /// <returns>A new <see cref="Result" /> representing failure with multiple exceptions.</returns>
    public static Result Failure(IEnumerable<Exception> exceptions)
    {
        var exceptionList = exceptions.ToList();
        var errorMessage = exceptionList.Count > 0 ? exceptionList[0].Message : "Multiple errors occurred.";
        return new Result(ResultState.Failure, errorMessage, exceptionList.FirstOrDefault())
        {
            Exceptions = new ReadOnlyCollection<Exception>(exceptionList)
        };
    }

    /// <summary>
    ///     Creates a warning result with the specified warning message.
    /// </summary>
    /// <param name="error">The warning message.</param>
    /// <returns>A new <see cref="Result" /> representing a warning.</returns>
    public static Result Warning(string error)
    {
        return new Result(ResultState.Warning, error, null);
    }

    /// <summary>
    ///     Creates a partially successful result with the specified error message.
    /// </summary>
    /// <param name="error">The error message describing the partial success.</param>
    /// <returns>A new <see cref="Result" /> representing partial success.</returns>
    public static Result PartialSuccess(string error)
    {
        return new Result(ResultState.PartialSuccess, error, null);
    }

    /// <summary>
    ///     Creates a cancelled result with the specified error message.
    /// </summary>
    /// <param name="error">The cancellation message.</param>
    /// <returns>A new <see cref="Result" /> representing cancellation.</returns>
    public static Result Cancelled(string error)
    {
        return new Result(ResultState.Cancelled, error, null);
    }

    /// <summary>
    ///     Executes an action if the result is a failure.
    /// </summary>
    /// <param name="action">The action to execute, receiving the error message and exception.</param>
    /// <returns>The current <see cref="Result" />.</returns>
    public Result OnFailure(Action<string, Exception?> action)
    {
        if (State is ResultState.Failure)
        {
            SafeExecute(() => action(ErrorMessage, Exception));
        }

        return this;
    }

    /// <summary>
    ///     Executes an action if the result is a success.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    public void OnSuccess(Action action)
    {
        if (IsSuccess)
        {
            SafeExecute(action);
        }
    }

    /// <summary>
    ///     Matches the current result to a corresponding function based on its state.
    /// </summary>
    /// <typeparam name="T">The type of the result returned by the functions.</typeparam>
    /// <param name="onSuccess">The function to execute if the result is a success.</param>
    /// <param name="onFailure">The function to execute if the result is a failure.</param>
    /// <param name="onWarning">The function to execute if the result is a warning.</param>
    /// <param name="onPartialSuccess">The function to execute if the result is a partial success.</param>
    /// <param name="onCancelled">The function to execute if the result is cancelled.</param>
    /// <param name="onPending">The function to execute if the result is pending.</param>
    /// <param name="onNoOp">The function to execute if the result is a no-op.</param>
    /// <returns>The result of the corresponding function based on the state.</returns>
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

    /// <summary>
    ///     Safely executes an action, logging any exceptions that occur.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    private void SafeExecute(Action action)
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

    /// <summary>
    ///     Safely executes an asynchronous action, logging any exceptions that occur.
    /// </summary>
    /// <param name="action">The asynchronous action to execute.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task SafeExecuteAsync(Func<Task> action)
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
}
