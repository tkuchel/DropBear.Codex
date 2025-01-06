#region

using System.Collections;
using System.Collections.ObjectModel;
using DropBear.Codex.Core.Enums;

#endregion

namespace DropBear.Codex.Core.Results.Compatibility;

/// <summary>
///     A backwards-compatible <c>Result&lt;T&gt;</c> class that can hold a success value of type <typeparamref name="T" />
///     or an error message. Supports enumeration (yielding the value if successful).
/// </summary>
/// <typeparam name="T">The success value type.</typeparam>
public class Result<T> : Result, IEnumerable<T>, IEquatable<Result<T>>
{
    private readonly T _value;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Result{T}" /> class.
    /// </summary>
    /// <param name="value">The success value (if <paramref name="state" /> is a success-like state).</param>
    /// <param name="error">An optional error message if <paramref name="state" /> indicates a failure.</param>
    /// <param name="exception">An optional exception providing additional context.</param>
    /// <param name="state">The <see cref="ResultState" /> (e.g. Success, Failure, etc.).</param>
    protected Result(T value, string? error, Exception? exception, ResultState state)
        : base(state, error, exception)
    {
        _value = value;
    }

    /// <summary>
    ///     Gets the success value if <see cref="Result.IsSuccess" /> is true.
    ///     Otherwise, throws <see cref="InvalidOperationException" />.
    /// </summary>
    /// <exception cref="InvalidOperationException">If <see cref="Result.IsSuccess" /> is false.</exception>
    public T Value =>
        IsSuccess ? _value : throw new InvalidOperationException("Cannot access Value on a failed result.");

    #region Enumeration

    /// <summary>
    ///     Enumerates the success value if <see cref="Result.IsSuccess" /> is true; yields nothing otherwise.
    /// </summary>
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

    #endregion

    #region Equality

    /// <summary>
    ///     Indicates whether this result is equal to another <see cref="Result{T}" /> based on state, error, and value.
    /// </summary>
    public bool Equals(Result<T>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        // Compare base result equality
        if (!base.Equals(other))
        {
            return false;
        }

        // Only compare values if both results are successful
        if (IsSuccess && other.IsSuccess)
        {
            return EqualityComparer<T>.Default.Equals(Value, other.Value);
        }

        // If both are failures, base.Equals covers error equality
        return !IsSuccess && !other.IsSuccess;
    }

    /// <summary>
    ///     Executes the specified action if this result is a failure.
    /// </summary>
    public new Result<T> OnFailure(Action<string, Exception?> action)
    {
        base.OnFailure(action);
        return this;
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

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals(obj as Result<T>);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        // Include base hash code and Value hash code, but only if successful
        if (IsSuccess)
        {
            return HashCode.Combine(base.GetHashCode(), Value);
        }

        return base.GetHashCode();
    }

    #endregion

    #region Functional Methods

    /// <summary>
    ///     Maps the success value with <paramref name="mapper" />, returning a new <see cref="Result{TResult}" />
    ///     in the success state. If this result is not successful, returns a new failure result with the same error.
    /// </summary>
    public Result<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        return IsSuccess
            ? Result<TResult>.Success(mapper(Value))
            : Result<TResult>.Failure(ErrorMessage, Exception);
    }

    /// <summary>
    ///     Binds the success value to a new operation, returning its result.
    ///     If this result is not successful, propagates the failure.
    /// </summary>
    public Result<TResult> Bind<TResult>(Func<T, Result<TResult>> func)
    {
        return IsSuccess
            ? func(Value)
            : Result<TResult>.Failure(ErrorMessage, Exception);
    }

    /// <summary>
    ///     Ensures the current value satisfies <paramref name="predicate" />; if it does not, returns a failure.
    /// </summary>
    public Result<T> Ensure(Func<T, bool> predicate, string errorMessage)
    {
        if (!IsSuccess)
        {
            return this;
        }

        return predicate(Value) ? this : Failure(errorMessage);
    }

    /// <summary>
    ///     Returns the success value if successful, otherwise returns <paramref name="defaultValue" />.
    /// </summary>
    public T ValueOrDefault(T defaultValue = default!)
    {
        return IsSuccess ? Value : defaultValue;
    }

    /// <summary>
    ///     Returns the success value if successful, otherwise throws <see cref="InvalidOperationException" />.
    /// </summary>
    /// <param name="errorMessage">Optional error message for the thrown exception.</param>
    public T ValueOrThrow(string? errorMessage = null)
    {
        if (IsSuccess)
        {
            return Value;
        }

        throw new InvalidOperationException(errorMessage ?? ErrorMessage);
    }

    #endregion

    #region Static Factory Methods

    /// <summary>
    ///     Creates a new success <see cref="Result{T}" /> with the specified <paramref name="value" />.
    /// </summary>
    public static Result<T> Success(T value)
    {
        return new Result<T>(value, null, null, ResultState.Success);
    }

    /// <summary>
    ///     Creates a new failure <see cref="Result{T}" /> with the specified error message
    ///     and optional <paramref name="exception" />.
    /// </summary>
    public new static Result<T> Failure(string error, Exception? exception = null)
    {
        return new Result<T>(default!, error, exception, ResultState.Failure);
    }

    /// <summary>
    ///     Creates a new failure <see cref="Result{T}" /> from the specified <paramref name="exception" />.
    /// </summary>
    public static Result<T> Failure(Exception exception)
    {
        return new Result<T>(default!, exception.Message, exception, ResultState.Failure);
    }

    /// <summary>
    ///     Creates a new failure <see cref="Result{T}" /> from multiple exceptions.
    /// </summary>
    public new static Result<T> Failure(IEnumerable<Exception> exceptions)
    {
        var exceptionList = exceptions.ToList();
        var errorMessage = exceptionList.Count > 0
            ? exceptionList[0].Message
            : "Multiple errors occurred.";
        return new Result<T>(default!, errorMessage, exceptionList.FirstOrDefault(), ResultState.Failure)
        {
            Exceptions = new ReadOnlyCollection<Exception>(exceptionList)
        };
    }

    /// <summary>
    ///     Creates a partial success <see cref="Result{T}" /> with the specified <paramref name="value" /> and
    ///     <paramref name="error" />.
    /// </summary>
    public static Result<T> PartialSuccess(T value, string error)
    {
        return new Result<T>(value, error, null, ResultState.PartialSuccess);
    }

    /// <summary>
    ///     Invokes <paramref name="func" />, returning a success if it completes or a failure if it throws an exception.
    /// </summary>
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

    /// <summary>
    ///     Implicitly converts a <typeparamref name="T" /> to a success result containing that value.
    /// </summary>
    public static implicit operator Result<T>(T value)
    {
        return Success(value);
    }

    /// <summary>
    ///     Implicitly converts an <see cref="Exception" /> to a failure result containing that exception.
    /// </summary>
    public static implicit operator Result<T>(Exception exception)
    {
        return Failure(exception);
    }

    #endregion
}
