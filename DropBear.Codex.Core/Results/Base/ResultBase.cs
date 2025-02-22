#region

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Diagnostics;
using DropBear.Codex.Core.Results.Errors;
using Microsoft.Extensions.ObjectPool;
using Serilog;

#endregion

namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     An abstract base class for all Result types, providing common functionality.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public abstract class ResultBase : IResult
{
    private protected static readonly ILogger Logger = LoggerFactory.Logger.ForContext<ResultBase>();
    private protected static readonly IResultTelemetry Telemetry = new DefaultResultTelemetry();

    private static readonly ConcurrentDictionary<Type, DefaultObjectPool<List<Exception>>> ExceptionListPool = new();


    private static readonly HashSet<ResultState> ValidStates = [..Enum.GetValues<ResultState>()];
    private readonly IReadOnlyCollection<Exception> _exceptions;

    #region Constructor

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

        // Track telemetry
        Telemetry.TrackResultCreated(state, GetType());

        if (exception != null)
        {
            Telemetry.TrackException(exception, state, GetType());
        }
    }

    #endregion

    #region Properties

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

    #region Protected Methods

    /// <summary>
    ///     Executes the specified action safely, catching and logging any exceptions.
    /// </summary>
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateState(ResultState state)
    {
        if (!ValidStates.Contains(state))
        {
            throw new ResultValidationException($"Invalid result state: {state}");
        }
    }

    private static IReadOnlyCollection<Exception> CreateExceptionCollection(Exception? exception)
    {
        if (exception is null)
        {
            return Array.Empty<Exception>();
        }

        var pool = ExceptionListPool.GetOrAdd(
            typeof(ResultBase),
            _ => new DefaultObjectPool<List<Exception>>(
                new DefaultPooledObjectPolicy<List<Exception>>()));

        var exceptionList = pool.Get();
        try
        {
            if (exception is AggregateException aggregateException)
            {
                exceptionList.AddRange(aggregateException.InnerExceptions);
            }
            else
            {
                exceptionList.Add(exception);
            }

            return exceptionList.ToArray();
        }
        finally
        {
            pool.Return(exceptionList);
        }
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
