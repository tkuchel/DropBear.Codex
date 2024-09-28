#region

using DropBear.Codex.Core.Logging;
using MessagePipe;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Filters;

public sealed class AsyncExceptionHandlingFilter<TMessage> : AsyncMessageHandlerFilter<TMessage>
{
    private readonly ILogger _logger;

    public AsyncExceptionHandlingFilter()
    {
        _logger = LoggerFactory.Logger.ForContext(typeof(TMessage));
    }

    public override async ValueTask HandleAsync(TMessage message, CancellationToken cancellationToken,
        Func<TMessage, CancellationToken, ValueTask> next)
    {
        try
        {
            await next(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "An exception occurred while handling message of type {MessageType}: {Message}",
                typeof(TMessage).Name, message);
            // Optionally, you can rethrow or handle the exception accordingly
        }
    }
}
