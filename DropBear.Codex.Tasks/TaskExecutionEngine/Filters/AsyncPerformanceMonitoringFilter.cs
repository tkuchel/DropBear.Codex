#region

using System.Diagnostics;
using DropBear.Codex.Core.Logging;
using MessagePipe;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Filters;

public sealed class AsyncPerformanceMonitoringFilter<TMessage> : AsyncMessageHandlerFilter<TMessage>
{
    private readonly ILogger _logger;

    public AsyncPerformanceMonitoringFilter()
    {
        _logger = LoggerFactory.Logger.ForContext(typeof(TMessage));
    }

    public override async ValueTask HandleAsync(TMessage message, CancellationToken cancellationToken,
        Func<TMessage, CancellationToken, ValueTask> next)
    {
        var stopwatch = Stopwatch.StartNew();

        await next(message, cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();
        _logger.Information("Handled message of type {MessageType} in {ElapsedMilliseconds} ms", typeof(TMessage).Name,
            stopwatch.ElapsedMilliseconds);
    }
}
