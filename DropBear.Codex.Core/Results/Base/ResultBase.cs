#region

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
///     Consolidates shared behavior for result types and provides a unified foundation.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public abstract class ResultBase : IResult, IResultDiagnostics
{
    // Static resources shared by all result types
    private protected static readonly ILogger Logger = LoggerFactory.Logger.ForContext<ResultBase>();
    private protected static readonly IResultTelemetry Telemetry = new DefaultResultTelemetry();

    // Set of valid result states for validation
    private static readonly HashSet<ResultState> ValidStates = [..Enum.GetValues<ResultState>()];

    // Diagnostic info for this result
    private readonly DiagnosticInfo _diagnosticInfo;

    // Exception collection for this result
    private readonly IReadOnlyCollection<Exception> _exceptions;

    // Gets the creation timestamp for this result
    public DateTime CreatedAt => _diagnosticInfo.CreatedAt;

    /// <summary>
    ///     Initializes a new instance of <see cref="ResultBase" />.
    /// </summary>
    /// <param name="state">The <see cref="ResultState" /> (e.g., Success, Failure, etc.).</param>
    /// <param name="exception">An optional <see cref="Exception" /> if the result failed.</param>
    protected ResultBase(ResultState state, Exception? exception = null)
    {
        ValidateState(state);

        State = state;
        Exception = exception;
        _exceptions = CreateExceptionCollection(exception);

        // Initialize diagnostic data
        _diagnosticInfo = new DiagnosticInfo(
            state,
            GetType(),
            DateTime.UtcNow,
            Activity.Current?.Id);

        // Track telemetry
        Telemetry.TrackResultCreated(state, GetType());

        if (exception != null)
        {
            Telemetry.TrackException(exception, state, GetType());
        }
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
    ///     Gets all exceptions associated with this result.
    /// </summary>
    public virtual IReadOnlyCollection<Exception> Exceptions => _exceptions;

    #endregion

    #region IResultDiagnostics Implementation

    /// <summary>
    ///     Gets diagnostic information about this result.
    /// </summary>
    public DiagnosticInfo GetDiagnostics()
    {
        return _diagnosticInfo;
    }

    /// <summary>
    ///     Gets the trace context for this result operation.
    /// </summary>
    public ActivityContext GetTraceContext()
    {
        return Activity.Current?.Context ?? default;
    }

    #endregion

    #region Protected Methods

    /// <summary>
    ///     Executes the specified action safely, catching and logging any exceptions.
    /// </summary>
    /// <param name="action">The action to execute.</param>
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
            Telemetry.TrackException(ex, ResultState.Failure, typeof(ResultBase));
        }
    }

    /// <summary>
    ///     Executes an asynchronous action safely, catching and logging any exceptions.
    /// </summary>
    /// <param name="action">The asynchronous action to execute.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A <see cref="ValueTask" /> representing the asynchronous operation.</returns>
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
            Telemetry.TrackException(ex, ResultState.Failure, typeof(ResultBase));
        }
    }

    /// <summary>
    ///     Executes an asynchronous action safely with a timeout.
    /// </summary>
    /// <param name="action">The asynchronous action to execute.</param>
    /// <param name="timeout">The timeout period.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A <see cref="ValueTask" /> representing the asynchronous operation.</returns>
    protected static async ValueTask SafeExecuteWithTimeoutAsync(
        Func<CancellationToken, Task> action,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be greater than zero.");
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await action(cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.Error(ex, "Exception during asynchronous execution with timeout");
            Telemetry.TrackException(ex, ResultState.Failure, typeof(ResultBase));
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    ///     Validates that the specified state is a valid <see cref="ResultState" />.
    /// </summary>
    /// <param name="state">The state to validate.</param>
    /// <exception cref="ResultValidationException">Thrown if the state is invalid.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateState(ResultState state)
    {
        if (!ValidStates.Contains(state))
        {
            throw new ResultValidationException($"Invalid result state: {state}");
        }
    }

    /// <summary>
    ///     Creates a collection of exceptions from a single exception.
    /// </summary>
    /// <param name="exception">The exception to include in the collection, if any.</param>
    /// <returns>A read-only collection of exceptions.</returns>
    private static IReadOnlyCollection<Exception> CreateExceptionCollection(Exception? exception)
    {
        if (exception is null)
        {
            return Array.Empty<Exception>();
        }

        if (exception is AggregateException aggregateException)
        {
            return aggregateException.InnerExceptions;
        }

        return new[] { exception };
    }

    #endregion

    #region Debugging Support

    private string DebuggerDisplay => $"State = {State}, Success = {IsSuccess}";

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebugView
    {
        get
        {
            var items = new Dictionary<string, object>
                (StringComparer.Ordinal)
                {
                    { "State", State },
                    { "IsSuccess", IsSuccess },
                    { "IsCompleteSuccess", IsCompleteSuccess },
                    { "HasException", Exception != null }
                };

            if (Exception != null)
            {
                items.Add("Exception", Exception.Message);
            }

            return string.Join(Environment.NewLine,
                items.Select(kvp => $"{kvp.Key} = {kvp.Value}"));
        }
    }

    #endregion
}
