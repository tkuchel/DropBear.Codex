#region

using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Common;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Compatibility;
using DropBear.Codex.Core.Results.Errors;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     A concrete result type that can contain both a value and an error.
///     Refactored to reduce constructor overloads, preventing ambiguity.
/// </summary>
/// <typeparam name="T">The type of the successful value.</typeparam>
/// <typeparam name="TError">A type inheriting from <see cref="ResultError" />.</typeparam>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[JsonConverter(typeof(ResultTTErrorJsonConverter<,>))]
public class Result<T, TError> : Result<TError>, IResult<T, TError>
    where TError : ResultError
{
    #region Object Pool

    // We supply a single, unambiguous constructor call here, naming the parameters for clarity.
    private static readonly ObjectPool<Result<T, TError>> ResultPool =
        ObjectPoolManager.GetPool<Result<T, TError>>(
            () => new Result<T, TError>(
                default!,
                ResultState.Success,
                null,
                null
            )
        );

    #endregion

    // We store the success value in a lazy so it won't be created if not needed.
    private readonly Lazy<T> _lazyValue;

    #region Constructors

    /// <summary>
    ///     Initializes a new instance of <see cref="Result{T, TError}" /> with a single, unambiguous constructor,
    ///     taking the "value" you wish to store.
    /// </summary>
    /// <param name="initialValue">The actual value to store if the result is in a success state.</param>
    /// <param name="state">The <see cref="ResultState" /> (Success, Failure, etc.).</param>
    /// <param name="error">The error if not successful, else null.</param>
    /// <param name="exception">Optional exception if the result represents an error state caused by an exception.</param>
    protected Result(
        T initialValue,
        ResultState state,
        TError? error = null,
        Exception? exception = null)
        : base(state, error, exception)
    {
        // By default, we wrap the initial value in a lazy container, so if the result is not actually success,
        // we won't force creation of that value.
        _lazyValue = new Lazy<T>(() => initialValue, LazyThreadSafetyMode.PublicationOnly);
    }

    #endregion

    #region Operators

    /// <summary>
    ///     Implicitly converts a value of type <typeparamref name="T" /> into a success <see cref="Result{T,TError}" />.
    /// </summary>
    public static implicit operator Result<T, TError>(T value)
    {
        return Success(value);
    }

    #endregion

    #region Internal Implementation

    /// <summary>
    ///     A renamed, unambiguous initialization method for the "value" scenario.
    ///     We do not have an overload for "factory" or "lazy" here, so no confusion arises.
    /// </summary>
    protected void InitializeFromValue(T value, ResultState state, TError? error = null, Exception? exception = null)
    {
        // Reuse the base class method to reset state & error
        InitializeInternal(state, error, exception);

        // Now forcibly reassign our _lazyValue so it knows the new 'value'
        var lazyField = GetType().GetField("_lazyValue", BindingFlags.NonPublic | BindingFlags.Instance);
        if (lazyField != null)
        {
            lazyField.SetValue(
                this,
                new Lazy<T>(() => value, LazyThreadSafetyMode.PublicationOnly)
            );
        }
    }

    #endregion

    #region IResult<T, TError> Implementation

    /// <inheritdoc />
    public T? Value => IsSuccess ? _lazyValue.Value : default;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T ValueOrDefault(T defaultValue = default!)
    {
        return IsSuccess ? Value! : defaultValue;
    }

    /// <inheritdoc />
    public T ValueOrThrow(string? errorMessage = null)
    {
        if (IsSuccess)
        {
            return Value!;
        }

        throw new ResultException(errorMessage ?? Error?.Message ?? "Operation failed");
    }

    /// <inheritdoc />
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
            Logger.Error(ex, "Exception during Ensure operation");
            Telemetry.TrackException(ex, State, GetType());
            return Failure(error, ex);
        }
    }

    #endregion

    #region Factory Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> Success(T value)
    {
        var result = ResultPool.Get();
        // We now call our renamed method: "InitializeFromValue" to avoid ambiguity
        result.InitializeFromValue(value, ResultState.Success);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new static Result<T, TError> Failure(TError error, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        var result = ResultPool.Get();
        // If we supply a "default!" for the value, it's only relevant if the state is success.
        result.InitializeFromValue(default!, ResultState.Failure, error, exception);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> PartialSuccess(T value, TError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var result = ResultPool.Get();
        result.InitializeFromValue(value, ResultState.PartialSuccess, error);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> Warning(T value, TError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var result = ResultPool.Get();
        result.InitializeFromValue(value, ResultState.Warning, error);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new static Result<T, TError> Cancelled(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var result = ResultPool.Get();
        result.InitializeFromValue(default!, ResultState.Cancelled, error);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> Cancelled(T value, TError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var result = ResultPool.Get();
        result.InitializeFromValue(value, ResultState.Cancelled, error);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new static Result<T, TError> Pending(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var result = ResultPool.Get();
        result.InitializeFromValue(default!, ResultState.Pending, error);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> Pending(T value, TError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var result = ResultPool.Get();
        result.InitializeFromValue(value, ResultState.Pending, error);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new static Result<T, TError> NoOp(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var result = ResultPool.Get();
        result.InitializeFromValue(default!, ResultState.NoOp, error);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> NoOp(T value, TError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var result = ResultPool.Get();
        result.InitializeFromValue(value, ResultState.NoOp, error);
        return result;
    }

    #endregion

    #region Transform Methods

    /// <summary>
    ///     Transforms the success value using <paramref name="mapper" /> if this is a success.
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
            Logger.Error(ex, "Exception during Map operation");
            Telemetry.TrackException(ex, State, GetType());
            return Result<TNew, TError>.Failure(Error!, ex);
        }
    }

    /// <summary>
    ///     Transforms this success result into another result via <paramref name="binder" />.
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
            Logger.Error(ex, "Exception during Bind operation");
            Telemetry.TrackException(ex, State, GetType());
            return Result<TNew, TError>.Failure(Error!, ex);
        }
    }

    /// <summary>
    ///     Maps the current error to a new error type using <paramref name="errorMapper" />.
    /// </summary>
    public Result<T, TNewError> MapError<TNewError>(Func<TError, TNewError> errorMapper)
        where TNewError : ResultError
    {
        return IsSuccess
            ? Result<T, TNewError>.Success(Value!)
            : Result<T, TNewError>.Failure(errorMapper(Error!));
    }

    #endregion

    #region Equality / Debugging

    public override bool Equals(object? obj)
    {
        if (!base.Equals(obj))
        {
            return false;
        }

        var other = (Result<T, TError>)obj;
        // If both are success, compare T
        if (IsSuccess && other.IsSuccess)
        {
            return EqualityComparer<T>.Default.Equals(Value, other.Value);
        }

        // If either is not success, they are only "equal" if states, errors, etc. match (already tested by base).
        return true;
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
            { "Exception", Exception?.Message }
        };

    #endregion
}
