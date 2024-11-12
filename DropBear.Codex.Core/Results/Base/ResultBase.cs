#region

using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     Base class for all Result types providing common functionality
/// </summary>
public abstract class ResultBase : IResult
{
    private protected static readonly ILogger Logger = LoggerFactory.Logger.ForContext<ResultBase>();
    private readonly IReadOnlyCollection<Exception> _exceptions;

    #region Constructor

    /// <summary>
    ///     Initializes a new instance of ResultBase
    /// </summary>
    /// <param name="state">The result state</param>
    /// <param name="exception">Optional exception that caused a failure</param>
    /// <exception cref="ArgumentException">If state is invalid</exception>
    protected ResultBase(ResultState state, Exception? exception = null)
    {
        ValidateState(state);

        State = state;
        Exception = exception;
        _exceptions = CreateExceptionCollection(exception);
    }

    #endregion

    #region Public Properties

    /// <summary>
    ///     Gets the state of the result
    /// </summary>
    public ResultState State { get; }

    /// <summary>
    ///     Gets whether the result represents a success
    /// </summary>
    public bool IsSuccess => State is ResultState.Success or ResultState.PartialSuccess;

    /// <summary>
    ///     Gets whether the result represents a complete success
    /// </summary>
    public bool IsCompleteSuccess => State is ResultState.Success;

    /// <summary>
    ///     Gets whether the result represents a failure
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    ///     Gets the primary exception if the result failed
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    ///     Gets all exceptions associated with this result
    /// </summary>
    public virtual IReadOnlyCollection<Exception> Exceptions => _exceptions;

    #endregion

    #region Protected Methods

    /// <summary>
    ///     Executes an action safely, catching and logging any exceptions
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
        }
    }

    /// <summary>
    ///     Executes an async action safely, catching and logging any exceptions
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
        }
    }

    /// <summary>
    ///     Executes an async action safely with a timeout
    /// </summary>
    protected static async ValueTask SafeExecuteWithTimeoutAsync(
        Func<CancellationToken, Task> action,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be greater than zero");
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
        }
    }

    #endregion

    #region Private Methods

    private static void ValidateState(ResultState state)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentException($"Invalid result state: {state}", nameof(state));
        }
    }

    private static IReadOnlyCollection<Exception> CreateExceptionCollection(Exception? exception)
    {
        if (exception is null)
        {
            return Array.Empty<Exception>();
        }

        if (exception is AggregateException aggregateException)
        {
            return aggregateException.InnerExceptions.ToList().AsReadOnly();
        }

        return new[] { exception }.ToList().AsReadOnly();
    }

    #endregion
}
