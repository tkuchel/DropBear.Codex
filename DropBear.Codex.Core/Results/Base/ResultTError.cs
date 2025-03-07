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
///     A robust Result type for operations without return values, carrying rich error information.
///     This class forms the foundation of the Result pattern in the library.
/// </summary>
/// <typeparam name="TError">A type inheriting from ResultError.</typeparam>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[JsonConverter(typeof(ResultJsonConverter<>))]
public class Result<TError> : ResultBase, IResult<TError>
    where TError : ResultError
{
    // Shared object pool for result instances
    private static readonly ObjectPool<Result<TError>> ResultPool =
        ObjectPoolManager.GetPool(() => new Result<TError>(ResultState.Success));

    #region Constructors

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

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the error object if the result is unsuccessful.
    /// </summary>
    public TError? Error { get; private set; }

    #endregion

    #region Match / Recover / Ensure

    /// <summary>
    ///     Performs pattern matching on the current state.
    /// </summary>
    /// <typeparam name="T">The return type of the pattern matching.</typeparam>
    /// <param name="onSuccess">Function to call if the result is successful.</param>
    /// <param name="onFailure">Function to call if the result is a failure.</param>
    /// <param name="onWarning">Optional function to call if the result is a warning.</param>
    /// <param name="onPartialSuccess">Optional function to call if the result is a partial success.</param>
    /// <param name="onCancelled">Optional function to call if the result is cancelled.</param>
    /// <param name="onPending">Optional function to call if the result is pending.</param>
    /// <param name="onNoOp">Optional function to call if the result is a no-op.</param>
    /// <returns>The result of the matching function.</returns>
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
                _ => throw new ResultException($"Unhandled state: {State}")
            };
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during match operation");
            Telemetry.TrackException(ex, State, GetType());
            return onFailure(Error ?? CreateDefaultError(), ex);
        }

        T InvokeOrDefault(Func<TError, T>? handler, Func<TError, Exception?, T> defaultHandler)
        {
            return handler is not null ? handler(Error!) : defaultHandler(Error!, Exception);
        }
    }

    /// <summary>
    ///     Attempts to recover from a failure by invoking a recovery function.
    /// </summary>
    /// <param name="recovery">A function that attempts to recover from the failure.</param>
    /// <returns>Either this result (if successful) or the result of the recovery function.</returns>
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
            Telemetry.TrackException(ex, State, GetType());
            return this;
        }
    }

    /// <summary>
    ///     Ensures a specified condition is met.
    /// </summary>
    /// <param name="predicate">A function that returns true if the condition is met.</param>
    /// <param name="error">The error to return if the condition is not met.</param>
    /// <returns>Either this result (if successful and the condition is met) or a failure result.</returns>
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
            Telemetry.TrackException(ex, State, GetType());
            return Failure(error, ex);
        }
    }

    #endregion

    #region NEW Async Versions of Recover / Ensure

    /// <summary>
    ///     Attempts to recover from a failure asynchronously by invoking a recovery function.
    /// </summary>
    /// <param name="recovery">A function that attempts to recover from the failure asynchronously.</param>
    /// <returns>A ValueTask returning either this result (if successful) or the recovered result.</returns>
    public async ValueTask<Result<TError>> RecoverAsync(
        Func<TError, Exception?, ValueTask<Result<TError>>> recovery)
    {
        if (IsSuccess)
        {
            return this;
        }

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
    ///     Ensures a specified condition is met asynchronously.
    /// </summary>
    /// <param name="predicate">A function that returns true if the condition is met.</param>
    /// <param name="error">The error to return if the condition is not met.</param>
    /// <returns>A ValueTask returning either this result (if successful) or a failure result.</returns>
    public async ValueTask<Result<TError>> EnsureAsync(
        Func<ValueTask<bool>> predicate,
        TError error)
    {
        if (!IsSuccess)
        {
            return this;
        }

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

    #region Internal Implementation

    /// <summary>
    ///     Validates that the error state is consistent with the result state.
    /// </summary>
    /// <param name="state">The result state.</param>
    /// <param name="error">The error object, if any.</param>
    /// <exception cref="ResultValidationException">Thrown if the state and error are inconsistent.</exception>
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
    private TError CreateDefaultError()
    {
        return (TError)Activator.CreateInstance(typeof(TError), "Operation failed with unhandled exception")!;
    }

    /// <summary>
    ///     Initializes or reinitializes the result instance for pooling.
    ///     This is purely internal – you might hide or rename it if desired.
    /// </summary>
    internal void InitializeInternal(ResultState state, TError? error = null, Exception? exception = null)
    {
        // Bypass normal constructor logic using reflection or direct field assignment
        var baseType = typeof(ResultBase);
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;

        // Set the "State" field (on the base class)
        baseType.GetField("<State>k__BackingField", flags)?.SetValue(this, state);

        // Set the "Exception" field (on the base class)
        baseType.GetField("<Exception>k__BackingField", flags)?.SetValue(this, exception);

        // Then set ours
        Error = error;
    }

    #endregion

    #region Factory Methods

    /// <summary>
    ///     Creates a new Result in the Success state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TError> Success()
    {
        var result = ResultPool.Get();
        result.InitializeInternal(ResultState.Success);
        return result;
    }

    /// <summary>
    ///     Creates a new Result in the Failure state.
    /// </summary>
    /// <param name="error">The error that caused the failure.</param>
    /// <param name="exception">Optional exception associated with the failure.</param>
    /// <returns>A failed result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TError> Failure(TError error, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        var result = ResultPool.Get();
        result.InitializeInternal(ResultState.Failure, error, exception);
        return result;
    }

    /// <summary>
    ///     Creates a new Result in the Warning state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TError> Warning(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var result = ResultPool.Get();
        result.InitializeInternal(ResultState.Warning, error);
        return result;
    }

    /// <summary>
    ///     Creates a new Result in the PartialSuccess state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TError> PartialSuccess(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var result = ResultPool.Get();
        result.InitializeInternal(ResultState.PartialSuccess, error);
        return result;
    }

    /// <summary>
    ///     Creates a new Result in the Cancelled state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TError> Cancelled(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var result = ResultPool.Get();
        result.InitializeInternal(ResultState.Cancelled, error);
        return result;
    }

    /// <summary>
    ///     Creates a new Result in the Pending state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TError> Pending(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var result = ResultPool.Get();
        result.InitializeInternal(ResultState.Pending, error);
        return result;
    }

    /// <summary>
    ///     Creates a new Result in the NoOp (No Operation) state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TError> NoOp(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var result = ResultPool.Get();
        result.InitializeInternal(ResultState.NoOp, error);
        return result;
    }

    #endregion

    #region Equality / Debugging

    /// <summary>
    ///     Determines whether this result is equal to an error.
    /// </summary>
    public bool Equals(TError? other)
    {
        if (other is null)
        {
            return false;
        }

        return Error is not null && Error.Equals(other);
    }

    /// <summary>
    ///     Determines whether this result is equal to another object.
    /// </summary>
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
               Equals(Exception, other.Exception);
    }

    /// <summary>
    ///     Gets a hash code for this result.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(State, Error, Exception);
    }

    private string DebuggerDisplay => $"State = {State}, Error = {Error?.Message ?? "None"}";

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Dictionary<string, object?> DebugView =>
        new(StringComparer.Ordinal)
        {
            { "State", State },
            { "IsSuccess", IsSuccess },
            { "Error", Error?.Message },
            { "Exception", Exception?.Message }
        };

    #endregion
}
