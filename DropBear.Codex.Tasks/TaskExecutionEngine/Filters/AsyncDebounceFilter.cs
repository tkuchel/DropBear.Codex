#region

using MessagePipe;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Filters;

public sealed class AsyncDebounceFilter<TMessage> : AsyncMessageHandlerFilter<TMessage>
{
    private readonly TimeSpan _debounceTime;
    private DateTime _lastHandledTime;

    public AsyncDebounceFilter(TimeSpan debounceTime)
    {
        _debounceTime = debounceTime;
        _lastHandledTime = DateTime.MinValue;
    }

    public override async ValueTask HandleAsync(TMessage message, CancellationToken cancellationToken,
        Func<TMessage, CancellationToken, ValueTask> next)
    {
        var now = DateTime.UtcNow;
        if (now - _lastHandledTime >= _debounceTime)
        {
            _lastHandledTime = now;
            await next(message, cancellationToken).ConfigureAwait(false);
        }
    }

    // EXAMPLE
    // _progressSubscription = ProgressSubscriber.Subscribe(_channelId, async (TaskProgressMessage message, CancellationToken cancellationToken) =>
    // {
    //     // Handle the message
    // }, new DebounceFilter<TaskProgressMessage>(TimeSpan.FromMilliseconds(500)));
}
