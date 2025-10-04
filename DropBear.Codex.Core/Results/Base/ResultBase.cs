#region

using System.Collections.Frozen;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Diagnostics;
using DropBear.Codex.Core.Results.Errors;
using DropBear.Codex.Core.Results.Extensions;
using Serilog;

#endregion

namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     An abstract base class for all Result types, providing common functionality.
///     Optimized for .NET 9 with enhanced performance, reduced allocations, and improved diagnostics.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public abstract class ResultBase : IResult, IResultDiagnostics
{
    // Static resources shared by all result types
    private protected static readonly ILogger Logger = LoggerFactory.Logger.ForContext<ResultBase>();

    // Frozen set for better performance in .NET 9 - using collection expression
    private static readonly FrozenSet<ResultState> ValidStates =
    [
        ResultState.Success,
        ResultState.Failure,
        ResultState.Pending,
        ResultState.Cancelled,
        ResultState.Warning,
        ResultState.PartialSuccess,
        ResultState.NoOp
    ];

    // Diagnostic info for this result
    private DiagnosticInfo _diagnosticInfo;

    // Exception storage - using collection expression for empty array
    private Exception[] _exceptions;

    // Time provider for testability (.NET 9 feature)
    private static TimeProvider _timeProvider = TimeProvider.System;

    /// <summary>
    ///     Gets the creation timestamp for this result.
    /// </summary>
    public DateTime CreatedAt => _diagnosticInfo.CreatedAt;

    /// <summary>
    ///     Sets the time provider for testing purposes.
    ///     Only use this in unit tests.
    /// </summary>
    internal static void SetTimeProvider(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <summary>
    ///     Resets the time provider to the system default.
    /// </summary>
    internal static void ResetTimeProvider()
    {
        _timeProvider = TimeProvider.System;
    }

    /// <summary>
    ///     Initializes a new instance of <see cref="ResultBase" />.
    ///     Optimized constructor for .NET 9 with improved validation and reduced allocations.
    /// </summary>
    /// <param name="state">The <see cref="ResultState" /> (e.g., Success, Failure, etc.).</param>
    /// <param name="exception">An optional <see cref="Exception" /> if the result failed.</param>
    protected ResultBase(ResultState state, Exception? exception = null)
    {
        ValidateState(state);

        State = state;
        _exceptions = CreateExceptionArray(exception);
        _diagnosticInfo = DiagnosticInfo.Create(state, GetType());

        // Track telemetry for result creation
        TrackTelemetryAsync(state, exception);
    }

    #region Performance-Optimized Methods

    /// <summary>
    ///     Fast path state check without virtual call overhead.
    ///     Uses aggressive inlining for hot path optimization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsSuccessFast() => State is ResultState.Success or ResultState.PartialSuccess;

    /// <summary>
    ///     Fast path failure check.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsFailureFast() => State is ResultState.Failure;

    /// <summary>
    ///     Gets exception count without enumerating the collection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetExceptionCount() => _exceptions.Length;

    /// <summary>
    ///     Tries to get the primary exception without allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetException(out Exception? exception)
    {
        if (_exceptions.Length > 0)
        {
            exception = _exceptions[0];
            return true;
        }

        exception = null;
        return false;
    }

    #endregion

    #region IResult Implementation

    /// <inheritdoc />
    public ResultState State { get; protected set; }

    /// <inheritdoc />
    public bool IsSuccess => State.IsSuccessState();

    /// <inheritdoc />
    public Exception? Exception => _exceptions.Length > 0 ? _exceptions[0] : null;

    /// <inheritdoc />
    public IReadOnlyCollection<Exception> Exceptions => _exceptions;

    #endregion

    #region IResultDiagnostics Implementation

    /// <inheritdoc />
    public DiagnosticInfo GetDiagnostics() => _diagnosticInfo;

    /// <inheritdoc />
    public ActivityContext GetTraceContext()
    {
        var current = Activity.Current;
        return current?.Context ?? default;
    }

    #endregion

    #region Pooling Support

    /// <summary>
    ///     Re-initializes the result state for pooled instances.
    ///     This is an internal method used only by the compatibility layer pooling.
    /// </summary>
    /// <param name="state">The new result state.</param>
    /// <param name="exception">Optional exception.</param>
    protected internal void SetStateInternal(ResultState state, Exception? exception)
    {
        ValidateState(state);

        // Update the state
        State = state;

        // Update exceptions
        _exceptions = CreateExceptionArray(exception);

        // Update diagnostic info
        _diagnosticInfo = DiagnosticInfo.Create(state, GetType());

        // Track telemetry
        TrackTelemetryAsync(state, exception);
    }

    #endregion

    #region Validation

    /// <summary>
    ///     Validates that the provided state is a valid ResultState value.
    ///     Uses modern frozen set lookup for optimal performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected static void ValidateState(ResultState state)
    {
        if (!ValidStates.Contains(state))
        {
            throw new ResultException($"Invalid ResultState value: {state}");
        }
    }

    /// <summary>
    ///     Validates that the error state is consistent with the provided error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected static void ValidateErrorState<TError>(ResultState state, TError? error)
        where TError : ResultError
    {
        // Failure, Warning, PartialSuccess, Cancelled, Pending, NoOp require an error
        var requiresError = state is ResultState.Failure or ResultState.Warning
            or ResultState.PartialSuccess or ResultState.Cancelled
            or ResultState.Pending or ResultState.NoOp;

        if (requiresError && error is null)
        {
            throw new ResultException($"ResultState {state} requires an error, but none was provided.");
        }

        if (!requiresError && error is not null)
        {
            Logger.Warning("ResultState {State} does not typically have an error, but one was provided", state);
        }
    }

    #endregion

    #region Exception Handling

    /// <summary>
    ///     Creates an exception array from a single exception (if present).
    ///     Uses modern collection expressions for optimal allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Exception[] CreateExceptionArray(Exception? exception)
    {
        return exception switch
        {
            null => [],
            AggregateException aggEx => [.. aggEx.Flatten().InnerExceptions],
            _ => [exception]
        };
    }

    #endregion

    #region Telemetry

    /// <summary>
    ///     Tracks telemetry for this result using the global telemetry provider.
    ///     Non-blocking operation that respects the configured telemetry mode.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TrackTelemetryAsync(ResultState state, Exception? exception)
    {
        // Only track if telemetry is enabled
        if (!TelemetryProvider.IsEnabled)
            return;

        var telemetry = TelemetryProvider.Current;

        if (exception is not null)
        {
            telemetry.TrackException(exception, state, GetType());
        }
        else
        {
            telemetry.TrackResultCreated(state, GetType());
        }
    }

    #endregion

    #region Debugger Display

    /// <summary>
    ///     Gets a string representation for the debugger display.
    /// </summary>
    protected virtual string DebuggerDisplay =>
        $"State = {State}, Success = {IsSuccess}";

    #endregion
}
