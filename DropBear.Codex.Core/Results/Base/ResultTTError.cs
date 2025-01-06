#region

using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;

#endregion

namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     A concrete result type that can contain both a <typeparamref name="T" /> value (if successful)
///     and an error of type <typeparamref name="TError" />.
///     Implements <see cref="IResult{T,TError}" />.
/// </summary>
/// <typeparam name="T">The type of the successful value.</typeparam>
/// <typeparam name="TError">A type inheriting from <see cref="ResultError" /> representing the error.</typeparam>
public class Result<T, TError> : Result<TError>, IResult<T, TError>
    where TError : ResultError
{
    private readonly Lazy<T> _lazyValue;

    #region Properties

    /// <summary>
    ///     Gets the value if the result is successful. Otherwise, returns default(<typeparamref name="T" />).
    /// </summary>
    public T? Value => IsSuccess ? _lazyValue.Value : default;

    #endregion

    #region Operators

    /// <summary>
    ///     Implicitly converts a <typeparamref name="T" /> value to a Success <see cref="Result{T, TError}" />.
    /// </summary>
    /// <param name="value">The value to be wrapped in a success result.</param>
    public static implicit operator Result<T, TError>(T value)
    {
        return Success(value);
    }

    #endregion

    #region Constructors

    private Result(
        Lazy<T> lazyValue,
        ResultState state,
        TError? error = null,
        Exception? exception = null)
        : base(state, error, exception)
    {
        _lazyValue = lazyValue;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Result{T, TError}" /> class
    ///     using a concrete <typeparamref name="T" /> value.
    /// </summary>
    /// <param name="value">The value to store (if the result is successful).</param>
    /// <param name="state">The <see cref="ResultState" /> (e.g., Success, Failure, etc.).</param>
    /// <param name="error">The error object if not successful.</param>
    /// <param name="exception">An optional exception for additional context.</param>
    protected Result(T value, ResultState state, TError? error = null, Exception? exception = null)
        : this(new Lazy<T>(() => value), state, error, exception)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Result{T, TError}" /> class
    ///     with a deferred <typeparamref name="T" /> value via a <paramref name="valueFactory" />.
    /// </summary>
    /// <param name="valueFactory">A <see cref="Func{T}" /> that produces the value on-demand.</param>
    /// <param name="state">The <see cref="ResultState" /> (e.g., Success, Failure, etc.).</param>
    /// <param name="error">The error object if not successful.</param>
    /// <param name="exception">An optional exception for additional context.</param>
    protected Result(Func<T> valueFactory, ResultState state, TError? error = null, Exception? exception = null)
        : this(new Lazy<T>(valueFactory), state, error, exception)
    {
    }

    #endregion

    #region Public Methods

    /// <summary>
    ///     Returns the <see cref="Value" /> if successful, otherwise <paramref name="defaultValue" />.
    /// </summary>
    /// <param name="defaultValue">A fallback value for unsuccessful results.</param>
    public T ValueOrDefault(T defaultValue = default!)
    {
        return IsSuccess ? Value! : defaultValue;
    }

    /// <summary>
    ///     Returns the <see cref="Value" /> if successful, otherwise throws an <see cref="InvalidOperationException" />.
    /// </summary>
    /// <param name="errorMessage">
    ///     An optional error message for the exception if the result is not successful.
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown if the result is not successful.</exception>
    public T ValueOrThrow(string? errorMessage = null)
    {
        if (IsSuccess)
        {
            return Value!;
        }

        throw new InvalidOperationException(errorMessage ?? Error?.Message ?? "Operation failed");
    }

    /// <summary>
    ///     Transforms the success value with a given <paramref name="mapper" /> if successful.
    ///     If the result is not successful, returns a new <see cref="Failure" /> result with the same error.
    /// </summary>
    /// <typeparam name="TNew">The type of the mapped value.</typeparam>
    /// <param name="mapper">A function to map the existing value to <typeparamref name="TNew" />.</param>
    public Result<TNew, TError> Map<TNew>(Func<T, TNew> mapper)
    {
        if (!IsSuccess)
        {
            return Result<TNew, TError>.Failure(Error!);
        }

        try
        {
            var mappedValue = mapper(Value!);
            return Result<TNew, TError>.Success(mappedValue);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during map operation");
            return Result<TNew, TError>.Failure(Error!, ex);
        }
    }

    /// <summary>
    ///     Asynchronously transforms the success value with a given <paramref name="mapper" /> if successful.
    ///     If the result is not successful, returns a new <see cref="Failure" /> result with the same error.
    /// </summary>
    /// <typeparam name="TNew">The type of the mapped value.</typeparam>
    /// <param name="mapper">A function to map the existing value to <typeparamref name="TNew" /> asynchronously.</param>
    /// <param name="cancellationToken">A token to cancel the async operation if desired.</param>
    public async ValueTask<Result<TNew, TError>> MapAsync<TNew>(
        Func<T, ValueTask<TNew>> mapper,
        CancellationToken cancellationToken = default)
    {
        if (!IsSuccess)
        {
            return Result<TNew, TError>.Failure(Error!);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await mapper(Value!).ConfigureAwait(false);
            return Result<TNew, TError>.Success(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.Error(ex, "Exception during async map operation");
            return Result<TNew, TError>.Failure(Error!, ex);
        }
    }

    /// <summary>
    ///     Transforms this result into another result via a <paramref name="binder" /> function,
    ///     effectively chaining two operations.
    /// </summary>
    /// <typeparam name="TNew">The type of the new success value.</typeparam>
    /// <param name="binder">A function that takes the current value and returns a new <see cref="Result{TNew, TError}" />.</param>
    public Result<TNew, TError> Bind<TNew>(Func<T, Result<TNew, TError>> binder)
    {
        if (!IsSuccess)
        {
            return Result<TNew, TError>.Failure(Error!);
        }

        try
        {
            return binder(Value!);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during bind operation");
            return Result<TNew, TError>.Failure(Error!, ex);
        }
    }

    /// <summary>
    ///     Asynchronously transforms this result into another result via a <paramref name="binder" /> function.
    /// </summary>
    /// <typeparam name="TNew">The type of the new success value.</typeparam>
    /// <param name="binder">
    ///     An async function that takes the current value and returns a <see cref="Result{TNew, TError}" />.
    /// </param>
    public async ValueTask<Result<TNew, TError>> BindAsync<TNew>(
        Func<T, ValueTask<Result<TNew, TError>>> binder)
    {
        if (!IsSuccess)
        {
            return Result<TNew, TError>.Failure(Error!);
        }

        try
        {
            return await binder(Value!).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during async bind operation");
            return Result<TNew, TError>.Failure(Error!, ex);
        }
    }

    /// <summary>
    ///     Maps the current error <typeparamref name="TError" /> to a new error type <typeparamref name="TNewError" />.
    ///     If the result is successful, this is effectively a no-op on the value.
    /// </summary>
    /// <typeparam name="TNewError">The type of the new error, also inheriting from <see cref="ResultError" />.</typeparam>
    /// <param name="errorMapper">A function that converts <typeparamref name="TError" /> to <typeparamref name="TNewError" />.</param>
    public Result<T, TNewError> MapError<TNewError>(Func<TError, TNewError> errorMapper)
        where TNewError : ResultError
    {
        return IsSuccess
            ? Result<T, TNewError>.Success(Value!)
            : Result<T, TNewError>.Failure(errorMapper(Error!));
    }

    /// <summary>
    ///     Ensures a specified condition is met on the current <see cref="Value" />.
    ///     If the condition fails, returns a <see cref="Failure" /> result.
    /// </summary>
    /// <param name="predicate">A function that returns <c>true</c> if <see cref="Value" /> passes validation.</param>
    /// <param name="error">The <typeparamref name="TError" /> to use if the condition fails.</param>
    /// <returns>This result if already failed or if the condition passes; otherwise a new Failure result.</returns>
    public IResult<T, TError> Ensure(Func<T, bool> predicate, TError error)
    {
        if (!IsSuccess)
        {
            return this;
        }

        try
        {
            return predicate(Value!) ? this : Failure(error);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during ensure operation");
            return Failure(error, ex);
        }
    }

    #endregion

    #region Factory Methods

    /// <summary>
    ///     Creates a Success <see cref="Result{T, TError}" /> containing <paramref name="value" />.
    /// </summary>
    /// <param name="value">The value to wrap in a success result.</param>
    public static Result<T, TError> Success(T value)
    {
        return new Result<T, TError>(value, ResultState.Success);
    }

    /// <summary>
    ///     Creates a Success <see cref="Result{T, TError}" /> that defers <paramref name="valueFactory" /> execution
    ///     until <see cref="Value" /> is accessed.
    /// </summary>
    /// <param name="valueFactory">A function producing the value on demand.</param>
    public static Result<T, TError> LazySuccess(Func<T> valueFactory)
    {
        return new Result<T, TError>(valueFactory, ResultState.Success);
    }

    /// <summary>
    ///     Creates a Failure <see cref="Result{T, TError}" /> with the specified <paramref name="error" />
    ///     and an optional <paramref name="exception" />.
    /// </summary>
    /// <param name="error">The <typeparamref name="TError" /> describing the failure.</param>
    /// <param name="exception">An optional <see cref="Exception" /> for additional context.</param>
    public new static Result<T, TError> Failure(TError error, Exception? exception = null)
    {
        return new Result<T, TError>(
            new Lazy<T>(() => default!),
            ResultState.Failure,
            error,
            exception);
    }

    /// <summary>
    ///     Creates a PartialSuccess <see cref="Result{T, TError}" /> with <paramref name="value" />
    ///     and an <paramref name="error" /> describing the partial success condition.
    /// </summary>
    /// <param name="value">The value indicating partial success.</param>
    /// <param name="error">A <typeparamref name="TError" /> for details on partial failure.</param>
    public static Result<T, TError> PartialSuccess(T value, TError error)
    {
        return new Result<T, TError>(value, ResultState.PartialSuccess, error);
    }

    #endregion

    #region Equality Members

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

        if (!base.Equals(obj))
        {
            return false;
        }

        var other = (Result<T, TError>)obj;

        // If both are failures, base.Equals already checked error equality.
        // If at least one is success, compare the Value.
        return !IsSuccess || !other.IsSuccess ||
               EqualityComparer<T>.Default.Equals(Value, other.Value);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = base.GetHashCode();
            if (IsSuccess)
            {
                hashCode = HashCode.Combine(hashCode, EqualityComparer<T>.Default.GetHashCode(Value!));
            }

            return hashCode;
        }
    }

    #endregion
}
