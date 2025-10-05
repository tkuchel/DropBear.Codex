#region

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Diagnostics;
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
    ///     Creates a new Result in the NoOp state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TError> NoOp(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<TError>(ResultState.NoOp, error);
    }

    #endregion

    #region Pooling Support for Compatibility Layer

    /// <summary>
    ///     Initializes the result instance with new state.
    ///     Internal method used by the pooling compatibility layer.
    /// </summary>
    /// <param name="state">The result state.</param>
    /// <param name="error">The error object.</param>
    /// <param name="exception">Optional exception.</param>
    protected internal void InitializeInternal(ResultState state, TError? error, Exception? exception)
    {
        ValidateErrorState(state, error);

        // Update the base state (this reinitializes the diagnostic info)
        SetStateInternal(state, exception);

        // Update the error
        Error = error;
    }

    #endregion

    #region Pattern Matching

    /// <summary>
    ///     Performs pattern matching on the current state with optimized performance.
    ///     Uses modern C# switch expressions for better codegen.
    /// </summary>
    public TResult Match<TResult>(
        Func<TResult> onSuccess,
        Func<TError, Exception?, TResult> onFailure,
        Func<TError, TResult>? onWarning = null,
        Func<TError, TResult>? onPartialSuccess = null,
        Func<TError, TResult>? onCancelled = null,
        Func<TError, TResult>? onPending = null,
        Func<TError, TResult>? onNoOp = null)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            return (State, Error) switch
            {
                (ResultState.Success, _) => onSuccess(),
                (ResultState.Failure, not null) => onFailure(Error, Exception),
                (ResultState.Warning, not null) when onWarning != null => onWarning(Error),
                (ResultState.PartialSuccess, not null) when onPartialSuccess != null => onPartialSuccess(Error),
                (ResultState.Cancelled, not null) when onCancelled != null => onCancelled(Error),
                (ResultState.Pending, not null) when onPending != null => onPending(Error),
                (ResultState.NoOp, not null) when onNoOp != null => onNoOp(Error),
                (_, not null) => onFailure(Error, Exception),
                _ => onFailure(CreateDefaultError(), Exception)
            };
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during match operation");
            TelemetryProvider.Current.TrackException(ex, State, GetType());
            return onFailure(Error ?? CreateDefaultError(), ex);
        }
    }

    /// <summary>
    ///     Asynchronous pattern matching with modern async patterns.
    /// </summary>
    public async ValueTask<TResult> MatchAsync<TResult>(
        Func<ValueTask<TResult>> onSuccess,
        Func<TError, Exception?, ValueTask<TResult>> onFailure,
        Func<TError, ValueTask<TResult>>? onWarning = null,
        Func<TError, ValueTask<TResult>>? onPartialSuccess = null)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            return (State, Error) switch
            {
                (ResultState.Success, _) => await onSuccess().ConfigureAwait(false),
                (ResultState.Failure, not null) => await onFailure(Error, Exception).ConfigureAwait(false),
                (ResultState.Warning, not null) when onWarning != null =>
                    await onWarning(Error).ConfigureAwait(false),
                (ResultState.PartialSuccess, not null) when onPartialSuccess != null =>
                    await onPartialSuccess(Error).ConfigureAwait(false),
                (_, not null) => await onFailure(Error, Exception).ConfigureAwait(false),
                _ => await onFailure(CreateDefaultError(), Exception).ConfigureAwait(false)
            };
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during async match operation");
            TelemetryProvider.Current.TrackException(ex, State, GetType());
            return await onFailure(Error ?? CreateDefaultError(), ex).ConfigureAwait(false);
        }
    }

    #endregion

    #region Transformation Methods

    /// <summary>
    ///     Executes an action if the result is successful.
    ///     Returns the same result for chaining.
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
                Logger.Error(ex, "Exception during Tap operation");
                TelemetryProvider.Current.TrackException(ex, State, GetType());
            }
        }

        return this;
    }

    /// <summary>
    ///     Executes an async action if the result is successful.
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
                Logger.Error(ex, "Exception during async Tap operation");
                TelemetryProvider.Current.TrackException(ex, State, GetType());
            }
        }

        return this;
    }

    /// <summary>
    ///     Transforms a successful result into a Result with a value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<T, TError> Map<T>(Func<T> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        if (!IsSuccess)
        {
            return Result<T, TError>.Failure(Error!);
        }

        try
        {
            var value = factory();
            return Result<T, TError>.Success(value);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during Map operation");
            TelemetryProvider.Current.TrackException(ex, State, GetType());
            return Result<T, TError>.Failure(CreateErrorFromException(ex), ex);
        }
    }

    /// <summary>
    ///     Binds this result to another result-returning function.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TError> Bind(Func<Result<TError>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);

        if (!IsSuccess)
        {
            return this;
        }

        try
        {
            return binder();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during Bind operation");
            TelemetryProvider.Current.TrackException(ex, State, GetType());
            return Failure(CreateErrorFromException(ex), ex);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Creates an error from an exception.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TError CreateErrorFromException(Exception exception)
    {
        try
        {
            return (TError)Activator.CreateInstance(
                typeof(TError),
                $"Operation failed: {exception.Message}")!;
        }
        catch
        {
            return CreateDefaultError();
        }
    }

    /// <summary>
    ///     Creates a default error instance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TError CreateDefaultError()
    {
        return (TError)Activator.CreateInstance(
            typeof(TError),
            "Operation failed with unhandled exception")!;
    }

    #endregion

    #region Debugger Display

    private string DebuggerDisplay =>
        $"State = {State}, Success = {IsSuccess}, Error = {Error?.Message ?? "null"}";

    #endregion

    #region Operators

    /// <summary>
    ///     Implicit conversion from error to failure result.
    /// </summary>
    public static implicit operator Result<TError>(TError error)
    {
        return Failure(error);
    }

    #endregion
}
