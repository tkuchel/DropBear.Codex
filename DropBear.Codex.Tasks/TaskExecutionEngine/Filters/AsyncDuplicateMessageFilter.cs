#region

using MessagePipe;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Filters;

public class AsyncDuplicateMessageFilter<TMessage> : AsyncMessageHandlerFilter<TMessage>
{
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _memoryDuration;
    private readonly HashSet<TMessage> _recentMessages = new();

    public AsyncDuplicateMessageFilter(TimeSpan memoryDuration)
    {
        _memoryDuration = memoryDuration;
        _cleanupTimer = new Timer(Cleanup, null, memoryDuration, memoryDuration);
    }

    public override async ValueTask HandleAsync(TMessage message, CancellationToken cancellationToken,
        Func<TMessage, CancellationToken, ValueTask> next)
    {
        if (!_recentMessages.Add(message))
        {
            // Duplicate message, ignore
            return;
        }

        await next(message, cancellationToken).ConfigureAwait(false);
    }

    private void Cleanup(object? state)
    {
        _recentMessages.Clear();
    }

    // EXAMPLE
    // _progressSubscription = ProgressSubscriber.Subscribe(_channelId, async (TaskProgressMessage message, CancellationToken cancellationToken) =>
    // {
    //     // Handle the message
    // }, new AsyncDuplicateMessageFilter<TaskProgressMessage>(TimeSpan.FromMilliseconds(500)));
}
