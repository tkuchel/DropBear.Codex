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
///     A robust Result type for operations without return values, carrying rich error information.
///     Optimized for .NET 9 with direct allocation and modern performance patterns.
/// </summary>
/// <typeparam name="TError">A type inheriting from ResultError.</typeparam>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[JsonConverter(typeof(ResultJsonConverter<>))]
public class Result<TError> : ResultBase, IResult<TError>
    where TError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of Result{TError}.
    /// </summary>
    /// <param name="state">The state of the result.</param>
    /// <param name="error">Optional error if the result is not successful.</param>
    /// <param name="exception">Optional exception if an error occurred.</param>
    protected Result(ResultState state, TError? error = null, Exception? exception = null)
        : base(state, exception)
    {
        ValidateErrorState(state, error);
        Error = error;
    }

    /// <summary>
    ///     Gets the error object if the result is unsuccessful.
    /// </summary>
    public TError? Error { get; protected set; }

    #region Pattern Matching

    /// <summary>
    ///     Performs pattern matching on the current state with optimized performance.
    /// </summary>
    public T Match<T>(
        Func<T> onSuccess,
        Func<TError, Exception?, T> onFailure,
        Func<TError, T>? onWarning = null,
        Func<TError, T>? onPartialSuccess = null,
        Func<TError, T>? onCancelled = null,
        Func<TError, T>? onPending = null,
        Func<TError, T>? onNoOp = null)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            return State switch
            {
                ResultState.Success => onSuccess(),
                ResultState.Failure => onFailure(Error!, Exception),
                ResultState.Warning => InvokeOrDefault(onWarning),
                ResultState.PartialSuccess => InvokeOrDefault(onPartialSuccess),
                ResultState.Cancelled => InvokeOrDefault(onCancelled),
                ResultState.Pending => InvokeOrDefault(onPending),
                ResultState.NoOp => InvokeOrDefault(onNoOp),
                _ => throw new ResultException($"Unhandled state: {State}")
            };
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during match operation");
            Telemetry.TrackException(ex, State, GetType());
            return onFailure(Error ?? CreateDefaultError(), ex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        T InvokeOrDefault(Func<TError, T>? handler)
        {
            return handler is not null ? handler(Error!) : onFailure(Error!, Exception);
        }
    }

    /// <summary>
    ///     Performs async pattern matching with ValueTask optimization.
    /// </summary>
    public async ValueTask<T> MatchAsync<T>(
        Func<ValueTask<T>> onSuccess,
        Func<TError, Exception?, ValueTask<T>> onFailure,
        Func<TError, ValueTask<T>>? onWarning = null,
        Func<TError, ValueTask<T>>? onPartialSuccess = null,
        Func<TError, ValueTask<T>>? onCancelled = null,
        Func<TError, ValueTask<T>>? onPending = null,
        Func<TError, ValueTask<T>>? onNoOp = null)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            return State switch
            {
                ResultState.Success => await onSuccess().ConfigureAwait(false),
                ResultState.Failure => await onFailure(Error!, Exception).ConfigureAwait(false),
                ResultState.Warning => await InvokeOrDefaultAsync(onWarning).ConfigureAwait(false),
                ResultState.PartialSuccess => await InvokeOrDefaultAsync(onPartialSuccess).ConfigureAwait(false),
                ResultState.Cancelled => await InvokeOrDefaultAsync(onCancelled).ConfigureAwait(false),
                ResultState.Pending => await InvokeOrDefaultAsync(onPending).ConfigureAwait(false),
                ResultState.NoOp => await InvokeOrDefaultAsync(onNoOp).ConfigureAwait(false),
                _ => throw new ResultException($"Unhandled state: {State}")
            };
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during async match operation");
            Telemetry.TrackException(ex, State, GetType());
            return await onFailure(Error ?? CreateDefaultError(), ex).ConfigureAwait(false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        async ValueTask<T> InvokeOrDefaultAsync(Func<TError, ValueTask<T>>? handler)
        {
            return handler is not null
                ? await handler(Error!).ConfigureAwait(false)
                : await onFailure(Error!, Exception).ConfigureAwait(false);
        }
    }

    #endregion

    #region Recovery and Validation

    /// <summary>
    ///     Attempts to recover from a failure by invoking a recovery function.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TError> Recover(Func<TError, Exception?, Result<TError>> recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);

        if (IsSuccess) return this;

        try
        {
            return recovery(Error!, Exception);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during recovery");
            Telemetry.TrackException(ex, State, GetType());
            return this;
        }
    }

    /// <summary>
    ///     Attempts to recover from a failure asynchronously with ValueTask optimization.
    /// </summary>
    public async ValueTask<Result<TError>> RecoverAsync(
        Func<TError, Exception?, ValueTask<Result<TError>>> recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);

        if (IsSuccess) return this;

        try
        {
            return await recovery(Error!, Exception).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during async recovery");
            Telemetry.TrackException(ex, State, GetType());
            return this;
        }
    }

    /// <summary>
    ///     Ensures a specified condition is met.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TError> Ensure(Func<bool> predicate, TError error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);

        if (!IsSuccess) return this;

        try
        {
            return predicate() ? this : Failure(error);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during ensure predicate");
            Telemetry.TrackException(ex, State, GetType());
            return Failure(error, ex);
        }
    }

    /// <summary>
    ///     Ensures a specified condition is met asynchronously with ValueTask optimization.
    /// </summary>
    public async ValueTask<Result<TError>> EnsureAsync(
        Func<ValueTask<bool>> predicate,
        TError error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);

        if (!IsSuccess) return this;

        try
        {
            var success = await predicate().ConfigureAwait(false);
            return success ? this : Failure(error);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during async ensure predicate");
            Telemetry.TrackException(ex, State, GetType());
            return Failure(error, ex);
        }
    }

    #endregion

    #region Transformation Methods

    /// <summary>
    ///     Executes an action if the result is successful, without changing the result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TError> Tap(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (IsSuccess)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Exception during tap operation");
                Telemetry.TrackException(ex, State, GetType());
            }
        }

        return this;
    }

    /// <summary>
    ///     Executes an async action if the result is successful, without changing the result.
    /// </summary>
    public async ValueTask<Result<TError>> TapAsync(Func<ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (IsSuccess)
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Exception during async tap operation");
                Telemetry.TrackException(ex, State, GetType());
            }
        }

        return this;
    }

    /// <summary>
    ///     Maps the error to a new error type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TNewError> MapError<TNewError>(Func<TError, TNewError> errorMapper)
        where TNewError : ResultError
    {
        ArgumentNullException.ThrowIfNull(errorMapper);

        return IsSuccess
            ? Result<TNewError>.Success()
            : Result<TNewError>.Failure(errorMapper(Error!), Exception);
    }

    #endregion

    #region Internal Implementation

    /// <summary>
    ///     Validates that the error state is consistent with the result state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateErrorState(ResultState state, TError? error)
    {
        if (state is ResultState.Failure or ResultState.PartialSuccess && error is null)
        {
            throw new ResultValidationException("Error is required for non-success results");
        }
    }

    /// <summary>
    ///     Creates a default error with a generic message.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TError CreateDefaultError()
    {
        return (TError)Activator.CreateInstance(typeof(TError), "Operation failed with unhandled exception")!;
    }

    #endregion

    #region Factory Methods

    /// <summary>
    ///     Creates a new Result in the Success state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TError> Success()
    {
        return new Result<TError>(ResultState.Success);
    }

    /// <summary>
    ///     Creates a new Result in the Failure state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TError> Failure(TError error, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<TError>(ResultState.Failure, error, exception);
    }

    /// <summary>
    ///     Creates a new Result in the Warning state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TError> Warning(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<TError>(ResultState.Warning, error);
    }

    /// <summary>
    ///     Creates a new Result in the PartialSuccess state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TError> PartialSuccess(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<TError>(ResultState.PartialSuccess, error);
    }

    /// <summary>
    ///     Creates a new Result in the Cancelled state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TError> Cancelled(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<TError>(ResultState.Cancelled, error);
    }

    /// <summary>
    ///     Creates a new Result in the Pending state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TError> Pending(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<TError>(ResultState.Pending, error);
    }

    /// <summary>
    ///     Creates a new Result in the NoOp (No Operation) state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TError> NoOp(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<TError>(ResultState.NoOp, error);
    }

    #endregion

    #region Equality and Debugging

    /// <summary>
    ///     Determines whether this result is equal to an error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(TError? other)
    {
        return other is not null && Error is not null && Error.Equals(other);
    }

    /// <summary>
    ///     Determines whether this result is equal to another object.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj switch
        {
            null => false,
            Result<TError> other => EqualsResult(other),
            TError error => Equals(error),
            _ => false
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool EqualsResult(Result<TError> other)
    {
        if (ReferenceEquals(this, other)) return true;

        return State == other.State &&
               EqualityComparer<TError?>.Default.Equals(Error, other.Error) &&
               Equals(Exception, other.Exception);
    }

    /// <summary>
    ///     Gets a hash code for this result with optimized computation.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(State, Error, Exception?.GetType());
    }

    /// <summary>
    ///     Gets a string representation optimized for debugging.
    /// </summary>
    public override string ToString()
    {
        return $"Result<{typeof(TError).Name}>[{State}]{(Error != null ? $": {Error.Message}" : "")}";
    }

    private string DebuggerDisplay => ToString();

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Dictionary<string, object?> DebugView =>
        new(StringComparer.Ordinal)
        {
            { "State", State },
            { "IsSuccess", IsSuccess },
            { "Error", Error?.Message },
            { "Exception", Exception?.Message },
            { "ErrorType", typeof(TError).Name },
            { "Age", DateTime.UtcNow - CreatedAt }
        };

    #endregion
}
