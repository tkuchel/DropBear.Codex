#region

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Compatibility;
using DropBear.Codex.Core.Results.Errors;

#endregion

namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     A concrete result type that can contain both a value and an error.
///     Optimized for .NET 9 with direct allocation and modern value handling.
/// </summary>
/// <typeparam name="T">The type of the successful value.</typeparam>
/// <typeparam name="TError">A type inheriting from <see cref="ResultError" />.</typeparam>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[JsonConverter(typeof(ResultTTErrorJsonConverter<,>))]
public class Result<T, TError> : Result<TError>, IResult<T, TError>
    where TError : ResultError
{
    private readonly T? _value;
    private readonly bool _hasValue;

    /// <summary>
    ///     Private constructor for direct instantiation.
    /// </summary>
    protected Result(T? value, ResultState state, TError? error = null, Exception? exception = null)
        : base(state, error, exception)
    {
        _value = value;
        _hasValue = state is ResultState.Success or ResultState.PartialSuccess or ResultState.Warning;
    }

    #region IResult<T, TError> Implementation

    /// <inheritdoc />
    public T? Value => _hasValue ? _value : default;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T ValueOrDefault(T defaultValue = default!)
    {
        return _hasValue ? _value! : defaultValue;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T ValueOrThrow(string? errorMessage = null)
    {
        if (_hasValue)
        {
            return _value!;
        }

        var message = errorMessage ?? Error?.Message ?? "Operation failed";
        throw new ResultException(message);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IResult<T, TError> Ensure(Func<T, bool> predicate, TError error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);

        if (!IsSuccess || !_hasValue)
        {
            return this;
        }

        try
        {
            return predicate(_value!) ? this : Failure(error);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during Ensure operation");
            Telemetry.TrackException(ex, State, GetType());
            return Failure(error, ex);
        }
    }

    #endregion

    #region Transformation Methods

    /// <summary>
    ///     Transforms the success value using the provided mapper if this is a success.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TNew, TError> Map<TNew>(Func<T, TNew> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        if (!IsSuccess || !_hasValue)
        {
            return Result<TNew, TError>.Failure(Error!);
        }

        try
        {
            var mappedValue = mapper(_value!);
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
    ///     Transforms the success value asynchronously using ValueTask for optimal performance.
    /// </summary>
    public async ValueTask<Result<TNew, TError>> MapAsync<TNew>(Func<T, ValueTask<TNew>> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        if (!IsSuccess || !_hasValue)
        {
            return Result<TNew, TError>.Failure(Error!);
        }

        try
        {
            var mappedValue = await mapper(_value!).ConfigureAwait(false);
            return Result<TNew, TError>.Success(mappedValue);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during async Map operation");
            Telemetry.TrackException(ex, State, GetType());
            return Result<TNew, TError>.Failure(Error!, ex);
        }
    }

    /// <summary>
    ///     Transforms this success result into another result via the provided binder.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TNew, TError> Bind<TNew>(Func<T, Result<TNew, TError>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);

        if (!IsSuccess || !_hasValue)
        {
            return Result<TNew, TError>.Failure(Error!);
        }

        try
        {
            return binder(_value!);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during Bind operation");
            Telemetry.TrackException(ex, State, GetType());
            return Result<TNew, TError>.Failure(Error!, ex);
        }
    }

    /// <summary>
    ///     Transforms this success result asynchronously via the provided binder with ValueTask optimization.
    /// </summary>
    public async ValueTask<Result<TNew, TError>> BindAsync<TNew>(Func<T, ValueTask<Result<TNew, TError>>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);

        if (!IsSuccess || !_hasValue)
        {
            return Result<TNew, TError>.Failure(Error!);
        }

        try
        {
            return await binder(_value!).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during async Bind operation");
            Telemetry.TrackException(ex, State, GetType());
            return Result<TNew, TError>.Failure(Error!, ex);
        }
    }

    /// <summary>
    ///     Maps the current error to a new error type using the provided mapper.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<T, TNewError> MapError<TNewError>(Func<TError, TNewError> errorMapper)
        where TNewError : ResultError
    {
        ArgumentNullException.ThrowIfNull(errorMapper);

        return IsSuccess && _hasValue
            ? Result<T, TNewError>.Success(_value!)
            : Result<T, TNewError>.Failure(errorMapper(Error!));
    }

    /// <summary>
    ///     Executes an action with the value if successful, without changing the result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<T, TError> Tap(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (IsSuccess && _hasValue)
        {
            try
            {
                action(_value!);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Exception during Tap operation");
                Telemetry.TrackException(ex, State, GetType());
            }
        }

        return this;
    }

    /// <summary>
    ///     Executes an async action with the value if successful, without changing the result.
    /// </summary>
    public async ValueTask<Result<T, TError>> TapAsync(Func<T, ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (IsSuccess && _hasValue)
        {
            try
            {
                await action(_value!).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Exception during async Tap operation");
                Telemetry.TrackException(ex, State, GetType());
            }
        }

        return this;
    }

    #endregion

    #region Pattern Matching

    /// <summary>
    ///     Performs pattern matching with value access optimization.
    /// </summary>
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<TError, Exception?, TResult> onFailure,
        Func<T, TError, TResult>? onWarning = null,
        Func<T, TError, TResult>? onPartialSuccess = null,
        Func<TError, TResult>? onCancelled = null,
        Func<TError, TResult>? onPending = null,
        Func<TError, TResult>? onNoOp = null)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            return State switch
            {
                ResultState.Success when _hasValue => onSuccess(_value!),
                ResultState.Warning when onWarning != null && _hasValue => onWarning(_value!, Error!),
                ResultState.PartialSuccess when onPartialSuccess != null && _hasValue =>
                    onPartialSuccess(_value!, Error!),
                ResultState.Cancelled when onCancelled != null => onCancelled(Error!),
                ResultState.Pending when onPending != null => onPending(Error!),
                ResultState.NoOp when onNoOp != null => onNoOp(Error!),
                _ => onFailure(Error!, Exception)
            };
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during match operation");
            Telemetry.TrackException(ex, State, GetType());
            return onFailure(Error ?? CreateDefaultError(), ex);
        }
    }

    /// <summary>
    ///     Performs async pattern matching with ValueTask optimization.
    /// </summary>
    public async ValueTask<TResult> MatchAsync<TResult>(
        Func<T, ValueTask<TResult>> onSuccess,
        Func<TError, Exception?, ValueTask<TResult>> onFailure,
        Func<T, TError, ValueTask<TResult>>? onWarning = null,
        Func<T, TError, ValueTask<TResult>>? onPartialSuccess = null,
        Func<TError, ValueTask<TResult>>? onCancelled = null,
        Func<TError, ValueTask<TResult>>? onPending = null,
        Func<TError, ValueTask<TResult>>? onNoOp = null)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            return State switch
            {
                ResultState.Success when _hasValue =>
                    await onSuccess(_value!).ConfigureAwait(false),
                ResultState.Warning when onWarning != null && _hasValue =>
                    await onWarning(_value!, Error!).ConfigureAwait(false),
                ResultState.PartialSuccess when onPartialSuccess != null && _hasValue =>
                    await onPartialSuccess(_value!, Error!).ConfigureAwait(false),
                ResultState.Cancelled when onCancelled != null =>
                    await onCancelled(Error!).ConfigureAwait(false),
                ResultState.Pending when onPending != null =>
                    await onPending(Error!).ConfigureAwait(false),
                ResultState.NoOp when onNoOp != null =>
                    await onNoOp(Error!).ConfigureAwait(false),
                _ => await onFailure(Error!, Exception).ConfigureAwait(false)
            };
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during async match operation");
            Telemetry.TrackException(ex, State, GetType());
            return await onFailure(Error ?? CreateDefaultError(), ex).ConfigureAwait(false);
        }
    }

    #endregion

    #region Internal Implementation

    /// <summary>
    ///     Creates a default error for fallback scenarios.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TError CreateDefaultError()
    {
        return (TError)Activator.CreateInstance(typeof(TError), "Operation failed with unhandled exception")!;
    }

    /// <summary>
    ///     Initializes the result instance with a value for pooling.
    ///     Internal method for use by legacy compatibility layer.
    /// </summary>
    internal void InitializeFromValue(T value, ResultState state, TError? error = null, Exception? exception = null)
    {
        // Use reflection to set base class fields safely
        var baseType = typeof(ResultBase);
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        try
        {
            // Set the State field on ResultBase
            var stateField = baseType.GetField("<State>k__BackingField", flags);
            if (stateField != null)
            {
                stateField.SetValue(this, state);
            }

            // Set the Exception field on ResultBase
            var exceptionField = baseType.GetField("<Exception>k__BackingField", flags);
            if (exceptionField != null)
            {
                exceptionField.SetValue(this, exception);
            }

            // Set the Error field on Result<TError>
            var errorField = typeof(Base.Result<TError>).GetField("<Error>k__BackingField", flags);
            if (errorField != null)
            {
                errorField.SetValue(this, error);
            }

            // Set the value container using reflection
            var valueContainerField = typeof(Result<T, TError>).GetField("_valueContainer", flags);
            if (valueContainerField != null)
            {
                // Create a new ValueContainer with the value
                var containerType = valueContainerField.FieldType;
                var constructor = containerType.GetConstructor(
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic,
                    null,
                    new[] { typeof(T) },
                    null);

                if (constructor != null)
                {
                    var container = constructor.Invoke(new object?[] { value });
                    valueContainerField.SetValue(this, container);
                }
            }
        }
        catch (Exception ex)
        {
            // Fallback: Log the error
            System.Diagnostics.Debug.WriteLine(
                $"Failed to initialize pooled result instance with value via reflection: {ex.Message}");
            throw new InvalidOperationException("Cannot initialize pooled result instance with value", ex);
        }
    }

    #endregion

    #region Factory Methods

    /// <summary>
    ///     Creates a success result with the specified value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> Success(T value)
    {
        return new Result<T, TError>(value, ResultState.Success);
    }

    /// <summary>
    ///     Creates a failure result with the specified error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new static Result<T, TError> Failure(TError error, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T, TError>(default, ResultState.Failure, error, exception);
    }

    /// <summary>
    ///     Creates a partial success result with both value and error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> PartialSuccess(T value, TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T, TError>(value, ResultState.PartialSuccess, error);
    }

    /// <summary>
    ///     Creates a warning result with both value and error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> Warning(T value, TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T, TError>(value, ResultState.Warning, error);
    }

    /// <summary>
    ///     Creates a cancelled result with no value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new static Result<T, TError> Cancelled(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T, TError>(default, ResultState.Cancelled, error);
    }

    /// <summary>
    ///     Creates a cancelled result with value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> Cancelled(T value, TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T, TError>(value, ResultState.Cancelled, error);
    }

    /// <summary>
    ///     Creates a pending result with no value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new static Result<T, TError> Pending(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T, TError>(default, ResultState.Pending, error);
    }

    /// <summary>
    ///     Creates a pending result with value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> Pending(T value, TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T, TError>(value, ResultState.Pending, error);
    }

    /// <summary>
    ///     Creates a no-op result with no value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new static Result<T, TError> NoOp(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T, TError>(default, ResultState.NoOp, error);
    }

    /// <summary>
    ///     Creates a no-op result with value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> NoOp(T value, TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T, TError>(value, ResultState.NoOp, error);
    }

    #endregion

    #region Operators

    /// <summary>
    ///     Implicitly converts a value of type T into a success result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<T, TError>(T value)
    {
        return Success(value);
    }

    /// <summary>
    ///     Implicitly converts an error into a failure result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<T, TError>(TError error)
    {
        return Failure(error);
    }

    #endregion

    #region Equality and Debugging

    /// <summary>
    ///     Determines equality with optimized comparison logic.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj switch
        {
            null => false,
            Result<T, TError> other => EqualsResult(other),
            T value => IsSuccess && _hasValue && EqualityComparer<T>.Default.Equals(_value, value),
            TError error => !IsSuccess && Error is not null && Error.Equals(error),
            _ => false
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool EqualsResult(Result<T, TError> other)
    {
        if (ReferenceEquals(this, other)) return true;

        if (State != other.State) return false;

        // Compare errors
        if (!EqualityComparer<TError?>.Default.Equals(Error, other.Error)) return false;

        // Compare exceptions
        if (!Equals(Exception, other.Exception)) return false;

        // Compare values for success states
        if (IsSuccess)
        {
            if (_hasValue != other._hasValue) return false;
            if (_hasValue)
            {
                return EqualityComparer<T>.Default.Equals(_value, other._value);
            }
        }

        return true;
    }

    /// <summary>
    ///     Gets an optimized hash code.
    /// </summary>
    public override int GetHashCode()
    {
        var hash = HashCode.Combine(State, Error, Exception?.GetType());

        if (IsSuccess && _hasValue)
        {
            hash = HashCode.Combine(hash, _value);
        }

        return hash;
    }

    /// <summary>
    ///     Gets an optimized string representation.
    /// </summary>
    public override string ToString()
    {
        var typeName = $"Result<{typeof(T).Name}, {typeof(TError).Name}>";
        var stateInfo = $"[{State}]";

        var valueInfo = State switch
        {
            ResultState.Success when _hasValue => $": {_value}",
            _ when Error != null => $": {Error.Message}",
            _ => ""
        };

        return $"{typeName}{stateInfo}{valueInfo}";
    }

    private string DebuggerDisplay => ToString();

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Dictionary<string, object?> DebugView
    {
        get
        {
            var view = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "State", State },
                { "IsSuccess", IsSuccess },
                { "ValueType", typeof(T).Name },
                { "ErrorType", typeof(TError).Name },
                { "Age", DateTime.UtcNow - CreatedAt }
            };

            if (IsSuccess && _hasValue)
            {
                view.Add("Value", _value);
            }

            if (Error != null)
            {
                view.Add("Error", Error.Message);
            }

            if (Exception != null)
            {
                view.Add("Exception", Exception.Message);
            }

            return view;
        }
    }

    #endregion
}
