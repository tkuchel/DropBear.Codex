#region

using DropBear.Codex.Core.Logging;
using MessagePipe;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Filters;

public sealed class AsyncLoggingFilter<TMessage> : AsyncMessageHandlerFilter<TMessage>
{
    private readonly ILogger _logger;

    public AsyncLoggingFilter()
    {
        _logger = LoggerFactory.Logger.ForContext(typeof(TMessage));
    }

    public override async ValueTask HandleAsync(TMessage message, CancellationToken cancellationToken,
        Func<TMessage, CancellationToken, ValueTask> next)
    {
        _logger.Information("Starting async handling of message type {MessageType}: {Message}",
            typeof(TMessage).Name, message);

        // Call the next handler in the pipeline
        await next(message, cancellationToken).ConfigureAwait(false);

        _logger.Information("Finished async handling of message type {MessageType}", typeof(TMessage).Name);
    }
}
