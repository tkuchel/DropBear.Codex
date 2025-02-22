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
    private static readonly ConcurrentDictionary<Type, DefaultObjectPool<Result<T, TError>>> ResultPool = new();

    private readonly Lazy<T> _lazyValue;

    private sealed class ResultPooledObjectPolicy : IPooledObjectPolicy<Result<T, TError>>
    {
        public Result<T, TError> Create()
        {
            return new Result<T, TError>(new Lazy<T>(() => default!), ResultState.Success);
        }

        public bool Return(Result<T, TError> obj)
        {
            obj.Initialize(ResultState.Success);
            return true;
        }
    }

    #region Properties

    /// <summary>
    ///     Gets the value if the result is successful.
    /// </summary>
    public T? Value => IsSuccess ? _lazyValue.Value : default;

    #endregion

    #region Operators

    public static implicit operator Result<T, TError>(T value)
    {
        return Success(value);
    }

    #endregion

    #region Private Methods

    private static DefaultObjectPool<Result<T, TError>> GetOrCreatePool(Type type)
    {
        return ResultPool.GetOrAdd(type, _ =>
            new DefaultObjectPool<Result<T, TError>>(new ResultPooledObjectPolicy()));
    }

    #endregion

    #region Constructors

    protected Result(
        Lazy<T> lazyValue,
        ResultState state,
        TError? error = null,
        Exception? exception = null)
        : base(state, error, exception)
    {
        _lazyValue = lazyValue;
    }


    protected Result(T value, ResultState state, TError? error = null, Exception? exception = null)
        : this(new Lazy<T>(() => value), state, error, exception)
    {
    }

    protected Result(Func<T> valueFactory, ResultState state, TError? error = null, Exception? exception = null)
        : this(new Lazy<T>(valueFactory), state, error, exception)
    {
    }

    #endregion

    #region Public Methods

    /// <summary>
    ///     Returns the Value if successful, otherwise defaultValue.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T ValueOrDefault(T defaultValue = default!)
    {
        return IsSuccess ? Value! : defaultValue;
    }

    /// <summary>
    ///     Returns the Value if successful, otherwise throws.
    /// </summary>
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
    ///     Asynchronously transforms this result into another result.
    /// </summary>
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> PartialSuccess(T value, TError error)
    {
        var pool = GetOrCreatePool(typeof(Result<T, TError>));
        var result = pool.Get();
        result.Initialize(value, ResultState.PartialSuccess, error);
        return result;
    }

    #endregion

    #region Protected Methods

    protected override void Initialize(ResultState state, TError? error = null, Exception? exception = null)
    {
        base.Initialize(state, error, exception);
        var field = GetType().GetField("_lazyValue", BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(this, new Lazy<T>(() => default!));
    }

    protected void Initialize(T value, ResultState state, TError? error = null, Exception? exception = null)
    {
        base.Initialize(state, error, exception);
        var field = GetType().GetField("_lazyValue", BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(this, new Lazy<T>(() => value));
    }

    protected void Initialize(Func<T> valueFactory, ResultState state, TError? error = null, Exception? exception = null)
    {
        base.Initialize(state, error, exception);
        var field = GetType().GetField("_lazyValue", BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(this, new Lazy<T>(valueFactory));
    }

    #endregion

    #region Equality Members

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
