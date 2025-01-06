#region

using System.Collections.ObjectModel;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Core.Results.Compatibility;

/// <summary>
///     A backwards-compatible <c>Result</c> class that does not use generic error types.
///     This class retains older APIs for smooth migration to newer, generic-based results.
/// </summary>
public class Result
{
    private static ILogger? _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Result" /> class.
    /// </summary>
    /// <param name="state">The <see cref="ResultState" /> (e.g., Success, Failure, etc.).</param>
    /// <param name="error">
    ///     A string describing the error, if <paramref name="state" /> is a failure-like state.
    /// </param>
    /// <param name="exception">An optional exception associated with this result.</param>
    /// <exception cref="ArgumentException">
    ///     Thrown if <paramref name="state" /> indicates a failure or partial success but <paramref name="error" /> is null or
    ///     empty.
    /// </exception>
    protected Result(ResultState state, string? error, Exception? exception)
    {
        if (state is ResultState.Failure or ResultState.PartialSuccess && string.IsNullOrEmpty(error))
        {
            throw new ArgumentException(
                "An error message must be provided for non-success results.",
                nameof(error));
        }

        State = state;
        ErrorMessage = error ?? string.Empty;
        Exception = exception;
        Exceptions = new ReadOnlyCollection<Exception>(new List<Exception>()); // empty by default

        // Initialize the logger once
        _logger = LoggerFactory.Logger.ForContext<Result>();
    }

    /// <summary>
    ///     Gets the <see cref="ResultState" /> of this result.
    /// </summary>
    private ResultState State { get; }

    /// <summary>
    ///     Gets the error message, if any, associated with this result.
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    ///     Gets an optional exception that provides additional context for a failure.
    /// </summary>
    protected Exception? Exception { get; }

    /// <summary>
    ///     A collection of exceptions if multiple errors occurred. Typically empty unless
    ///     <see cref="Failure(IEnumerable{Exception})" /> was used.
    /// </summary>
    protected ReadOnlyCollection<Exception> Exceptions { get; init; }

    /// <summary>
    ///     Indicates whether the result is considered successful (<see cref="ResultState.Success" /> or
    ///     <see cref="ResultState.PartialSuccess" />).
    /// </summary>
    public bool IsSuccess => State is ResultState.Success or ResultState.PartialSuccess;

    #region Static Factory Methods

    /// <summary>
    ///     Creates a new <see cref="Result" /> in the <see cref="ResultState.Success" /> state.
    /// </summary>
    public static Result Success()
    {
        return new Result(ResultState.Success, null, null);
    }

    /// <summary>
    ///     Creates a new <see cref="Result" /> in the <see cref="ResultState.Failure" /> state with an error message and
    ///     optional exception.
    /// </summary>
    public static Result Failure(string error, Exception? exception = null)
    {
        return new Result(ResultState.Failure, error, exception);
    }

    /// <summary>
    ///     Creates a new <see cref="Result" /> in the <see cref="ResultState.Failure" /> state from a collection of
    ///     exceptions.
    /// </summary>
    public static Result Failure(IEnumerable<Exception> exceptions)
    {
        var exceptionList = exceptions.ToList();
        var errorMessage = exceptionList.Count > 0
            ? exceptionList[0].Message
            : "Multiple errors occurred.";
        return new Result(ResultState.Failure, errorMessage, exceptionList.FirstOrDefault())
        {
            Exceptions = new ReadOnlyCollection<Exception>(exceptionList)
        };
    }

    /// <summary>
    ///     Creates a new <see cref="Result" /> in the <see cref="ResultState.Warning" /> state with an error message.
    /// </summary>
    public static Result Warning(string error)
    {
        return new Result(ResultState.Warning, error, null);
    }

    /// <summary>
    ///     Creates a new <see cref="Result" /> in the <see cref="ResultState.PartialSuccess" /> state with an error message.
    /// </summary>
    public static Result PartialSuccess(string error)
    {
        return new Result(ResultState.PartialSuccess, error, null);
    }

    /// <summary>
    ///     Creates a new <see cref="Result" /> in the <see cref="ResultState.Cancelled" /> state with an error message.
    /// </summary>
    public static Result Cancelled(string error)
    {
        return new Result(ResultState.Cancelled, error, null);
    }

    #endregion

    #region Chained Operations

    /// <summary>
    ///     Executes the specified action if the result state is <see cref="ResultState.Failure" />.
    /// </summary>
    /// <param name="action">
    ///     A callback that receives the error message and exception.
    /// </param>
    /// <returns>This <see cref="Result" /> for chaining.</returns>
    public Result OnFailure(Action<string, Exception?> action)
    {
        if (State == ResultState.Failure)
        {
            SafeExecute(() => action(ErrorMessage, Exception));
        }

        return this;
    }

    /// <summary>
    ///     Executes the specified action if the result is considered successful.
    /// </summary>
    /// <param name="action">A callback to execute on success.</param>
    public void OnSuccess(Action action)
    {
        if (IsSuccess)
        {
            SafeExecute(action);
        }
    }

    /// <summary>
    ///     Pattern matching method that invokes the appropriate function based on the <see cref="State" />.
    /// </summary>
    /// <typeparam name="T">The type returned by each callback.</typeparam>
    /// <param name="onSuccess">Callback for <see cref="ResultState.Success" />.</param>
    /// <param name="onFailure">Callback for <see cref="ResultState.Failure" />.</param>
    /// <param name="onWarning">Callback for <see cref="ResultState.Warning" /> (optional).</param>
    /// <param name="onPartialSuccess">Callback for <see cref="ResultState.PartialSuccess" /> (optional).</param>
    /// <param name="onCancelled">Callback for <see cref="ResultState.Cancelled" /> (optional).</param>
    /// <param name="onPending">Callback for <see cref="ResultState.Pending" /> (optional).</param>
    /// <param name="onNoOp">Callback for <see cref="ResultState.NoOp" /> (optional).</param>
    /// <returns>The result of whichever callback is chosen.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the state is unhandled.</exception>
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

    #endregion

    #region Protected Helpers

    /// <summary>
    ///     Executes an action, logging any exception that occurs.
    /// </summary>
    private static void SafeExecute(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Exception during action execution.");
        }
    }

    /// <summary>
    ///     Executes an async function, logging any exception that occurs.
    /// </summary>
    protected static async Task SafeExecuteAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Exception during asynchronous action execution.");
        }
    }

    #endregion

    #region Equality

    /// <inheritdoc />
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

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(State, ErrorMessage, Exception);
    }

    #endregion
}
