#region

using DropBear.Codex.Core.Logging;
using MessagePipe;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Filters;

public sealed class AsyncRetryFilter<TMessage> : AsyncMessageHandlerFilter<TMessage>
{
    private readonly ILogger _logger;
    private readonly int _maxRetryCount;
    private readonly TimeSpan _retryDelay;

    public AsyncRetryFilter(int maxRetryCount, TimeSpan retryDelay)
    {
        _maxRetryCount = maxRetryCount;
        _retryDelay = retryDelay;
        _logger = LoggerFactory.Logger.ForContext(typeof(TMessage));
    }

    public override async ValueTask HandleAsync(TMessage message, CancellationToken cancellationToken,
        Func<TMessage, CancellationToken, ValueTask> next)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                attempt++;
                await next(message, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (attempt <= _maxRetryCount)
            {
                _logger.Warning(ex,
                    "Exception occurred while handling message of type {MessageType}. Attempt {Attempt} of {MaxRetryCount}. Retrying after {RetryDelay} ms.",
                    typeof(TMessage).Name, attempt, _maxRetryCount, _retryDelay.TotalMilliseconds);

                await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
