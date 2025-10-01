#region

using System.Collections.Frozen;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Diagnostics;
using DropBear.Codex.Core.Results.Errors;
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
    private protected static readonly IResultTelemetry Telemetry = new DefaultResultTelemetry();

    // Frozen set for better performance in .NET 9
    private static readonly FrozenSet<ResultState> ValidStates =
        Enum.GetValues<ResultState>().ToFrozenSet();

    // Diagnostic info for this result
    private readonly DiagnosticInfo _diagnosticInfo;

    // Exception storage - using array for better performance
    private readonly Exception[] _exceptions;

    /// <summary>
    ///     Gets the creation timestamp for this result.
    /// </summary>
    public DateTime CreatedAt => _diagnosticInfo.CreatedAt;

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
        Exception = exception;
        _exceptions = CreateExceptionArray(exception);

        // Initialize diagnostic data
        _diagnosticInfo = new DiagnosticInfo(
            state,
            GetType(),
            DateTime.UtcNow,
            Activity.Current?.Id);

        // Track telemetry asynchronously to avoid blocking
        _ = Task.Run(() =>
        {
            Telemetry.TrackResultCreated(state, GetType());

            if (exception != null)
            {
                Telemetry.TrackException(exception, state, GetType());
            }
        });
    }

    #region IResult Properties

    /// <summary>
    ///     Gets the <see cref="ResultState" /> of this result.
    /// </summary>
    public ResultState State { get; }

    /// <summary>
    ///     Indicates whether the result represents success or partial success.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public bool IsSuccess => State is ResultState.Success or ResultState.PartialSuccess;

    /// <summary>
    ///     Indicates whether the result represents a complete success.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public bool IsCompleteSuccess => State == ResultState.Success;

    /// <summary>
    ///     Indicates whether the result represents a failure.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public bool IsFailure => !IsSuccess;

    /// <summary>
    ///     Gets the primary exception associated with this result, if any.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    ///     Gets all exceptions associated with this result as a span.
    /// </summary>
    public virtual ReadOnlySpan<Exception> Exceptions => _exceptions.AsSpan();

    /// <summary>
    ///     Gets all exceptions as a collection for compatibility.
    /// </summary>
    IReadOnlyCollection<Exception> IResult.Exceptions => _exceptions;

    #endregion

    #region IResultDiagnostics Implementation

    /// <summary>
    ///     Gets diagnostic information about this result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DiagnosticInfo GetDiagnostics() => _diagnosticInfo;

    /// <summary>
    ///     Gets the trace context for this result operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ActivityContext GetTraceContext() => Activity.Current?.Context ?? default;

    #endregion

    #region Protected Methods - Enhanced for .NET 9

    /// <summary>
    ///     Executes the specified action safely, catching and logging any exceptions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void SafeExecute(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            action();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception during synchronous execution");
            _ = Task.Run(() => Telemetry.TrackException(ex, ResultState.Failure, typeof(ResultBase)));
        }
    }

    /// <summary>
    ///     Executes an asynchronous action safely, optimized for ValueTask.
    /// </summary>
    protected static async ValueTask SafeExecuteAsync(
        Func<CancellationToken, ValueTask> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            await action(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.Error(ex, "Exception during asynchronous execution");
            _ = Task.Run(() => Telemetry.TrackException(ex, ResultState.Failure, typeof(ResultBase)),
                cancellationToken);
        }
    }

    /// <summary>
    ///     Executes an asynchronous Task action safely (legacy support).
    /// </summary>
    protected static async ValueTask SafeExecuteAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            await action(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.Error(ex, "Exception during asynchronous execution");
            _ = Task.Run(() => Telemetry.TrackException(ex, ResultState.Failure, typeof(ResultBase)),
                cancellationToken);
        }
    }

    /// <summary>
    ///     Executes an asynchronous action safely with a timeout.
    /// </summary>
    protected static async ValueTask SafeExecuteWithTimeoutAsync(
        Func<CancellationToken, ValueTask> action,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await action(cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.Error(ex, "Exception during asynchronous execution with timeout");
            _ = Task.Run(() => Telemetry.TrackException(ex, ResultState.Failure, typeof(ResultBase)), cts.Token);
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    ///     Validates that the specified state is a valid <see cref="ResultState" />.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateState(ResultState state)
    {
        if (!ValidStates.Contains(state))
        {
            throw new ResultValidationException($"Invalid result state: {state}");
        }
    }

    /// <summary>
    ///     Creates an array of exceptions from a single exception.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Exception[] CreateExceptionArray(Exception? exception)
    {
        return exception switch
        {
            null => [],
            AggregateException aggregateException => aggregateException.InnerExceptions.ToArray(),
            _ => [exception]
        };
    }

    #endregion

    #region Performance Metrics

    /// <summary>
    ///     Gets performance metrics for this result instance.
    /// </summary>
    public ResultPerformanceMetrics GetPerformanceMetrics()
    {
        var elapsed = DateTime.UtcNow - CreatedAt;
        return new ResultPerformanceMetrics(
            elapsed,
            _exceptions.Length,
            State,
            GetType().Name);
    }

    #endregion

    #region Debugging Support

    private string DebuggerDisplay => $"State = {State}, Success = {IsSuccess}, Exceptions = {_exceptions.Length}";

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Dictionary<string, object> DebugView
    {
        get
        {
            var items = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "State", State },
                { "IsSuccess", IsSuccess },
                { "IsCompleteSuccess", IsCompleteSuccess },
                { "ExceptionCount", _exceptions.Length },
                { "CreatedAt", CreatedAt },
                { "Age", DateTime.UtcNow - CreatedAt }
            };

            if (Exception != null)
            {
                items.Add("PrimaryException", Exception.Message);
            }

            if (_exceptions.Length > 1)
            {
                items.Add("AllExceptions", _exceptions.Select(e => e.Message).ToArray());
            }

            return items;
        }
    }

    #endregion
}
