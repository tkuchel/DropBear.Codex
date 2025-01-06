#region

using System.Collections.ObjectModel;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;

#endregion

namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     A base <see cref="ResultBase" /> implementation parameterized by an error type <typeparamref name="TError" />,
///     implementing <see cref="IResult{TError}" />.
/// </summary>
/// <typeparam name="TError">A type inheriting from <see cref="ResultError" />.</typeparam>
public class Result<TError> : ResultBase, IResult<TError>
    where TError : ResultError
{
    private readonly ReadOnlyCollection<Exception> _exceptions;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Result{TError}" /> class.
    /// </summary>
    /// <param name="state">The <see cref="ResultState" /> (e.g., Success, Failure, etc.).</param>
    /// <param name="error">
    ///     The <typeparamref name="TError" /> object describing the error if this is not a success state.
    ///     Must be non-null if <paramref name="state" /> indicates a failure or partial success.
    /// </param>
    /// <param name="exception">An optional <see cref="Exception" /> for additional context on the failure.</param>
    /// <exception cref="ArgumentException">
    ///     Thrown if <paramref name="state" /> is non-success and <paramref name="error" /> is <c>null</c>.
    /// </exception>
    protected Result(ResultState state, TError? error = null, Exception? exception = null)
        : base(state, exception)
    {
        if (state is ResultState.Failure or ResultState.PartialSuccess && error is null)
        {
            throw new ArgumentException("Error is required for non-success results", nameof(error));
        }

        Error = error;
        _exceptions = new ReadOnlyCollection<Exception>(
            exception is null
                ? Array.Empty<Exception>()
                : new[] { exception });
    }

    /// <summary>
    ///     Gets the error object (of type <typeparamref name="TError" />) if the result is unsuccessful.
    /// </summary>
    public TError? Error { get; }

    /// <summary>
    ///     Gets the read-only collection of exceptions associated with this result.
    ///     For most scenarios, this contains at most one exception.
    /// </summary>
    public override IReadOnlyCollection<Exception> Exceptions => _exceptions;

    #region Factory Methods

    /// <summary>
    ///     Creates a new <see cref="Result{TError}" /> in the Success state (no error).
    /// </summary>
    public static Result<TError> Success()
    {
        return new Result<TError>(ResultState.Success);
    }

    /// <summary>
    ///     Creates a new <see cref="Result{TError}" /> in the Failure state with the given <paramref name="error" />.
    /// </summary>
    /// <param name="error">A <typeparamref name="TError" /> describing the failure.</param>
    /// <param name="exception">An optional <see cref="Exception" /> for additional context.</param>
    public static Result<TError> Failure(TError error, Exception? exception = null)
    {
        return new Result<TError>(ResultState.Failure, error, exception);
    }

    /// <summary>
    ///     Creates a new <see cref="Result{TError}" /> in the Warning state with the given <paramref name="error" />.
    /// </summary>
    /// <param name="error">A <typeparamref name="TError" /> describing the warning condition.</param>
    public static Result<TError> Warning(TError error)
    {
        return new Result<TError>(ResultState.Warning, error);
    }

    /// <summary>
    ///     Creates a new <see cref="Result{TError}" /> in the PartialSuccess state.
    ///     Commonly used when an operation completes with some minor or partial errors.
    /// </summary>
    /// <param name="error">A <typeparamref name="TError" /> describing the partial success condition.</param>
    public static Result<TError> PartialSuccess(TError error)
    {
        return new Result<TError>(ResultState.PartialSuccess, error);
    }

    /// <summary>
    ///     Creates a new <see cref="Result{TError}" /> in the Cancelled state,
    ///     typically indicating user-initiated cancellation or a task cancellation.
    /// </summary>
    /// <param name="error">A <typeparamref name="TError" /> describing the cancellation.</param>
    public static Result<TError> Cancelled(TError error)
    {
        return new Result<TError>(ResultState.Cancelled, error);
    }

    #endregion

    #region Operation Methods

    /// <summary>
    ///     Attempts to recover from a failure by invoking <paramref name="recovery" />
    ///     if this result is not successful, returning the new result.
    /// </summary>
    /// <param name="recovery">
    ///     A function taking the current error and exception to produce a new <see cref="Result{TError}" />
    ///     .
    /// </param>
    /// <returns>The original result if successful, or the new result from <paramref name="recovery" /> if failed.</returns>
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

    /// <summary>
    ///     Ensures a specified condition is met; if not, returns a failure result.
    /// </summary>
    /// <param name="predicate">A function that returns <c>true</c> if the condition is met.</param>
    /// <param name="error">The <typeparamref name="TError" /> to use if the condition fails.</param>
    /// <returns>This result if already failed or if the condition is met; otherwise a new Failure result.</returns>
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

    /// <summary>
    ///     Performs a pattern match on the current <see cref="ResultBase.State" />, invoking the respective callback.
    ///     If any callback is not provided, a default fallback is used (the <paramref name="onFailure" /> callback).
    /// </summary>
    /// <typeparam name="T">The return type of each branch.</typeparam>
    /// <param name="onSuccess">Invoked if the state is <see cref="ResultState.Success" />.</param>
    /// <param name="onFailure">Invoked if the state is <see cref="ResultState.Failure" />.</param>
    /// <param name="onWarning">Optional callback for <see cref="ResultState.Warning" />.</param>
    /// <param name="onPartialSuccess">Optional callback for <see cref="ResultState.PartialSuccess" />.</param>
    /// <param name="onCancelled">Optional callback for <see cref="ResultState.Cancelled" />.</param>
    /// <param name="onPending">Optional callback for <see cref="ResultState.Pending" />.</param>
    /// <param name="onNoOp">Optional callback for <see cref="ResultState.NoOp" />.</param>
    /// <returns>The result of whichever delegate is invoked, typed as <typeparamref name="T" />.</returns>
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

    /// <summary>
    ///     Executes a task asynchronously, logging any exceptions that occur.
    ///     This local override is used by derived classes.
    /// </summary>
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

    /// <summary>
    ///     Executes a synchronous action, logging any exceptions that occur.
    ///     This local override is used by derived classes.
    /// </summary>
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

    /// <summary>
    ///     Compares the specified <paramref name="other" /> error with this instance's error for equality.
    /// </summary>
    /// <param name="other">Another error to compare against.</param>
    /// <returns><c>true</c> if equal, otherwise <c>false</c>.</returns>
    public bool Equals(TError? other)
    {
        if (other is null)
        {
            return false;
        }

        return Error is not null && Error.Equals(other);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(State, Error, Exception, Exceptions);
    }

    #endregion
}
