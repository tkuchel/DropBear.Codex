#region

using MessagePipe;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Filters;

public sealed class AsyncConditionalFilter<TMessage> : AsyncMessageHandlerFilter<TMessage>
{
    private readonly Func<TMessage, bool> _predicate;

    public AsyncConditionalFilter(Func<TMessage, bool> predicate)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    public override async ValueTask HandleAsync(TMessage message, CancellationToken cancellationToken,
        Func<TMessage, CancellationToken, ValueTask> next)
    {
        if (_predicate(message))
        {
            await next(message, cancellationToken).ConfigureAwait(false);
        }
        // Else, message is ignored
    }

    // EXAMPLE
    // _progressSubscription = ProgressSubscriber.Subscribe(_channelId, async (TaskProgressMessage message, CancellationToken cancellationToken) =>
    // {
    //     // Handle the message
    // }, new ConditionalFilter<TaskProgressMessage>(message => message.Status == TaskStatus.InProgress));

}
