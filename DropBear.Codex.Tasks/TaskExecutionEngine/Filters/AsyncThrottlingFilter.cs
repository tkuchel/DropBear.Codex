#region

using DropBear.Codex.Core.Logging;
using MessagePipe;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Filters;

public sealed class AsyncThrottlingFilter<TMessage> : AsyncMessageHandlerFilter<TMessage>
{
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _semaphore;

    public AsyncThrottlingFilter(int maxConcurrentMessages)
    {
        _semaphore = new SemaphoreSlim(maxConcurrentMessages, maxConcurrentMessages);
        _logger = LoggerFactory.Logger.ForContext(typeof(TMessage));
    }

    public override async ValueTask HandleAsync(TMessage message, CancellationToken cancellationToken,
        Func<TMessage, CancellationToken, ValueTask> next)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await next(message, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
