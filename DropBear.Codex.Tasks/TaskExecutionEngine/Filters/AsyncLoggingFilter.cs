#region

using DropBear.Codex.Core.Logging;
using MessagePipe;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Filters;

/// <summary>
///     Provides logging around the asynchronous handling of messages.
/// </summary>
/// <typeparam name="TMessage">The type of the message being handled.</typeparam>
public sealed class AsyncLoggingFilter<TMessage> : AsyncMessageHandlerFilter<TMessage>
{
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AsyncLoggingFilter{TMessage}" /> class.
    /// </summary>
    public AsyncLoggingFilter()
    {
        _logger = LoggerFactory.Logger.ForContext(typeof(TMessage));
    }

    /// <summary>
    ///     Logs the start and end of asynchronous message handling.
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
        var messageDetails = GetMessageDetails(message);

        try
        {
            _logger.Information("Starting async handling of message type {MessageType}: {MessageDetails}",
                typeof(TMessage).Name, messageDetails);

            await next(message, cancellationToken).ConfigureAwait(false);

            _logger.Information("Finished async handling of message type {MessageType}", typeof(TMessage).Name);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during async handling of message type {MessageType}: {MessageDetails}",
                typeof(TMessage).Name, messageDetails);
            throw; // Re-throw the exception to ensure it is handled upstream
        }
    }

    /// <summary>
    ///     Extracts a meaningful representation of the message for logging purposes.
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
