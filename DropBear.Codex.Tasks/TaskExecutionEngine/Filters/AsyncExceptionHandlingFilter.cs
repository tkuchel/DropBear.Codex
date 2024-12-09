#region

using DropBear.Codex.Core.Logging;
using MessagePipe;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Filters;

/// <summary>
///     Provides a filter for handling exceptions during asynchronous message processing.
/// </summary>
/// <typeparam name="TMessage">The type of the message being handled.</typeparam>
public sealed class AsyncExceptionHandlingFilter<TMessage> : AsyncMessageHandlerFilter<TMessage>
{
    private readonly Action<Exception, TMessage>? _customErrorHandler;
    private readonly ILogger _logger;
    private readonly bool _rethrow;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AsyncExceptionHandlingFilter{TMessage}" /> class.
    /// </summary>
    /// <param name="customErrorHandler">Optional custom error handler for additional exception processing.</param>
    /// <param name="rethrow">Indicates whether to rethrow exceptions after handling them.</param>
    public AsyncExceptionHandlingFilter(Action<Exception, TMessage>? customErrorHandler = null, bool rethrow = false)
    {
        _logger = LoggerFactory.Logger.ForContext(typeof(TMessage));
        _customErrorHandler = customErrorHandler;
        _rethrow = rethrow;
    }

    /// <summary>
    ///     Handles exceptions that occur during message processing.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <param name="next">The next handler in the pipeline.</param>
    /// <returns>A <see cref="ValueTask" /> representing the asynchronous operation.</returns>
    public override async ValueTask HandleAsync(
        TMessage message,
        CancellationToken cancellationToken,
        Func<TMessage, CancellationToken, ValueTask> next)
    {
        try
        {
            await next(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "An exception occurred while handling message of type {MessageType}: {Message}",
                typeof(TMessage).Name, GetMessageDetails(message));

            _customErrorHandler?.Invoke(ex, message);

            if (_rethrow)
            {
                throw; // Re-throw the exception if configured to do so
            }
        }
    }

    /// <summary>
    ///     Extracts detailed information about the message for logging purposes.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <returns>A string representation of the message.</returns>
    private static string GetMessageDetails(TMessage message)
    {
        try
        {
            return message?.ToString() ?? "null";
        }
        catch
        {
            return "Unable to retrieve message details.";
        }
    }
}
