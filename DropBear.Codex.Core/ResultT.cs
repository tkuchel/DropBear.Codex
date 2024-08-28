#region

using System.Collections;
using System.Collections.ObjectModel;
using DropBear.Codex.Core.Enums;

#endregion

namespace DropBear.Codex.Core;

/// <summary>
///     Represents the outcome of an operation that returns a value of type <typeparamref name="T" />.
/// </summary>
/// <typeparam name="T">The type of the value returned by the operation.</typeparam>
#pragma warning disable MA0048
public class Result<T> : Result, IEquatable<Result<T>>, IEnumerable<T>
#pragma warning restore MA0048
{
    private Result(T value, string? error, Exception? exception, ResultState state)
        : base(state, error, exception)
    {
        Value = value;
    }

    /// <summary>
    ///     Gets the value returned by the operation, if successful.
    /// </summary>
    public T Value { get; }

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator()
    {
        if (IsSuccess)
        {
            yield return Value;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    ///     Determines whether the specified result is equal to the current result.
    /// </summary>
    /// <param name="other">The result to compare with the current result.</param>
    /// <returns>True if the specified result is equal to the current result; otherwise, false.</returns>
    public bool Equals(Result<T>? other)
    {
        return base.Equals(other) && EqualityComparer<T>.Default.Equals(Value, other!.Value);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as Result<T>);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Value);
    }

    /// <summary>
    ///     Returns the value if the result is successful; otherwise, returns the specified default value.
    /// </summary>
    /// <param name="defaultValue">The default value to return if the result is not successful.</param>
    /// <returns>The value of the result if successful; otherwise, the specified default value.</returns>
    public T ValueOrDefault(T defaultValue = default!)
    {
        return IsSuccess ? Value : defaultValue;
    }

    /// <summary>
    ///     Returns the value if the result is successful; otherwise, throws an <see cref="InvalidOperationException" />.
    /// </summary>
    /// <param name="errorMessage">The error message to include in the exception if the result is not successful.</param>
    /// <returns>The value of the result if successful.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the result is not successful.</exception>
    public T ValueOrThrow(string? errorMessage = null)
    {
        if (IsSuccess)
        {
            return Value;
        }

        throw new InvalidOperationException(
            errorMessage ?? ErrorMessage ?? "Operation failed without an error message.");
    }

    /// <summary>
    ///     Applies a function to the value of the result if it is successful, returning a new result of type
    ///     <typeparamref name="TOut" />.
    /// </summary>
    /// <typeparam name="TOut">The type of the value returned by the function.</typeparam>
    /// <param name="func">The function to apply to the value of the result.</param>
    /// <returns>
    ///     A new result containing the value returned by the function, or a failure result if the original result was not
    ///     successful.
    /// </returns>
    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> func)
    {
        return IsSuccess ? func(Value) : Result<TOut>.Failure(ErrorMessage, Exception);
    }

    /// <inheritdoc />
    public new Result OnSuccess(Action action)
    {
        base.OnSuccess(action);
        return this;
    }

    /// <summary>
    ///     Executes the specified action if the result is successful.
    /// </summary>
    /// <param name="action">The action to execute if the result is successful.</param>
    /// <returns>The current result.</returns>
    public Result OnSuccess(Func<T, Result> action)
    {
        return IsSuccess ? SafeExecute(() => action(Value)) : this;
    }

    /// <summary>
    ///     Executes the specified function if the result is successful, returning a new result of type
    ///     <typeparamref name="TOut" />.
    /// </summary>
    /// <typeparam name="TOut">The type of the value returned by the function.</typeparam>
    /// <param name="func">The function to execute if the result is successful.</param>
    /// <returns>
    ///     A new result containing the value returned by the function, or a failure result if the original result was not
    ///     successful.
    /// </returns>
    public Result<TOut> OnSuccess<TOut>(Func<T, Result<TOut>> func)
    {
        return IsSuccess ? SafeExecute(() => func(Value)) : Result<TOut>.Failure(ErrorMessage, Exception);
    }

    /// <inheritdoc />
    public new Result<T> OnFailure(Action<string, Exception?> action)
    {
        base.OnFailure(action);
        return this;
    }

    /// <summary>
    ///     Matches the current result to a corresponding function based on its state.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned by the functions.</typeparam>
    /// <param name="onSuccess">The function to execute if the result is a success.</param>
    /// <param name="onFailure">The function to execute if the result is a failure.</param>
    /// <param name="onWarning">The function to execute if the result is a warning.</param>
    /// <param name="onPartialSuccess">The function to execute if the result is a partial success.</param>
    /// <param name="onCancelled">The function to execute if the result is cancelled.</param>
    /// <param name="onPending">The function to execute if the result is pending.</param>
    /// <param name="onNoOp">The function to execute if the result is a no-op.</param>
    /// <returns>The result of the corresponding function based on the state.</returns>
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<string, Exception?, TResult> onFailure,
        Func<string, TResult>? onWarning = null,
        Func<string, TResult>? onPartialSuccess = null,
        Func<string, TResult>? onCancelled = null,
        Func<string, TResult>? onPending = null,
        Func<string, TResult>? onNoOp = null)
    {
        return base.Match(
            () => onSuccess(Value),
            onFailure,
            onWarning,
            onPartialSuccess,
            onCancelled,
            onPending,
            onNoOp);
    }

    /// <summary>
    ///     Returns the error message associated with the result, or the specified default error message if the result is
    ///     successful.
    /// </summary>
    /// <param name="defaultError">The default error message to return if the result is successful.</param>
    /// <returns>The error message associated with the result, or the specified default error message if successful.</returns>
    public string UnwrapError(string defaultError = "")
    {
        return IsSuccess ? defaultError : ErrorMessage ?? "An unknown error has occurred.";
    }

    /// <inheritdoc />
    public T Unwrap()
    {
        if (Value is not null)
        {
            return Value;
        }

        throw new InvalidOperationException("Cannot unwrap a result that is not a Result<Result>.");
    }

    /// <summary>
    ///     Maps the value of the result to a new value using the specified mapper function.
    /// </summary>
    /// <typeparam name="TOut">The type of the value returned by the mapper function.</typeparam>
    /// <param name="mapper">The function to apply to the value of the result.</param>
    /// <returns>
    ///     A new result containing the value returned by the mapper function, or a failure result if the original result
    ///     was not successful.
    /// </returns>
    public Result<TOut> Map<TOut>(Func<T, TOut> mapper)
    {
        return IsSuccess ? Result<TOut>.Success(mapper(Value)) : Result<TOut>.Failure(ErrorMessage, Exception);
    }

    private static Result SafeExecute(Func<Result> action)
    {
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            Logger?.Error(ex, "Error executing action.");
            return Failure(ex.Message);
        }
    }

    private static Result<TOut> SafeExecute<TOut>(Func<Result<TOut>> func)
    {
        try
        {
            return func();
        }
        catch (Exception ex)
        {
            Logger?.Error(ex, "Error executing function.");
            return Result<TOut>.Failure(ex.Message);
        }
    }

    public static implicit operator Result<T>(T value)
    {
        return Success(value);
    }

    public static implicit operator Result<T>(Exception exception)
    {
        return Failure(exception);
    }

    /// <summary>
    ///     Creates a successful result with the specified value.
    /// </summary>
    /// <param name="value">The value of the result.</param>
    /// <returns>A new <see cref="Result{T}" /> representing success.</returns>
    public static Result<T> Success(T value)
    {
        return new Result<T>(value, string.Empty, null, ResultState.Success);
    }

    /// <summary>
    ///     Creates a failure result with the specified error message and exception.
    /// </summary>
    /// <param name="error">The error message describing the failure.</param>
    /// <param name="exception">The exception associated with the failure, if any.</param>
    /// <returns>A new <see cref="Result{T}" /> representing failure.</returns>
    public new static Result<T> Failure(string error, Exception? exception = null)
    {
        return new Result<T>(default!, error, exception, ResultState.Failure);
    }

    /// <summary>
    ///     Creates a failure result with the specified exception.
    /// </summary>
    /// <param name="exception">The exception associated with the failure.</param>
    /// <returns>A new <see cref="Result{T}" /> representing failure.</returns>
    public static Result<T> Failure(Exception exception)
    {
        return new Result<T>(default!, exception.Message, exception, ResultState.Failure);
    }

    /// <summary>
    ///     Creates a failure result with multiple exceptions.
    /// </summary>
    /// <param name="exceptions">The collection of exceptions that occurred.</param>
    /// <returns>A new <see cref="Result{T}" /> representing failure with multiple exceptions.</returns>
    public new static Result<T> Failure(IEnumerable<Exception> exceptions)
    {
        var exceptionList = exceptions.ToList();
        var errorMessage = exceptionList.Count > 0 ? exceptionList[0].Message : "Multiple errors occurred.";
        return new Result<T>(default!, errorMessage, exceptionList.FirstOrDefault(), ResultState.Failure)
        {
            Exceptions = new ReadOnlyCollection<Exception>(exceptionList)
        };
    }

    /// <summary>
    ///     Tries to execute the specified function and returns a success result with the function's return value if
    ///     successful; otherwise, returns a failure result.
    /// </summary>
    /// <param name="func">The function to execute.</param>
    /// <returns>A success result with the function's return value if successful; otherwise, a failure result.</returns>
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
}
