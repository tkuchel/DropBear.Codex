#region

using System.Collections.Concurrent;
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
///     A concrete result type that can contain both a value and an error.
/// </summary>
/// <typeparam name="T">The type of the successful value.</typeparam>
/// <typeparam name="TError">A type inheriting from ResultError.</typeparam>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[JsonConverter(typeof(ResultTTErrorJsonConverter<,>))]
public class Result<T, TError> : Result<TError>, IResult<T, TError>
    where TError : ResultError
{
    // Global singleton pool provider for consistent pool behavior
    private static readonly ObjectPoolProvider PoolProvider = new DefaultObjectPoolProvider
    {
        MaximumRetained = 1024 // Adjust based on application profile
    };

    private static readonly ConcurrentDictionary<Type, ObjectPool<Result<T, TError>>> ResultPool = new();

    private readonly Lazy<T> _lazyValue;

    #region Properties

    /// <summary>
    ///     Gets the value if the result is successful.
    /// </summary>
    public T? Value => IsSuccess ? _lazyValue.Value : default;

    #endregion

    #region Operators

    /// <summary>
    ///     Implicitly converts a value to a success result containing that value.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator Result<T, TError>(T value)
    {
        return Success(value);
    }

    #endregion

    #region Private Methods

    /// <summary>
    ///     Gets or creates an object pool for the specified result type.
    /// </summary>
    /// <param name="type">The type of result to pool.</param>
    /// <returns>An object pool for the result type.</returns>
    private static ObjectPool<Result<T, TError>> GetOrCreatePool(Type type)
    {
        return ResultPool.GetOrAdd(type, _ =>
            PoolProvider.Create(new ResultPooledObjectPolicy()));
    }

    #endregion

    /// <summary>
    ///     Custom pooling policy for Result objects.
    /// </summary>
    private sealed class ResultPooledObjectPolicy : IPooledObjectPolicy<Result<T, TError>>
    {
        public Result<T, TError> Create()
        {
            return new Result<T, TError>(new Lazy<T>(() => default!, LazyThreadSafetyMode.PublicationOnly),
                ResultState.Success);
        }

        public bool Return(Result<T, TError> obj)
        {
            obj.Initialize(ResultState.Success);
            return true;
        }
    }

    #region Constructors

    /// <summary>
    ///     Initializes a new instance of the <see cref="Result{T, TError}" /> class with a lazy value.
    /// </summary>
    /// <param name="lazyValue">The lazy value to store.</param>
    /// <param name="state">The state of the result.</param>
    /// <param name="error">Optional error if the result is not successful.</param>
    /// <param name="exception">Optional exception if an error occurred.</param>
    protected Result(
        Lazy<T> lazyValue,
        ResultState state,
        TError? error = null,
        Exception? exception = null)
        : base(state, error, exception)
    {
        _lazyValue = lazyValue;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Result{T, TError}" /> class with a value.
    /// </summary>
    /// <param name="value">The value to store.</param>
    /// <param name="state">The state of the result.</param>
    /// <param name="error">Optional error if the result is not successful.</param>
    /// <param name="exception">Optional exception if an error occurred.</param>
    protected Result(T value, ResultState state, TError? error = null, Exception? exception = null)
        : this(new Lazy<T>(() => value, LazyThreadSafetyMode.PublicationOnly), state, error, exception)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Result{T, TError}" /> class with a factory function.
    /// </summary>
    /// <param name="valueFactory">A function that produces the value.</param>
    /// <param name="state">The state of the result.</param>
    /// <param name="error">Optional error if the result is not successful.</param>
    /// <param name="exception">Optional exception if an error occurred.</param>
    protected Result(Func<T> valueFactory, ResultState state, TError? error = null, Exception? exception = null)
        : this(new Lazy<T>(valueFactory, LazyThreadSafetyMode.PublicationOnly), state, error, exception)
    {
    }

    #endregion

    #region Public Methods

    /// <summary>
    ///     Returns the Value if successful, otherwise defaultValue.
    /// </summary>
    /// <param name="defaultValue">The default value to return if the result is not successful.</param>
    /// <returns>Either the result's value or the default value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T ValueOrDefault(T defaultValue = default!)
    {
        return IsSuccess ? Value! : defaultValue;
    }

    /// <summary>
    ///     Returns the Value if successful, otherwise throws.
    /// </summary>
    /// <param name="errorMessage">Optional error message to include in the exception.</param>
    /// <returns>The result's value.</returns>
    /// <exception cref="ResultException">Thrown if the result is not successful.</exception>
    public T ValueOrThrow(string? errorMessage = null)
    {
        if (IsSuccess)
        {
            return Value!;
        }

        throw new ResultException(errorMessage ?? Error?.Message ?? "Operation failed");
    }

    /// <summary>
    ///     Transforms the success value if successful.
    /// </summary>
    /// <typeparam name="TNew">The type of the transformed value.</typeparam>
    /// <param name="mapper">A function that transforms the value.</param>
    /// <returns>A new result containing the transformed value.</returns>
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
            Telemetry.TrackException(ex, State, GetType());
            return Result<TNew, TError>.Failure(Error!, ex);
        }
    }

    /// <summary>
    ///     Asynchronously transforms the success value if successful.
    /// </summary>
    /// <typeparam name="TNew">The type of the transformed value.</typeparam>
    /// <param name="mapper">A function that asynchronously transforms the value.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task representing the asynchronous operation that returns a new result.</returns>
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
            Telemetry.TrackException(ex, State, GetType());
            return Result<TNew, TError>.Failure(Error!, ex);
        }
    }

    /// <summary>
    ///     Transforms this result into another result.
    /// </summary>
    /// <typeparam name="TNew">The type of the new result's value.</typeparam>
    /// <param name="binder">A function that transforms this result into another result.</param>
    /// <returns>The transformed result.</returns>
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
            Telemetry.TrackException(ex, State, GetType());
            return Result<TNew, TError>.Failure(Error!, ex);
        }
    }

    /// <summary>
    ///     Performs pattern matching on the current state of this <see cref="Result{T, TError}" />,
    ///     invoking the appropriate callback function.
    /// </summary>
    /// <typeparam name="TResult">
    ///     The return type for each of the match functions.
    /// </typeparam>
    /// <param name="onSuccess">
    ///     A function to call if the result is <see cref="ResultState.Success" />.
    ///     Receives the successful <typeparamref name="T" /> value.
    /// </param>
    /// <param name="onFailure">
    ///     A function to call if the result is <see cref="ResultState.Failure" />.
    ///     Receives the <typeparamref name="TError" /> and the associated <see cref="Exception" /> (if any).
    /// </param>
    /// <param name="onWarning">
    ///     An optional function to call if the result is <see cref="ResultState.Warning" />.
    ///     Receives the partial <typeparamref name="T" /> value and the <typeparamref name="TError" />.
    /// </param>
    /// <param name="onPartialSuccess">
    ///     An optional function to call if the result is <see cref="ResultState.PartialSuccess" />.
    ///     Receives the partial <typeparamref name="T" /> value and the <typeparamref name="TError" />.
    /// </param>
    /// <param name="onCancelled">
    ///     An optional function to call if the result is <see cref="ResultState.Cancelled" />.
    ///     Receives the partial <typeparamref name="T" /> value (if any) and the <typeparamref name="TError" />.
    /// </param>
    /// <param name="onPending">
    ///     An optional function to call if the result is <see cref="ResultState.Pending" />.
    ///     Receives the partial <typeparamref name="T" /> value (if any) and the <typeparamref name="TError" />.
    /// </param>
    /// <param name="onNoOp">
    ///     An optional function to call if the result is <see cref="ResultState.NoOp" />.
    ///     Receives the partial <typeparamref name="T" /> value (if any) and the <typeparamref name="TError" />.
    /// </param>
    /// <returns>
    ///     The value returned by whichever callback was invoked.
    /// </returns>
    /// <remarks>
    ///     If a given state callback (e.g., <paramref name="onWarning" />) is not provided,
    ///     and this <see cref="ResultState" /> matches that state, the method will fall back
    ///     to using <paramref name="onFailure" />.
    /// </remarks>
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<TError, Exception?, TResult> onFailure,
        Func<T, TError, TResult>? onWarning = null,
        Func<T, TError, TResult>? onPartialSuccess = null,
        Func<T, TError, TResult>? onCancelled = null,
        Func<T, TError, TResult>? onPending = null,
        Func<T, TError, TResult>? onNoOp = null)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            // Helper to simplify falling back to onFailure if a specialized callback is null
            TResult InvokeOrDefault(
                Func<T, TError, TResult>? handler,
                TError theError,
                Exception? ex)
            {
                return handler is not null
                    ? handler(Value!, theError)
                    : onFailure(theError, ex);
            }

            // Switch over the ResultState
            return State switch
            {
                ResultState.Success => onSuccess(Value!),
                ResultState.Failure => onFailure(Error!, Exception),
                ResultState.Warning => InvokeOrDefault(onWarning, Error!, Exception),
                ResultState.PartialSuccess => InvokeOrDefault(onPartialSuccess, Error!, Exception),
                ResultState.Cancelled => InvokeOrDefault(onCancelled, Error!, Exception),
                ResultState.Pending => InvokeOrDefault(onPending, Error!, Exception),
                ResultState.NoOp => InvokeOrDefault(onNoOp, Error!, Exception),
                _ => throw new ResultException($"Unhandled state: {State}")
            };
        }
        catch (Exception ex)
        {
            // Log the unexpected exception during the callback
            Logger.Error(ex, "Exception during Match operation");
            Telemetry.TrackException(ex, State, GetType());

            // If we have a known error, use it; otherwise, create a default one
            return onFailure(Error ?? CreateDefaultError(), ex);
        }
    }


    /// <summary>
    ///     Asynchronously transforms this result into another result.
    /// </summary>
    /// <typeparam name="TNew">The type of the new result's value.</typeparam>
    /// <param name="binder">A function that asynchronously transforms this result into another result.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task representing the asynchronous operation that returns the transformed result.</returns>
    public async ValueTask<Result<TNew, TError>> BindAsync<TNew>(
        Func<T, ValueTask<Result<TNew, TError>>> binder,
        CancellationToken cancellationToken = default)
    {
        if (!IsSuccess)
        {
            return Result<TNew, TError>.Failure(Error!);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await binder(Value!).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.Error(ex, "Exception during async bind operation");
            Telemetry.TrackException(ex, State, GetType());
            return Result<TNew, TError>.Failure(Error!, ex);
        }
    }

    /// <summary>
    ///     Maps the current error to a new error type.
    /// </summary>
    /// <typeparam name="TNewError">The type of the new error.</typeparam>
    /// <param name="errorMapper">A function that transforms the error.</param>
    /// <returns>A new result with the transformed error.</returns>
    public Result<T, TNewError> MapError<TNewError>(Func<TError, TNewError> errorMapper)
        where TNewError : ResultError
    {
        return IsSuccess
            ? Result<T, TNewError>.Success(Value!)
            : Result<T, TNewError>.Failure(errorMapper(Error!));
    }

    /// <summary>
    ///     Ensures a specified condition is met on the current Value.
    /// </summary>
    /// <param name="predicate">A function that returns true if the condition is met.</param>
    /// <param name="error">The error to return if the condition is not met.</param>
    /// <returns>Either this result (if successful and the condition is met) or a failure result.</returns>
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
            Telemetry.TrackException(ex, State, GetType());
            return Failure(error, ex);
        }
    }

    #endregion

    #region Factory Methods

    /// <summary>
    ///     Creates a Success Result containing value.
    /// </summary>
    /// <param name="value">The success value.</param>
    /// <returns>A successful result containing the value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> Success(T value)
    {
        var pool = GetOrCreatePool(typeof(Result<T, TError>));
        var result = pool.Get();
        result.Initialize(value, ResultState.Success);
        return result;
    }

    /// <summary>
    ///     Creates a Success Result that defers value factory execution.
    /// </summary>
    /// <param name="valueFactory">A function that produces the value when needed.</param>
    /// <returns>A successful result that will lazily evaluate the factory function.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> LazySuccess(Func<T> valueFactory)
    {
        var pool = GetOrCreatePool(typeof(Result<T, TError>));
        var result = pool.Get();
        result.Initialize(valueFactory, ResultState.Success);
        return result;
    }

    /// <summary>
    ///     Creates a Failure Result.
    /// </summary>
    /// <param name="error">The error that caused the failure.</param>
    /// <param name="exception">Optional exception associated with the failure.</param>
    /// <returns>A failed result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new static Result<T, TError> Failure(TError error, Exception? exception = null)
    {
        var pool = GetOrCreatePool(typeof(Result<T, TError>));
        var result = pool.Get();
        result.Initialize(default(T)!, ResultState.Failure, error, exception);
        return result;
    }

    /// <summary>
    ///     Creates a PartialSuccess Result.
    /// </summary>
    /// <param name="value">The partial success value.</param>
    /// <param name="error">Information about the partial success condition.</param>
    /// <returns>A result in the partial success state.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> PartialSuccess(T value, TError error)
    {
        var pool = GetOrCreatePool(typeof(Result<T, TError>));
        var result = pool.Get();
        result.Initialize(value, ResultState.PartialSuccess, error);
        return result;
    }

    /// <summary>
    ///     Creates a Warning Result.
    /// </summary>
    /// <param name="value">The success value with warning.</param>
    /// <param name="error">The warning information.</param>
    /// <returns>A result in the warning state.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> Warning(T value, TError error)
    {
        var pool = GetOrCreatePool(typeof(Result<T, TError>));
        var result = pool.Get();
        result.Initialize(value, ResultState.Warning, error);
        return result;
    }

    /// <summary>
    ///     Creates a Cancelled Result.
    /// </summary>
    /// <param name="error">Information about the cancellation.</param>
    /// <returns>A result in the cancelled state.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new static Result<T, TError> Cancelled(TError error)
    {
        var pool = GetOrCreatePool(typeof(Result<T, TError>));
        var result = pool.Get();
        result.Initialize(default(T)!, ResultState.Cancelled, error);
        return result;
    }

    /// <summary>
    ///     Creates a Cancelled Result with a partial value.
    /// </summary>
    /// <param name="value">The partial value that was captured before cancellation.</param>
    /// <param name="error">Information about the cancellation.</param>
    /// <returns>A result in the cancelled state with a partial value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> Cancelled(T value, TError error)
    {
        var pool = GetOrCreatePool(typeof(Result<T, TError>));
        var result = pool.Get();
        result.Initialize(value, ResultState.Cancelled, error);
        return result;
    }

    /// <summary>
    ///     Creates a Pending Result.
    /// </summary>
    /// <param name="error">Information about the pending operation.</param>
    /// <returns>A result in the pending state.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new static Result<T, TError> Pending(TError error)
    {
        var pool = GetOrCreatePool(typeof(Result<T, TError>));
        var result = pool.Get();
        result.Initialize(default(T)!, ResultState.Pending, error);
        return result;
    }

    /// <summary>
    ///     Creates a Pending Result with an interim value.
    /// </summary>
    /// <param name="value">An interim or partial value available while the operation is pending.</param>
    /// <param name="error">Information about the pending operation.</param>
    /// <returns>A result in the pending state with an interim value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> Pending(T value, TError error)
    {
        var pool = GetOrCreatePool(typeof(Result<T, TError>));
        var result = pool.Get();
        result.Initialize(value, ResultState.Pending, error);
        return result;
    }

    /// <summary>
    ///     Creates a NoOp (No Operation) Result.
    /// </summary>
    /// <param name="error">Information about why no operation was performed.</param>
    /// <returns>A result in the NoOp state.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new static Result<T, TError> NoOp(TError error)
    {
        var pool = GetOrCreatePool(typeof(Result<T, TError>));
        var result = pool.Get();
        result.Initialize(default(T)!, ResultState.NoOp, error);
        return result;
    }

    /// <summary>
    ///     Creates a NoOp (No Operation) Result with a context value.
    /// </summary>
    /// <param name="value">A context value related to the no-op condition.</param>
    /// <param name="error">Information about why no operation was performed.</param>
    /// <returns>A result in the NoOp state with a context value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> NoOp(T value, TError error)
    {
        var pool = GetOrCreatePool(typeof(Result<T, TError>));
        var result = pool.Get();
        result.Initialize(value, ResultState.NoOp, error);
        return result;
    }

    #endregion

    #region Protected Methods

    /// <summary>
    ///     Initializes or reinitializes the result instance.
    /// </summary>
    /// <param name="state">The state of the result.</param>
    /// <param name="error">Optional error if the result is not successful.</param>
    /// <param name="exception">Optional exception if an error occurred.</param>
    protected override void Initialize(ResultState state, TError? error = null, Exception? exception = null)
    {
        base.Initialize(state, error, exception);
        var field = GetType().GetField("_lazyValue", BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(this, new Lazy<T>(() => default!, LazyThreadSafetyMode.PublicationOnly));
    }

    /// <summary>
    ///     Initializes or reinitializes the result instance with a value.
    /// </summary>
    /// <param name="value">The value to store.</param>
    /// <param name="state">The state of the result.</param>
    /// <param name="error">Optional error if the result is not successful.</param>
    /// <param name="exception">Optional exception if an error occurred.</param>
    protected void Initialize(T value, ResultState state, TError? error = null, Exception? exception = null)
    {
        base.Initialize(state, error, exception);
        var field = GetType().GetField("_lazyValue", BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(this, new Lazy<T>(() => value, LazyThreadSafetyMode.PublicationOnly));
    }

    /// <summary>
    ///     Initializes or reinitializes the result instance with a factory function.
    /// </summary>
    /// <param name="valueFactory">A function that produces the value.</param>
    /// <param name="state">The state of the result.</param>
    /// <param name="error">Optional error if the result is not successful.</param>
    /// <param name="exception">Optional exception if an error occurred.</param>
    protected void Initialize(Func<T> valueFactory, ResultState state, TError? error = null,
        Exception? exception = null)
    {
        base.Initialize(state, error, exception);
        var field = GetType().GetField("_lazyValue", BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(this, new Lazy<T>(valueFactory, LazyThreadSafetyMode.PublicationOnly));
    }

    #endregion

    #region Equality Members

    /// <summary>
    ///     Determines whether this result is equal to another object.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True if the objects are equal.</returns>
    public override bool Equals(object? obj)
    {
        if (!base.Equals(obj))
        {
            return false;
        }

        var other = (Result<T, TError>)obj;
        return !IsSuccess || !other.IsSuccess ||
               EqualityComparer<T>.Default.Equals(Value, other.Value);
    }

    /// <summary>
    ///     Gets a hash code for this result.
    /// </summary>
    /// <returns>A hash code value.</returns>
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

    #region Debugging Support

    private string DebuggerDisplay =>
        $"State = {State}, Value = {Value?.ToString() ?? "null"}, Error = {Error?.Message ?? "None"}";

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Dictionary<string, object?> DebugView =>
        new(StringComparer.Ordinal)
        {
            { "State", State },
            { "IsSuccess", IsSuccess },
            { "Value", Value },
            { "Error", Error?.Message },
            { "Exception", Exception?.Message },
            { "ExceptionCount", Exceptions.Count }
        };

    #endregion
}
